using System.Net;
using System.Net.Sockets;
using System.Text;
using Azure;
using Azure.Data.Tables;
using GrowattMonitor.Configuration;
using GrowattMonitor.Models;
using Microsoft.Extensions.Options;


namespace GrowattMonitor;

public class InverterMonitor
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<InverterMonitor> _logger;
    private readonly AppConfig _appConfig;
    private readonly StorageTableHelper _storageTableHelper;
    //private CloudTable _table;
    private TableClient? _table;

    private static Socket _inverterSocket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    private static Socket _serverSocket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    private static CancellationTokenSource _cancellation = new();

    private byte[] _datalogger = Array.Empty<byte>();
    private bool _listenToInverter = true;
    private static bool _firstIdentify = true;
    private static bool _inIdentifyLoop = false;

    private MessageType prevMsgType;
    private static string? prevTablename;

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
        TcpListener listener = new (ip, _appConfig.DataloggerReceiverPort);
        listener.Start();

        _cancellation = new CancellationTokenSource();
        _logger.LogInformation("Monitor started...");

        // Enter the listening loop.
        while (Utils.IsDaylight(_appConfig.Latitude, _appConfig.Longitude) && !_cancellation.Token.IsCancellationRequested)
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
                _logger.LogWarning("Monitor canceled!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in monitor while running: ");
                _cancellation.Cancel(false);
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

    private async Task<Socket> ConnectSocket(Socket socket, TcpListener? listener = null)
    {
        if (socket != null && socket.Connected)
            // Everything is awesome!
            return socket;

        if (listener == null && !_appConfig.ActAsProxy)
            return new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        string host;
        int port;
        if (listener != null)
        {
            host = _appConfig.DataloggerReceiverAddress;
            port = _appConfig.DataloggerReceiverPort;
            _logger.LogInformation("==> Waiting for inverter to connect to proxy ({host}:{port})... ", host, port);
        }
        else
        {
            host = _appConfig.GrowattServerAddress;
            port = _appConfig.GrowattServerPort;
            _logger.LogInformation("==> Waiting for proxy to connect to server ({host}:{port})... ", host, port);
        }

        try
        {
            _cancellation?.Token.ThrowIfCancellationRequested();

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
                    socket = await listener.AcceptSocketAsync(_cancellation!.Token);
                }
                else
                    await socket.ConnectAsync(host, port);

            _logger.LogInformation("...connection established");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in monitor when connecting socket: ");
            _listenToInverter = true;
        }

        return socket!;
    }

    private async Task<byte[]> ReceiveBytes(TcpListener listener)
    {
        int i = 0;

        _cancellation?.Token.ThrowIfCancellationRequested();

        if (_inverterSocket == null || !_inverterSocket.Connected)
            _inverterSocket = await ConnectSocket(_inverterSocket!, listener);
        if (_serverSocket == null || !_serverSocket.Connected)
            _serverSocket = await ConnectSocket(_serverSocket!);

        byte[] bytes = new byte[4096];

        try
        {
            if (_listenToInverter)
                i = _inverterSocket!.Receive(bytes, SocketFlags.None);
            else
                i = _serverSocket!.Receive(bytes, SocketFlags.None);
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
                //_logger.LogError(e, "Error when receiving bytes: ");
                //if (Utils.IsDaylight(_appConfig.Latitude, _appConfig.Longitude) && ((DateTime.Now - Utils.riseTime).TotalMinutes < 20 || (DateTime.Now - Utils.setTime).TotalMinutes > 20))
                //{
                //    _logger.LogError("Outside of daylight period, cancelling run.");
                //    _cancellation.Cancel(true);
                //}

                //If there is a timeout just fall back to listening to inverter as it will always restart comminications from that side

                _listenToInverter = true;
                return Array.Empty<byte>(); 
            }
        }
        bytes = bytes[0..i];

        return bytes;
    }

    private async Task ProcessBytes(byte[]? buffer)
    {
        while (buffer?.Length > 0)
        {
            // Process the data sent by the client.
            var msg = Message.CreateFromByteBuffer(buffer, _loggerFactory);

            // When in IDENTIFY loop, keep listening to inverter
            _inIdentifyLoop = false;
            if (msg.Type == MessageType.IDENTIFY && prevMsgType == MessageType.IDENTIFY)
            {
                _inIdentifyLoop = true;
                _listenToInverter = true;
            }
            else
            {
                _inIdentifyLoop = false;
            }

            string source;
            if (_listenToInverter)
                source = "INVERTER";
            else
                source = "SERVER";

            msg.Dump($"FROM {source}", _appConfig.ShowBytesInDump);

            Message? reply = null;

            if (msg != null)
            {
                reply = await ProcessMessage(msg);
                buffer = msg.Remaining;
                prevMsgType = msg.Type;

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
        //string dest;
        bool sendMessage = true;

        if (_listenToInverter )
        {
            s = _serverSocket = ConnectSocket(_serverSocket).Result;
            //dest = "SERVER";

            if (!_inIdentifyLoop)
                _listenToInverter = false;

            sendMessage = _appConfig.ActAsProxy;
        }
        else
        {
            s = _inverterSocket = ConnectSocket(_inverterSocket).Result;
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

    private async Task<Message?> ProcessMessage(Message msg)
    {
        var data = msg.Decode();
        if (data == null)
            return null;

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
        try
        {
            if (_listenToInverter)
            {
                var cfg = msg.DecodeIdentifyDetail();
                Config.Add(cfg);

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
            _logger.LogError(ex, "Error in processing identify: ");
            _listenToInverter = true;
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

                _logger.LogInformation("Received announcement from {make} - {inverter}({inverteralias})", Display(data["make"]), Display(data["inverter"]), Display(data["inverteralias"]));
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

                _logger.LogInformation("Model {model}", model);

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
            throw new ArgumentNullException(nameof(telegram));
        }

        telegram.Dump();

        // Create or reference an existing table
        string? tablename = telegram.GetTablename(_appConfig.TablenamePrefix);
        if (prevTablename != tablename && tablename != null)
        {
            _table = await _storageTableHelper.GetTableAsync(tablename);
            prevTablename = tablename;
        }
        try
        {
            if (_table != null)
            {
                var result = await _table.UpsertEntityAsync(telegram);
                if (result.Status == (int)HttpStatusCode.NoContent)
                    _logger.LogInformation("Telegram {rowkey} stored in table {tablename}", telegram.RowKey, tablename);
            }
            return;
        }
        catch (RequestFailedException)
        {
            _listenToInverter = true;
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
