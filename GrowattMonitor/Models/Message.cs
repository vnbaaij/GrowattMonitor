using System.Text;
using GrowattMonitor.Helpers;
using MonitorUtils;

namespace GrowattMonitor.Models;

public class Message
{
    private static ILoggerFactory _loggerFactory;
    private static ILogger<Message> _logger;

    static Message()
    {
        //var factory = (ILoggerFactory)new LoggerFactory();
        if (_loggerFactory != null)
            _logger = _loggerFactory.CreateLogger<Message>();
    }

    public byte[] Content { get; private set; }

    public byte[] Body { get; private set; }

    private byte[] _crc;
    public byte[] Crc
    {
        get
        {
            return _crc;
        }
        private set
        {
            var crc = new Crc16(Crc16Mode.IBM_REVERSED);
            _crc = crc.ComputeChecksumBytes(value, 0xffff);
            _crc.ReverseWhenLittleEndian();
        }
    }

    public byte[] Remaining { get; set; }

    public ushort Id { get; set; } = 1;

    public ushort Size { get; private set; } = 0;

    public MessageType Type { get; private set; }

    public ushort Version { get; private set; } = 5;

    public bool IsAck { get; private set; } = false;

    public static Message CreateFromByteBuffer(byte[] buffer, ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        if (_logger == null)
            _logger = _loggerFactory.CreateLogger<Message>();

        var message = new Message();

        if (buffer.Length > 6)
        {
            var header = buffer[0..8];

            header.ReverseWhenLittleEndian();

            message.Type = (MessageType)BitConverter.ToUInt16(header, 0);
            message.Size = BitConverter.ToUInt16(header, 2); // Size is initially without crc length (2 bytes) (older versions of protocol did not use crc)
            message.Version = BitConverter.ToUInt16(header, 4);
            message.Id = BitConverter.ToUInt16(header, 6);

            // payload starts after header (8 bytes) and ends at header+size-2
            message.Body = Obfuscate(buffer[8..(message.Size + 6)], "Growatt");

            if (message.Version == 5)
            {
                // In this version include crc in size
                message.Size += 2;
            }

            if (buffer.Length - 5 >= message.Size)
            {
                message.Content = buffer[0..(message.Size + 6)];
                message.Remaining = buffer[(message.Size + 6)..^0];
                return message;
            }
            else
            {
                _logger.LogError("Invalid message, expected {Size}, got {Length}", message.Size, buffer.Length - 5);
                message.Remaining = null;
            }
        }
        return message;

    }

    public static Message Create(MessageType type, byte[] msg, ushort id = 1, ushort version = 5)
    {
        var message = new Message
        {
            Id = id,
            Type = type,
            Size = (ushort)(msg.Length + 2),
            Body = msg
        };

        if (version == 5)
        {
            message.Version = 5;

            msg = Obfuscate(msg, "Growatt");

            byte[] content = BitConverter.GetBytes(message.Id).ReverseWhenLittleEndian()
                .Concat(BitConverter.GetBytes(message.Version).ReverseWhenLittleEndian())
                .Concat(BitConverter.GetBytes(message.Size).ReverseWhenLittleEndian())
                .Concat(BitConverter.GetBytes((ushort)message.Type).ReverseWhenLittleEndian())
                .Concat(msg)
                .ToArray();

            message.Crc = content;
            message.Content = content.Concat(message.Crc).ToArray();
        }
        //else
        //{
        //    message.Version = 2;
        //    message.Content = BitConverter.GetBytes(message.Id).ReverseWhenLittleEndian()
        //        .Concat(BitConverter.GetBytes(message.Version).ReverseWhenLittleEndian())
        //        .Concat(BitConverter.GetBytes(message.Size).ReverseWhenLittleEndian())
        //        .Concat(BitConverter.GetBytes((ushort)message.Type).ReverseWhenLittleEndian())
        //        .Concat(msg)
        //        .ToArray();
        //}
        return message;
    }

    private static byte[] Obfuscate(byte[] msg, string v)
    {
        var s = Pad(v, msg.Length);

        byte[] result = new byte[msg.Length];
        for (int i = 0; i < msg.Length; i++)
        {
            result[i] = (byte)(msg[i] ^ s[i]);
        }
        return result;
    }

    public object Decode()
    {
        var header = Content[0..8];


        if (Version == 5)
        {
            Content = header.Concat(Obfuscate(Content[8..^0], "Growatt")).ToArray();
        }

        switch (Type)
        {
            case MessageType.ANNOUNCE:
                if (Body[0] == 0 && Body.Length == 1)
                    IsAck = true;
                else
                    return DecodeAnnounce();
                break;

            case MessageType.CURRDATA:
            case MessageType.HISTDATA:
                if (Body[0] == 0 && Body.Length == 1)
                {
                    IsAck = true;
                    break;
                }
                else
                    return DecodeData();
            //break;
            case MessageType.PING:
                return DecodePing();
            case MessageType.CONFIG:
                if (Body[12] == 0 && Body.Length == 13)
                    IsAck = true;
                return DecodeConfig();
            //break;
            case MessageType.IDENTIFY:
                break;
            case MessageType.REBOOT:
                break;
            case MessageType.CONFACK:
            //return DecodeConfAck();
            default:
                _logger.LogError("Received unknown message type 0x{Type}", Type);
                break;
        }
        return null;
    }

    private Dictionary<string, object> DecodeAnnounce()
    {
        Dictionary<string, object> result = new(12);
        if (Version == 5)
        {
            //2id/2version/2length/2type/10datalogger/10inverter/29jib1/5build/22jib2/10inverteralias/5model/62jib3/16make/9version/*jib3
            result = new Dictionary<string, object>
            {
                { "id", Id },
                { "datalogger", Content[8..18] },
                { "inverter", Content[18..28] },
                { "blob1", Content[28..57] },
                { "build", Content[57..62] },
                { "blob2", Content[62..85] },
                { "inverteralias", Content[85..95] },
                { "model", Content[95..99] },
                { "blob3", Content[99..161] },
                { "make", Content[161..177] },
                { "version", Content[177..186] },
                { "blob4", Content[186..^0] },
            };
        }
        // other version has not been researched (at all)
        //else
        //{
        //    //data = C/Cinit/C6pre/a10datalogger/a10ident/C58jib1/a10inverterserial/C62jib2/a20make/a8version/C*jib3
        //    result = new Dictionary<string, object>(6)
        //    {
        //        { "id", Id },
        //        { "datalogger", Content[8..18] },
        //        { "inverter", Content[18..28] },
        //        { "build", Content[57..62] },
        //        { "inverteralias", Content[86..96] },
        //        { "make", Content[158..178] },
        //        { "type", Content[178..186] },
        //        { "version", Content[177..186] },
        //    };

        //}
        return result;
    }

    private Telegram DecodeData()
    {
        return new Telegram(Content, _loggerFactory);
    }

    private Dictionary<string, object> DecodePing()
    {
        Dictionary<string, object> result = new(2)
        {
            { "id", Id },
            { "datalogger", Content[8..^2] }

        };

        return result;
    }


    private DataloggerConfig DecodeConfig()
    {
        //2id/2version/2length/2type/10datalogger/nconfigid/0

        var configid = "0x" + BitConverter.ToUInt16(Content[18..20].ReverseWhenLittleEndian()).ToString("X2");

        if (!IsAck)
        {
            ushort size = BitConverter.ToUInt16(Content[20..22].ReverseWhenLittleEndian());
            var value = Content[22..(22 + size)];

            return new DataloggerConfig(configid, value);
        }
        else
            return new DataloggerConfig(configid, 0);
    }

    public DataloggerConfig DecodeIdentifyDetail()
    {
        //2id/2version/2length/2type/nconfigid/nsize
        ushort size = BitConverter.ToUInt16(Body[12..14].ReverseWhenLittleEndian());

        var configid = "0x" + BitConverter.ToUInt16(Body[10..12].ReverseWhenLittleEndian()).ToString("X2");
        int endindex = 14 + size;
        if (endindex > Body.Length)
            endindex = Body.Length;

        var value = Body[14..endindex];

        return new DataloggerConfig(configid, value); ;
    }

    public Dictionary<string, object> DecodeIdentifyRequest()
    {
        //2id/2version/2length/2type/10datalogger/2first/2last
        Dictionary<string, object> result = new(3)
        {
            { "datalogger", Content[8..18] },
            { "first", BitConverter.ToUInt16(Body[^4..^2].ReverseWhenLittleEndian()) },
            { "last", BitConverter.ToUInt16(Body[^2..^0].ReverseWhenLittleEndian()) }

        };

        return result;
    }

    public static byte[] Pad(string pad, int length)
    {
        int i = (int)Math.Ceiling(length / (decimal)pad.Length);
        var k = new StringBuilder(i * pad.Length);

        for (int c = 0; c < i; c++)
        {
            k.Append(pad);
        }

        var result = Encoding.Default.GetBytes(k.ToString())[0..length];

        return result;
    }

    /// <summary>
    /// Produces output similar to what is used in Growatt WiFi protocol description
    /// https://www.vromans.org/software/sw_growatt_wifi_protocol.html
    /// </summary>
    /// <param name="info">Origin and direction of message</param>
    /// <param name="showBytesInDump">Output the byte array contents</param>
    /// <param name="processCrc">Does the byte[] contain a CRC to process?</param>
    public void Dump(string info, bool showBytesInDump = false, bool processCrc = true)
    {

        byte[] crc = new byte[] { 0, 0 };

        if (processCrc)
            crc = Content[^2..^0];

        StringBuilder lines = null;
        if (showBytesInDump)
        {
            lines = GetLines(Content);
            lines.AppendLine("PAYLOAD");
            lines.Append(GetLines(Body));
        }

        // Header = 8 bytes, CRC = 2 bytes, Msg length = # of bytes - header (includes CRC)
        int msgLength = Content.Length - 8;
        _logger.LogDebug("Message: {time}, {info}: {type} ({length})", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), info, Enum.GetName(typeof(MessageType), Type), msgLength);

        if (showBytesInDump)
        {
            _logger.LogDebug("CRC: {crc0:X2} {crc1:X2}\n", crc[0], crc[1]);
            _logger.LogDebug("{lines}", lines.ToString());
        }
        else
        {
            _logger.LogDebug("\n");
        }
    }

    private static StringBuilder GetLines(byte[] bytes)
    {
        int c = 0;
        string sbytes = "";
        StringBuilder lines = new();
        // Start with line counter
        string hex = c.ToString("D4") + ": ";
        foreach (byte b in bytes)
        {
            c++;

            // Get hex representation of current byte
            hex += (b).ToString("X2") + " ";

            // Get char representation of current byte (or '.' )
            if (b >= 32)
                sbytes += Convert.ToChar(b);
            else
                sbytes += ".";

            // 16 bytes/chars per line
            if ((c % 16) == 0)
            {
                lines.AppendLine(hex + sbytes);
                // Reset with current line count
                hex = (c / 16).ToString("D4") + ": "; ;
                sbytes = "";
            }
        }
        // Fill up line if less than 16 bytes
        hex = hex.PadRight(54, ' ');
        lines.AppendLine(hex + sbytes);
        return lines;
    }
}
