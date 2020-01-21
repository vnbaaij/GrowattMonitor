using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GrowattMonitorShared;

namespace GrowattMonitorShared
{
    public class InverterMonitor
    {
        private static string inverterAddress = "192.168.1.108";
        private static string serverAddress = "server.growatt.com";

        private static MonitorState _state = MonitorState.WAITING;
        private static Socket _inverterSocket = null;
        private static Socket _serverSocket = null;

        private static ushort _version = 5;
        private byte[] _datalogger;
        private bool bListenToInverter = true;
        private static bool bFirstIdentify = true;

        public List<Config> Config { get; set; } = new List<Config>();

        public void Run()
        {
            TcpListener listener = null;
            try
            {
                IPAddress ip = IPAddress.Parse(inverterAddress);

                listener = new TcpListener(ip, 5279);

                // Start listening for client requests.
                listener.Start();
                Console.Write("Monitor started...\n");


                // Enter the listening loop.
                while (true)
                {
                    // Buffer for reading data
                    //byte[] bytes = new Byte[2048];

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
                // Shutdown and end connection
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

            try
            {
                Console.Write($"\nWaiting to connect to '{host}'`... ");
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

                Console.WriteLine($"...Connection established");
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

            if (bListenToInverter)
            {
                s = _inverterSocket = ConnectSocket(_inverterSocket, inverterAddress, listener);
            }
            else
                s = _serverSocket = ConnectSocket(_serverSocket, serverAddress);

            byte[] bytes = new byte[2048];

            try
            {
                i = s.Receive(bytes);
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode == SocketError.TimedOut)
                {
                    bListenToInverter = true;
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
                if (bListenToInverter)
                    source = "INVERTER";
                else
                    source = "SERVER";

                msg.Dump($"IN FROM {source}");

                msg.Inverter = this;
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
            if (!bListenToInverter)
            {
                s = _inverterSocket = ConnectSocket(_inverterSocket, inverterAddress);
                dest = "INVERTER";

                bListenToInverter = true;
            }
            else
            {
                s = _serverSocket = ConnectSocket(_serverSocket, serverAddress);
                dest = "SERVER";

                //if (!msg.InIdentifyProcess)
                bListenToInverter = false;

            }

            s.SendBufferSize = msg.Size;
            int i = s.Send(msg.Content);

            if (i > 0)
                msg.Dump($"OUT TO {dest}");
        }

        private void SendIdentify()
        {
            byte[] request = _datalogger.Concat(new byte[] { 0x00, 0x04, 0x00, 0x15 }).ToArray();

            var msg = Message.Create(_version, MessageType.IDENTIFY, request, 1); // (ushort)data["id"]); //, 0x51);

            SendMessage(msg);

            _state = MonitorState.IDENTIFY_SENT;
        }

        private Message SendConfigInterval(Message msg)
        {
            byte[] request = _datalogger.Concat(new byte[] { 0x0, 0x04, 0x0, 0x01, 0x00 }).ToArray();
            Message reply = Message.Create(msg.Version, MessageType.CONFIG, request);
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


            //Buffer.BlockCopy(_datalogger, 0, content, 0, _datalogger.Length);
            //Buffer.BlockCopy(new byte[] {0x00, 0x1F, 0x00, 0x13 }, 0, content, _datalogger.Length, 4);
            //Buffer.BlockCopy(dt, 0, content, _datalogger.Length + 4, dt.Length);

            Message reply = Message.Create(msg.Version, MessageType.CONFIG, content);
            return reply;
        }

        private Message ProcessMessage(Message msg)
        {
            var data = msg.Decode();
            var reply = msg.Type switch
            {
                MessageType.PING => ProcessPing(msg, (Dictionary<string, object>)data),
                MessageType.IDENTIFY => ProcessIdentify(msg),
                MessageType.ANNOUNCE => ProcessAnnounce(msg, (Dictionary<string, object>)data),
                MessageType.DATA => ProcessData(msg, (Telegram)data),
                MessageType.DATA2 => ProcessData2(msg, (Telegram)data),
                MessageType.CONFIG => ProcessConfig(msg, (Config)data),
                MessageType.REBOOT => null,
                //MessageType.CONFACK => //ProcessConfAck((List<Config>)data),
                _ => null,
            };
            return reply;
        }
        private Message ProcessPing(Message msg, Dictionary<string, object> data)
        {
            Console.WriteLine("==>");
            Console.WriteLine($"Received ping from {Display(data["datalogger"])}");
            _datalogger = (byte[])data["datalogger"];

            Message reply = Message.Create(msg.Version, MessageType.PING, (byte[])data["datalogger"], (ushort)data["id"]);

            // Send Identify
            //if (_state == MonitorState.WAITING)
            //{
            //    _state = MonitorState.OPEN;
            //    SendIdentify();
            //    if (_state != MonitorState.IDENTIFY_SENT)
            //        Console.WriteLine("Something went wrong with sending query");
            //}
            return reply;
        }

        private Message ProcessIdentify(Message msg)
        {
            if (bListenToInverter)
            {
                var cfg = msg.DecodeIdentifyDetail();
                Config.Add(cfg); //.Index] = c.Value;

                Console.WriteLine("==>");
                Console.WriteLine($"Inverter says: {cfg.Display()}");

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

                Console.WriteLine("==>");
                Console.WriteLine("Identify reply received from server");

                var idreqdata = msg.DecodeIdentifyRequest();
                if (bFirstIdentify)
                {
                    Console.WriteLine($"\nReveived request for config items {idreqdata["first"]} to {idreqdata["last"]}...");
                    bFirstIdentify = false;
                }

                //byte[] ack = ((byte[])idreqdata["datalogger"])
                //    .Concat(BitConverter.GetBytes((ushort)idreqdata["first"]))
                //    .Concat(BitConverter.GetBytes((ushort)idreqdata["last"])).ToArray();

                //reply = Message.Create(5, MessageType.IDENTIFY, ack);
                //msg =  null;
                //_state = InverterState.IDENTIFY_REPLIED;

                //SendConfig();


            }
            Message reply = Message.Create(msg.Version, msg.Type, msg.Body, msg.Id);
            return reply;
        }

        private Message ProcessAnnounce(Message msg, Dictionary<string, object> data)
        {

            if (bListenToInverter)
            {

                Console.WriteLine("==>");
                Console.WriteLine($"Received announcement from '{Display(data["make"])}' - {Display(data["inverter"])} ({Display(data["inverteralias"])})");
                Console.WriteLine($"Data logger: {Display(data["datalogger"])}");
                Console.WriteLine($"Version: {Display(data["version"])}");
                Console.WriteLine($"Build: {Display(data["build"])}");

                //Console.WriteLine($"{Display(data["blob1"])}");
                //Console.WriteLine($"{Display(data["blob2"])}");
                //Console.WriteLine($"{Display(data["blob3"])}");
                //Console.WriteLine($"{Display(data["blob4"])}");
                //DumpBytes("IN", (byte[])data["blob1"], false);
                //DumpBytes("IN", (byte[])data["blob2"], false);
                //DumpBytes("IN", (byte[])data["blob3"], false);
                //DumpBytes("IN", (byte[])data["blob4"], false);


                _version = msg.Version;
                _state = MonitorState.ANNOUNCE_SENT;
                
            }
            else
            {
                //Message reply = Message.Create(msg.Version, msg.Type, new byte[] { 0x00 }, (ushort)data["id"]);

                _version = msg.Version;
                _state = MonitorState.ANNOUNCE_RCVD;
                //return msg; //reply;
            }
            Message reply = Message.Create(msg.Version, msg.Type, msg.Body, msg.Id);
            return reply;
        }

        private Message ProcessData(Message msg, Telegram telegram)
        {
            if (bListenToInverter)
            {
                _state = MonitorState.DATA_RCVD;

                Console.WriteLine("==>");
                Console.WriteLine($"Received data");

                //byte[] request = new byte[] { 0x00 };

                //var reply = Message.Create(_version, msg.Type, request, msg.Id);

                //$measurement = new measurement(new repository\memory());
                //$measurement->readFromTelegram($this->data);
                //$measurement->save();
            }

            Message reply = Message.Create(msg.Version, msg.Type, msg.Body, msg.Id);
            return reply;
        }

        private Message ProcessData2(Message msg, Telegram telegram)
        {
            _state = MonitorState.DATA_RCVD;

            if (bListenToInverter)
            {
                Console.WriteLine("==>");
                Console.WriteLine($"Received data 150");

                //return msg;

                //$measurement = new measurement(new repository\memory());
                //$measurement->readFromTelegram($this->data);
                //$measurement->save();
            }
            else
            {
                //byte[] request = new byte[] { 0x00 };

                //var reply = Message.Create(_version, msg.Type, request, msg.Id);
                //return reply;
            }
            Message reply = Message.Create(msg.Version, msg.Type, msg.Body, msg.Id);
            return reply;
        }

        private Message ProcessConfig(Message msg, Config cfg)
        {
            if (!msg.IsAck)
            {
                Console.WriteLine("==>");
                Console.WriteLine($"Configuration received: {cfg.Display()}");

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
                Console.WriteLine("==>");
                Console.WriteLine($"Configuration ACK received");
            }
            Message reply = Message.Create(msg.Version, MessageType.CONFIG, msg.Body, msg.Id);
            return reply;
        }


        private string Display(Object data)
        {
            return Encoding.Default.GetString((byte[])data);
        }
    }
}
