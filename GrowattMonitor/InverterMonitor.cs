﻿
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
using GrowattMonitor.Configuration;
using GrowattMonitorShared;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;


namespace GrowattMonitor
{
    public class InverterMonitor
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<InverterMonitor> _logger;
        private readonly AppConfig _appConfig;
        private readonly StorageTableHelper _storageTableHelper;
        private CloudTable _table;

        private static Socket _inverterSocket = null;
        private static Socket _serverSocket = null;

        private static CancellationTokenSource _cancellation;

        private byte[] _datalogger;
        private bool _listenToInverter = true;
        private static bool _firstIdentify = true;

        private MessageType prevMsgType;

        public List<DataloggerConfig> Config { get; set; } = new List<DataloggerConfig>();


        public InverterMonitor(ILoggerFactory loggerFactory, ILogger<InverterMonitor> logger, IOptions<AppConfig> appConfig)
        {
            _loggerFactory = loggerFactory;
            _logger = logger;
            _appConfig = appConfig.Value;

            _logger.LogDebug("StorrageConnectionString: {StorageConnectionstring}", _appConfig.StorageConnectionstring);

            _storageTableHelper = new StorageTableHelper(_appConfig.StorageConnectionstring);
        }

        public async Task Run()
        {
            IPAddress ip = IPAddress.Parse(_appConfig.DataloggerReceiverAddress);

            // Start listening for client requests.
            TcpListener listener = new TcpListener(ip, 5279);
            listener.Start();

            _cancellation = new CancellationTokenSource();
            _logger.LogInformation("Monitor started...");

            // Enter the listening loop.
            while (Utils.IsDaylight(_appConfig.Latitude, _appConfig.Longitude) && !_cancellation.Token.IsCancellationRequested)
            {
                try
                {
                    // Buffer for reading data
                    byte[] bytes = await ReceiveBytes(listener, _cancellation.Token);
                    // Handle data locally
                    await ProcessBytes(bytes, _cancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Monitor canceled!");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception in monitor");
                }
            }
            _logger.LogInformation("Monitor stopped...");

            // Shutdown and end sockets
            _inverterSocket?.Shutdown(SocketShutdown.Both);
            _inverterSocket?.Disconnect(reuseSocket: true);

            _serverSocket?.Shutdown(SocketShutdown.Both);
            _serverSocket?.Disconnect(reuseSocket: true);

            // Stop listening for new clients.
            listener.Stop();

        }

        private async Task<Socket> ConnectSocket(Socket socket, string host, CancellationToken cancellationToken,  TcpListener listener = null)
        {
            if (socket != null && socket.Connected)
                // Everything is awesome!
                return socket;

            if (listener == null && !_appConfig.ActAsProxy)
                return null;

            if (listener != null)
                _logger.LogInformation("==> Waiting for inverter to connect to proxy ({host})... ", host);
            else
                _logger.LogInformation("==> Waiting for proxy to connect tot server ({host})... ", host);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (socket == null)
                {
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                    {
                        NoDelay = true,
                        ReceiveBufferSize = 4096,
                        ReceiveTimeout = 500,
                        SendTimeout = 500
                    };
                }

                if (!socket.Connected)
                    if (listener != null)
                    {
                        socket = await listener.AcceptSocketAsync(_cancellation.Token);
                    }
                    else
                        await socket.ConnectAsync(host, 5279);

                _logger.LogInformation("...connection established");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in monitor");
            }

            return socket;
        }

        private async Task<byte[]> ReceiveBytes(TcpListener listener, CancellationToken cancellationToken)
        {
            int i = 0;

            cancellationToken.ThrowIfCancellationRequested();

            if (_inverterSocket == null || !_inverterSocket.Connected)
                _inverterSocket = await ConnectSocket(_inverterSocket, _appConfig.DataloggerReceiverAddress, cancellationToken, listener);
            if (_serverSocket == null || !_serverSocket.Connected)
                _serverSocket = await ConnectSocket(_serverSocket, _appConfig.GrowattServerAddress, cancellationToken);

            byte[] bytes = new byte[4096];

            try
            {
                if (_listenToInverter)
                    i = _inverterSocket.Receive(bytes, SocketFlags.None);
                else
                    i = _serverSocket.Receive(bytes, SocketFlags.None);
            }
           // catch (OperationCanceledException)
           // {
           //     Console.WriteLine("Canceled!");
           //     return null;
           // }
            catch (SocketException e)
            {
                if (e.SocketErrorCode == SocketError.TimedOut)
                {
                    if (Utils.IsDaylight(_appConfig.Latitude, _appConfig.Longitude) && ((DateTime.Now - Utils.riseTime).TotalMinutes < 20 || (DateTime.Now - Utils.setTime).TotalMinutes > 20))
                        _cancellation.Cancel(true);

                    _listenToInverter = true;
                    return null;
                }
            }
            bytes = bytes[0..i];

            return bytes;
        }

        private async Task ProcessBytes(byte[] buffer, CancellationToken cancellationToken)
        {
            while (buffer?.Length > 0)
            {
                // Process the data sent by the client.
                var msg = Message.CreateFromByteBuffer(buffer, _loggerFactory);

                // When in IDENTIFY loop, keep listening to inverter
                if (msg.Type == MessageType.IDENTIFY && prevMsgType == MessageType.IDENTIFY)
                {
                    _listenToInverter = true;
                }

                string source;
                if (_listenToInverter)
                    source = "INVERTER";
                else
                    source = "SERVER";

                msg.Dump($"FROM {source}", _appConfig.ShowBytesInDump);

                Message reply = null;

                if (msg != null)
                {
                    reply = await ProcessMessage(msg);
                    buffer = msg.Remaining;

                }
                if (reply != null)
                {
                    SendMessage(reply, cancellationToken);
                }
                prevMsgType = msg.Type;
            }
        }

        private void SendMessage(Message msg, CancellationToken cancellationToken = default)
        {
            Socket s;
            //string dest;
            bool sendMessage = true;

            if (_listenToInverter)
            {
                s = _serverSocket = ConnectSocket(_serverSocket, _appConfig.GrowattServerAddress, cancellationToken).Result;
                //dest = "SERVER";

                _listenToInverter = false;

                sendMessage = _appConfig.ActAsProxy;
            }
            else
            {
                s = _inverterSocket = ConnectSocket(_inverterSocket, _appConfig.DataloggerReceiverAddress, cancellationToken).Result;
                //dest = "INVERTER";

                _listenToInverter = true;
            }

            if (sendMessage)
            {
                s.SendBufferSize = msg.Content.Length;

                //int i =
                s.Send(msg.Content);

                //if (i > 0)
                //msg.Dump($"TO {dest}", _appConfig.ShowBytesInDump);
            }
        }

        private void SendIdentify()
        {
            byte[] request = _datalogger.Concat(new byte[] { 0x00, 0x04, 0x00, 0x15 }).ToArray();

            var msg = Message.Create(MessageType.IDENTIFY, request, 1);

            SendMessage(msg);

        }

        private async Task<Message> ProcessMessage(Message msg)
        {
            var data = msg.Decode();
            var reply = msg.Type switch
            {
                MessageType.PING => ProcessPing((Dictionary<string, object>)data),
                MessageType.IDENTIFY => ProcessIdentify(msg),
                MessageType.ANNOUNCE => ProcessAnnounce(msg, (Dictionary<string, object>)data),
                MessageType.CURRDATA => await ProcessData(msg, (Telegram)data),
                MessageType.HISTDATA => await ProcessData(msg, (Telegram)data),
                MessageType.CONFIG => ProcessConfig(msg, (DataloggerConfig)data),
                MessageType.REBOOT => null,
                //MessageType.CONFACK => ProcessConfAck((List<Config>)data),
                _ => null,
            };
            return reply;
        }
        private Message ProcessPing(Dictionary<string, object> data)
        {
            if (_listenToInverter)
            {
                Console.WriteLine($"==> Received ping from {Display(data["datalogger"])}");
                if (_datalogger == null)
                    _datalogger = (byte[])data["datalogger"];
            }

            // Send Identify
            if (!_appConfig.ActAsProxy)
            {
                SendIdentify();
            }

            Message reply = Message.Create(MessageType.PING, (byte[])data["datalogger"], (ushort)data["id"]);
            return reply;
        }

        private Message ProcessIdentify(Message msg)
        {
            try {
            if (_listenToInverter)
            {
                var cfg = msg.DecodeIdentifyDetail();
                Config.Add(cfg); //.Index] = c.Value;

                _logger.LogInformation("==> Inverter says: {display}", cfg.Display());

            }
            else
            {
                var idreqdata = msg.DecodeIdentifyRequest();
                if (_firstIdentify)
                {
                    _logger.LogInformation("==> Reveived request for config items {first} to {last}...", idreqdata["first"], idreqdata["last"]);
                    _firstIdentify = false;
                }
            }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in monitor: {message}", ex.Message);
            }
            Message reply = Message.Create(msg.Type, msg.Body, msg.Id);
            return reply;
        }

        private Message ProcessAnnounce(Message msg, Dictionary<string, object> data)
        {
            if (!msg.IsAck)
            {
                if (_listenToInverter)
                {

                    _logger.LogInformation("Received announcement from {make} - {inverter} ({inverteralias})",Display(data["make"]), Display(data["inverter"]), Display(data["inverteralias"]));
                    _logger.LogInformation("Data logger: {datalogger}", Display(data["datalogger"]));
                    _logger.LogInformation("Version: {version}", Display(data["version"]));
                    _logger.LogInformation("Build: {build}", Display(data["build"]));

                    var result = BitConverter.ToString((byte[])data["model"]).Replace("-", "").ToCharArray();

                    string model = "A" + result[0] +
                                   "B" + result[1] +
                                   "D" + result[2] +
                                   "T" + result[3] +
                                   "P" + result[4] +
                                   "U" + result[5] +
                                   "M" + result[6] +
                                   "S" + result[7];

                    _logger.LogInformation("Model {model}",model);

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
                _logger.LogInformation("==> ANNOUNCE ACK received");
            }

            Message reply = Message.Create(msg.Type, msg.Body, msg.Id);
            return reply;
        }

        private async Task<Message> ProcessData(Message msg, Telegram telegram)
        {
            if (msg.IsAck)
            {
                _logger.LogInformation("==> DATA ACK received ({type})", msg.Type);
            }
            else
            {
                if (_listenToInverter)
                {
                    await StoreTelegram(telegram);
                }

                if (telegram?.InvStat == 0 && msg.Type == MessageType.CURRDATA)
                {
                    _cancellation.Cancel(true);
                }
            }

            Message reply = Message.Create(msg.Type, msg.Body, msg.Id);
            return reply;
        }

        private Message ProcessConfig(Message msg, DataloggerConfig cfg)
        {
            if (!msg.IsAck)
            {
                _logger.LogInformation("==> Configuration received: {Display}", cfg.Display());
            }
            else
            {
                _logger.LogInformation("==> Configuration ACK received");
            }
            Message reply = Message.Create(MessageType.CONFIG, msg.Body, msg.Id);
            return reply;
        }

        private async Task StoreTelegram(Telegram telegram)
        {
            if (telegram == null)
            {
                throw new ArgumentNullException("telegram");
            }

            // Create or reference an existing table

            telegram.Dump();

            string tablename = telegram.GetTablename(_appConfig.TablenamePrefix);

            if (_table?.Name != tablename)
                _table = await _storageTableHelper.GetTableAsync(tablename);

            try
            {
                // Create the InsertOrReplace table operation
                TableOperation insertOrMergeOperation = TableOperation.InsertOrMerge(telegram);

                // Execute the operation.
                TableResult result = await _table.ExecuteAsync(insertOrMergeOperation);
                if (result.HttpStatusCode == (int) HttpStatusCode.NoContent)
                    _logger.LogInformation("Telegram {rowkey} stored in table {tablename}", telegram.RowKey, tablename);

                return;
            }
            catch (StorageException ex)
            {
                _logger.LogError("Error in monitor: {message}", ex.Message);
                throw;
            }
        }

        private string Display(Object data)
        {
            return Encoding.Default.GetString((byte[])data);
        }
        //private Message SendConfigInterval()
        //{
        //    byte[] request = _datalogger.Concat(new byte[] { 0x0, 0x04, 0x0, 0x01, 0x00 }).ToArray();
        //    Message reply = Message.Create(MessageType.CONFIG, request);
        //    return reply;
        //}


        //private Message SendConfigDate()
        //{

        //    string datetime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        //    byte[] dt = Encoding.Default.GetBytes(datetime);
        //    byte[] content = _datalogger.Concat(new byte[] { 0x00, 0x1F, 0x00, 0x13 })
        //        .Concat(dt)
        //        .ToArray();

        //    Message reply = Message.Create(MessageType.CONFIG, content);
        //    return reply;
        //}
    }
}