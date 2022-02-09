using System.Net;
using System.Net.Sockets;
using System.Text;
using Azure;
using Azure.Data.Tables;
using GrowattMonitor.Configuration;
using GrowattMonitor.Helpers;
using GrowattMonitor.Models;
using Microsoft.Extensions.Options;


namespace GrowattMonitor;

public class InverterMonitor
{
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger<InverterMonitor> logger;
    private readonly AppConfig config;
    private readonly Utils utils;
    private readonly StorageHelper storage;
    
    private TableClient table;

    private static Socket inverterSocket = null;
    private static Socket serverSocket = null;

    private readonly CancellationTokenSource cancellation;
    //private static CancellationToken _cancellationToken;

    private byte[] datalogger;
    private bool listenToInverter = true;
    private static bool firstIdentify = true;
    private static bool inIdentifyLoop = false;

    private MessageType prevMsgType;
    private static string prevTablename;

    public List<DataloggerConfig> Config { get; set; } = new List<DataloggerConfig>();


    public InverterMonitor(ILoggerFactory loggerFactory, ILogger<InverterMonitor> logger, IOptions<AppConfig> config, Utils utils, StorageHelper storage)
    {
        this.loggerFactory = loggerFactory;
        this.logger = logger;
        this.config = config.Value;
        this.utils = utils;
        this.storage = storage;


        cancellation = new();
    }

    public async Task Run()
    {
        logger.LogInformation("Monitor running...");

        IPAddress ip = IPAddress.Parse(config.DataloggerReceiverAddress);

        // Start listening for client requests.
        TcpListener listener = new(ip, config.DataloggerReceiverPort);
        listener.Start();

        // Enter the listening loop.
        while (utils.IsDaylight() && !cancellation.IsCancellationRequested)
        {
            try
            {
                // Buffer for reading data
                byte[] bytes = await ReceiveBytes(listener);
                // Handle data locally
                await ProcessBytes(bytes);
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Monitor canceled!");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception in monitor while running: ");
                cancellation.Cancel();
            }
        }
              

        if (!utils.IsDaylight())
        {
            logger.LogInformation("Outside of daylight period...");
        }
        logger.LogInformation("Monitor stopped...");

        // Shutdown and end sockets
        inverterSocket?.Shutdown(SocketShutdown.Both);
        inverterSocket?.Disconnect(reuseSocket: true);

        serverSocket?.Shutdown(SocketShutdown.Both);
        serverSocket?.Disconnect(reuseSocket: true);

        // Stop listening for new clients.
        listener.Stop();

        
    }
    
    private async Task<Socket> ConnectSocket(Socket socket, TcpListener listener = null)
    {
        if (socket != null && socket.Connected)
            // Everything is awesome!
            return socket;

        if (listener == null && !config.ActAsProxy)
            return null;

        string host;
        int port;
        if (listener != null)
        {
            host = config.DataloggerReceiverAddress;
            port = config.DataloggerReceiverPort;
            logger.LogInformation("==> Waiting for inverter to connect to proxy ({host}:{port})... ", host, port);
        }
        else
        {
            host = config.GrowattServerAddress;
            port = config.GrowattServerPort;
            logger.LogInformation("==> Waiting for proxy to connect to server ({host}:{port})... ", host, port);
        }

        try
        {
            cancellation.Token.ThrowIfCancellationRequested();

            if (socket == null)
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true,
                    ReceiveBufferSize = 4096,
                    ReceiveTimeout = 10000,
                    SendTimeout = 3000
                };
            }

            if (!socket.Connected)
                if (listener != null)
                {
                    socket = await listener.AcceptSocketAsync(cancellation.Token);
                }
                else
                    await socket.ConnectAsync(host, port);

            logger.LogInformation("...connection established");
        }
        catch (OperationCanceledException)
        {
            logger.LogError("Connect socket canceled!");
            listenToInverter = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception in monitor when connecting socket: ");
            listenToInverter = true;
        }

        return socket;
    }

    private async Task<byte[]> ReceiveBytes(TcpListener listener)
    {
        int i = 0;

        cancellation.Token.ThrowIfCancellationRequested();

        if (inverterSocket == null || !inverterSocket.Connected)
            inverterSocket = await ConnectSocket(inverterSocket, listener);
        if (serverSocket == null || !serverSocket.Connected)
            serverSocket = await ConnectSocket(serverSocket);

        byte[] bytes = new byte[4096];

        try
        {
            if (listenToInverter)
                i = inverterSocket.Receive(bytes, SocketFlags.None);
            else
                i = serverSocket.Receive(bytes, SocketFlags.None);
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Receive bytes canceled!");
            return null;
        }
        catch (SocketException e)
        {
            if (e.SocketErrorCode == SocketError.TimedOut)
            {
                //_logger.LogError(e, "Error when receiving bytes: ");
                //if (Utils.IsDaylight(_appConfig.Latitude, _appConfig.Longitude) && ((DateTime.Now - Utils.riseTime).TotalMinutes < 20 || (DateTime.Now - Utils.setTime).TotalMinutes > 20))
                //{
                //    _logger.LogError("Outside of daylight period, cancelling run.");
                //    _cancellation.Cancel(true);
                //}

                //If there is a timeout just fall back to listening to inverter as it will always restart communications from that side

                listenToInverter = true;
                return null;
            }
        }
        bytes = bytes[0..i];

        return bytes;
    }

    private async Task ProcessBytes(byte[] buffer)
    {
        while (buffer?.Length > 0)
        {
            // Process the data sent by the client.
            var msg = Message.CreateFromByteBuffer(buffer, loggerFactory);
            //var msg = new Message(buffer);

            // When in IDENTIFY loop, keep listening to inverter
            inIdentifyLoop = false;
            if (msg.Type == MessageType.IDENTIFY && prevMsgType == MessageType.IDENTIFY)
            {
                inIdentifyLoop = true;
                listenToInverter = true;
            }
            else
            {
                inIdentifyLoop = false;
            }

            string source;
            if (listenToInverter)
                source = "INVERTER";
            else
                source = "SERVER";

            msg.Dump($"FROM {source}", config.ShowBytesInDump);

            Message reply = null;

            if (msg != null)
            {
                reply = await ProcessMessage(msg);
                buffer = msg.Remaining;

            }
            if (reply != null)
            {
                SendMessage(reply);
            }
            prevMsgType = msg.Type;
        }
    }

    private void SendMessage(Message msg)
    {
        Socket s;
        //string dest;
        bool sendMessage = true;

        if (listenToInverter)
        {
            s = serverSocket = ConnectSocket(serverSocket).Result;
            //dest = "SERVER";

            if (!inIdentifyLoop)
                listenToInverter = false;

            sendMessage = config.ActAsProxy;
        }
        else
        {
            s = inverterSocket = ConnectSocket(inverterSocket).Result;
            //dest = "INVERTER";

            listenToInverter = true;
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
        byte[] request = datalogger.Concat(new byte[] { 0x00, 0x04, 0x00, 0x15 }).ToArray();

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
        if (listenToInverter)
        {
            logger.LogInformation("==> Received ping from {id}", Display(data["datalogger"]));
            if (datalogger == null)
                datalogger = (byte[])data["datalogger"];
        }

        // Send Identify
        if (!config.ActAsProxy)
        {
            SendIdentify();
        }

        Message reply = Message.Create(MessageType.PING, (byte[])data["datalogger"], (ushort)data["id"]);
        return reply;
    }

    private Message ProcessIdentify(Message msg)
    {
        try
        {
            if (listenToInverter)
            {
                var cfg = msg.DecodeIdentifyDetail();
                Config.Add(cfg);

                logger.LogInformation("==> Inverter says: {display}", cfg.Display());

            }
            else
            {
                var idreqdata = msg.DecodeIdentifyRequest();
                if (firstIdentify)
                {
                    logger.LogInformation("==> Reveived request for config items {first} to {last}...", idreqdata["first"], idreqdata["last"]);
                    firstIdentify = false;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in processing identify: ");
            listenToInverter = true;
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

                logger.LogInformation("Received announcement from {make} - {inverter}({inverteralias})", Display(data["make"]), Display(data["inverter"]), Display(data["inverteralias"]));
                logger.LogInformation("Data logger: {datalogger}", Display(data["datalogger"]));
                logger.LogInformation("Version: {version}", Display(data["version"]));
                logger.LogInformation("Build: {build}", Display(data["build"]));

                var result = BitConverter.ToString((byte[])data["model"]).Replace("-", "").ToCharArray();

                string model = "A" + result[0] +
                               "B" + result[1] +
                               "D" + result[2] +
                               "T" + result[3] +
                               "P" + result[4] +
                               "U" + result[5] +
                               "M" + result[6] +
                               "S" + result[7];

                logger.LogInformation("Model {model}", model);

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
            logger.LogInformation("==> ANNOUNCE ACK received");
        }

        Message reply = Message.Create(msg.Type, msg.Body, msg.Id);
        return reply;
    }

    private async Task<Message> ProcessData(Message msg, Telegram telegram)
    {
        if (msg.IsAck)
        {
            logger.LogInformation("==> DATA ACK received ({type})", msg.Type);
        }
        else
        {
            if (listenToInverter)
            {
                await StoreTelegram(telegram);
            }

            if (telegram.InvStat == 0 && msg.Type == MessageType.CURRDATA)
            {
                cancellation.Cancel();
            }
        }

        Message reply = Message.Create(msg.Type, msg.Body, msg.Id);
        return reply;
    }

    private Message ProcessConfig(Message msg, DataloggerConfig cfg)
    {
        if (!msg.IsAck)
        {
            logger.LogInformation("==> Configuration received: {Display}", cfg.Display());
        }
        else
        {
            logger.LogInformation("==> Configuration ACK received");
        }
        Message reply = Message.Create(MessageType.CONFIG, msg.Body, msg.Id);
        return reply;
    }

    private async Task StoreTelegram(Telegram telegram)
    {
        if (telegram == null)
        {
            throw new ArgumentNullException(nameof(telegram));
        }

        telegram.Dump();

        // Create or reference an existing table
        string tablename = telegram.GetTablename(config.TablenamePrefix);
        if (prevTablename != tablename)
        {
            table = await storage.GetTableAsync(tablename);
            prevTablename = tablename;
        }
        try
        {
            var result = await table.UpsertEntityAsync(telegram);
            if (result.Status == (int)HttpStatusCode.NoContent)
                logger.LogInformation("Telegram {rowkey} stored in table {tablename}", telegram.RowKey, tablename);

            return;
        }
        catch (RequestFailedException)
        {
            listenToInverter = true;
            throw;
        }
    }

    private static string Display(object data)
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
