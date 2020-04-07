using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Cosmos;
using GrowattMonitor.Configuration;
using GrowattMonitorShared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GrowattMonitor
{
    public class InverterMonitor
    {
        private readonly ILogger<InverterMonitor> _logger;
        public readonly ConfigSettings _config;
        private CosmosContainer _cosmosContainer;

        private static MonitorState _state = MonitorState.WAITING;
        private static Socket _inverterSocket = null;
        private static Socket _serverSocket = null;

        private byte[] _datalogger;
        private bool listenToInverter = true;
        private static bool firstIdentify = true;

        public List<Config> Config { get; set; } = new List<Config>();


        public InverterMonitor(ILogger<InverterMonitor> logger, IOptions<ConfigSettings> config)
        {
            _logger = logger;
            _config = config.Value;
        }

        public void Run(CosmosContainer cosmosContainer)
        {
            _cosmosContainer = cosmosContainer;

            TcpListener listener = null;
            try
            {
                IPAddress ip = IPAddress.Parse(_config.DataloggerReceiverAddress);

                listener = new TcpListener(ip, 5279);

                // Start listening for client requests.
                listener.Start();
                Console.WriteLine("Monitor started...");


                // Enter the listening loop.
                while (Utils.IsDaylight(_config.Latitude, _config.Longitude))
                {
                    // Buffer for reading data
                    byte[] bytes = ReceiveBytes(listener);
                    // Handle data locally
                    ProcessBytes(bytes);
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }
            finally
            {
                Console.WriteLine("Monitor stopped...");

                // Shutdown and end sockets
                _inverterSocket?.Close();
                _serverSocket?.Close();

                // Stop listening for new clients.
                listener.Stop();
            }
        }

        private Socket ConnectSocket(Socket socket, string host, TcpListener listener = null)
        {
            if (socket != null && socket.Connected)
                // Everything is awesome!
                return socket;

            if (listener == null && !_config.ActAsProxy)
                return null;

            try
            {
                Console.Write($"Waiting to connect to '{host}'... ");
                if (socket == null)
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                LingerOption lingerOptions = new LingerOption(true, 60);
                socket.LingerState = lingerOptions;
                socket.NoDelay = true;
                socket.ReceiveBufferSize = 2048;
                socket.ReceiveTimeout = 35000;
                socket.SendTimeout = 10000;

                if (!socket.Connected)
                    if (listener != null)
                        socket = listener.AcceptSocket();
                    else
                        socket.Connect(host, 5279);

                Console.WriteLine($"...connection established");
            }
            catch (SocketException e)
            {
                Console.WriteLine($"SocketException: {e}");
            }

            return socket;
        }

        private byte[] ReceiveBytes(TcpListener listener)
        {
            Socket s;
            int i = 0;

            if (listenToInverter)
                s = _inverterSocket = ConnectSocket(_inverterSocket, _config.DataloggerReceiverAddress, listener);
            else 
                s = _serverSocket = ConnectSocket(_serverSocket, _config.GrowattServerAddress);

            if (s == null && !_config.ActAsProxy)
            {
                listenToInverter = true;
                return null;
            }

            byte[] bytes = new byte[2048];

            try
            {
                i = s.Receive(bytes);
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode == SocketError.TimedOut)
                {
                    listenToInverter = true;
                    return null;
                }
            }
            bytes = bytes[0..i];

            return bytes;
        }

        private void ProcessBytes(byte[] buffer)
        {
            while (buffer?.Length > 0)
            {
                // Process the data sent by the client.
                var msg = Message.CreateFromByteBuffer(buffer);

                string source;
                if (listenToInverter)
                    source = "INVERTER";
                else
                    source = "SERVER";

                msg.Dump($"FROM {source}");

                Message reply = null;

                if (msg != null)
                {
                    reply = ProcessMessage(msg);
                    buffer = msg.Remaining;

                }
                if (reply != null)
                {
                    SendMessage(reply);
                }
            }
        }

        private void SendMessage(Message msg)
        {
            Socket s;
            string dest;
            bool sendMessage = true;

            if (!listenToInverter)
            {
                s = _inverterSocket = ConnectSocket(_inverterSocket, _config.DataloggerReceiverAddress);
                dest = "INVERTER";

                listenToInverter = true;
            }
            else
            {
                s = _serverSocket = ConnectSocket(_serverSocket, _config.GrowattServerAddress);
                dest = "SERVER";

                listenToInverter = false;

                sendMessage = _config.ActAsProxy;

            }

            if (sendMessage)
            {
                s.SendBufferSize = msg.Content.Length;

                int i = s.Send(msg.Content);

                if (i > 0)
                    msg.Dump($"TO {dest}");
            }
        }

        private void SendIdentify()
        {
            byte[] request = _datalogger.Concat(new byte[] { 0x00, 0x04, 0x00, 0x15 }).ToArray();

            var msg = Message.Create(MessageType.IDENTIFY, request, 1); // (ushort)data["id"]); //, 0x51);

            SendMessage(msg);

        }

        private Message SendConfigInterval(Message msg)
        {
            byte[] request = _datalogger.Concat(new byte[] { 0x0, 0x04, 0x0, 0x01, 0x00 }).ToArray();
            Message reply = Message.Create(MessageType.CONFIG, request);
            return reply;
        }

        private Message SendConfigDate(Message msg)
        {

            string datetime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            byte[] dt = Encoding.Default.GetBytes(datetime);
            //byte[] content = new byte[_datalogger.Length + dt.Length + 4];

            byte[] content = _datalogger.Concat(new byte[] { 0x00, 0x1F, 0x00, 0x13 })
                .Concat(dt)
                .ToArray();

            Message reply = Message.Create(MessageType.CONFIG, content);
            return reply;
        }

        private Message ProcessMessage(Message msg)
        {
            var data = msg.Decode();
            var reply = msg.Type switch
            {
                MessageType.PING => ProcessPing((Dictionary<string, object>)data),
                MessageType.IDENTIFY => ProcessIdentify(msg),
                MessageType.ANNOUNCE => ProcessAnnounce(msg, (Dictionary<string, object>)data),
                MessageType.CURRDATA => ProcessData(msg, (Telegram)data),
                MessageType.HISTDATA => ProcessData(msg, (Telegram)data),
                MessageType.CONFIG => ProcessConfig(msg, (Config)data),
                MessageType.REBOOT => null,
                //MessageType.CONFACK => ProcessConfAck((List<Config>)data),
                _ => null,
            };
            return reply;
        }
        private Message ProcessPing(Dictionary<string, object> data)
        {
            if (listenToInverter)
            {
                Console.WriteLine($"==> Received ping from {Display(data["datalogger"])}");
                if (_datalogger == null)
                    _datalogger = (byte[])data["datalogger"];
            }

            // Send Identify
            if (!_config.ActAsProxy)
            {
                _state = MonitorState.OPEN;
                SendIdentify();
                if (_state != MonitorState.IDENTIFY_SENT)
                    Console.WriteLine("Something went wrong with sending query");
            }

            Message reply = Message.Create(MessageType.PING, (byte[])data["datalogger"], (ushort)data["id"]);
            return reply;
        }

        private Message ProcessIdentify(Message msg)
        {
            if (listenToInverter)
            {
                var cfg = msg.DecodeIdentifyDetail();
                Config.Add(cfg); //.Index] = c.Value;

                Console.WriteLine($"==> Inverter says: {cfg.Display()}");

                //if (_state >= MonitorState.IDENTIFY_SENT && cfg.Index == "0x1F")
                //{
                //    _state = MonitorState.TIME_SET;
                //    return SendConfigDate(msg);
                //}

                //if (_state == MonitorState.TIME_SET)
                //{
                //    _state = MonitorState.IDENTIFY_RCVD;
                //    return SendConfigInterval(msg);
                //}

                //msg = null;

            }
            else
            {
                var idreqdata = msg.DecodeIdentifyRequest();
                if (firstIdentify)
                {
                    Console.WriteLine($"==> Reveived request for config items {idreqdata["first"]} to {idreqdata["last"]}...");
                    firstIdentify = false;
                }
            }
            Message reply = Message.Create(msg.Type, msg.Body, msg.Id);
            return reply;
        }

        private Message ProcessAnnounce(Message msg, Dictionary<string, object> data)
        {
            if (!msg.IsAck)
            {
                if (listenToInverter)
                {

                    Console.WriteLine("==>");
                    Console.WriteLine($"Received announcement from '{Display(data["make"])}' - {Display(data["inverter"])} ({Display(data["inverteralias"])})");
                    Console.WriteLine($"Data logger: {Display(data["datalogger"])}");
                    Console.WriteLine($"Version: {Display(data["version"])}");
                    Console.WriteLine($"Build: {Display(data["build"])}");

                    var result = BitConverter.ToString((byte[])data["model"]).Replace("-", "").ToCharArray();

                    string model = "A" + result[0] +
                                   "B" + result[1] +
                                   "D" + result[2] +
                                   "T" + result[3] +
                                   "P" + result[4] +
                                   "U" + result[5] +
                                   "M" + result[6] +
                                   "S" + result[7];



                    Console.WriteLine($"Model {model}");

                    //Console.WriteLine($"{Display(data["blob1"])}");
                    //Console.WriteLine($"{Display(data["blob2"])}");
                    //Console.WriteLine($"{Display(data["blob3"])}");
                    //Console.WriteLine($"{Display(data["blob4"])}");
                    //DumpBytes("IN", (byte[])data["blob1"], false);
                    //DumpBytes("IN", (byte[])data["blob2"], false);
                    //DumpBytes("IN", (byte[])data["blob3"], false);
                    //DumpBytes("IN", (byte[])data["blob4"], false);
                }
            }
            else
            {
                Console.WriteLine($"==> ANNOUNCE ACK received");
            }

            Message reply = Message.Create(msg.Type, msg.Body, msg.Id);
            return reply;
        }

        private Message ProcessData(Message msg, Telegram telegram)
        {
            if (msg.IsAck)
            {
                Console.WriteLine("==> DATA ACK received");
            }
            if (listenToInverter)
            {
                //telegram.Dump();
                SaveDataJson(telegram);
            }

            Message reply = Message.Create(msg.Type, msg.Body, msg.Id);
            return reply;
        }

        private Message ProcessConfig(Message msg, Config cfg)
        {
            if (!msg.IsAck)
            {
                Console.WriteLine($"==> Configuration received: {cfg.Display()}");

                // Send Identify
                //if (_state == MonitorState.TIME_SET)
                //{
                //    _state = MonitorState.OPEN;
                //    SendIdentify();
                //    if (_state != MonitorState.IDENTIFY_SENT)
                //        Console.WriteLine("Something went wrong with sending query");
                //}

                //byte[] data = new byte[] { 0x00, byte.Parse(cfg.Index[2..^0], System.Globalization.NumberStyles.HexNumber), 0x00, (byte)((byte[])cfg.Value).Length }
                //        .Concat((byte[])cfg.Value).ToArray();
            }
            else
            {
                Console.WriteLine($"==> Configuration ACK received");
            }
            Message reply = Message.Create(MessageType.CONFIG, msg.Body, msg.Id);
            return reply;
        }

        private async void SaveDataJson(Telegram data)
        {

            var options = new JsonSerializerOptions
            {
                IgnoreNullValues = true,
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase

            };

            var json = JsonSerializer.Serialize(data, options);
             Console.WriteLine($"json: {json}");


            //try
            //{
            //    ItemResponse<Telegram> cosmosResponse = await _cosmosContainer.ReadItemAsync<Telegram>(data.Id, new PartitionKey(data.Key));
            //    _logger.LogInformation("Item in database with id: {0} already exists\n", cosmosResponse.Value.Id);
            //}
            //catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            //{
            try 
            {
                ItemResponse<Telegram> cosmosResponse = await _cosmosContainer.CreateItemAsync<Telegram>(data, new PartitionKey(data.Key));
            }
            catch (CosmosException ex) when (ex.Status == (int) HttpStatusCode.Conflict)
            {
                 Console.WriteLine("Item in database with id: {0} already exists\n", data.Id);
            }
            finally{
                Console.WriteLine("Created item in database with id: {0}\n", data.Id);
            }

            //}
        }

        private string Display(Object data)
        {
            return Encoding.Default.GetString((byte[])data);
        }


       
    }
}
