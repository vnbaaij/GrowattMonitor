using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonitorUtils;

namespace GrowattMonitorShared
{
    public class Message
    {
        private static readonly bool showBytesInDump = false;

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

        public InverterMonitor Inverter { private get; set; }

        public ushort Version { get; private set; } = 5;

        public bool IsAck { get; private set; } = false;
        public bool InIdentifyProcess { get; private set; } = false;

        public static Message CreateFromByteBuffer(byte[] buffer)
        {
            var message = new Message();
            if (buffer.Length > 6)
            {
                var preamble = buffer[0..8];

                preamble.ReverseWhenLittleEndian();

                message.Type = (MessageType)BitConverter.ToUInt16(preamble, 0);

                // Size is initially without crc length (2 bytes) (older versions of protocol did not use crc)
                message.Size = BitConverter.ToUInt16(preamble, 2);
                message.Version = BitConverter.ToUInt16(preamble, 4);
                message.Id = BitConverter.ToUInt16(preamble, 6);

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
                    Console.WriteLine($"Invalid message, expected {message.Size}, got {buffer.Length - 5}");
                    message.Remaining = null;
                }
            }
            return message;

        }

        public static Message Create(ushort version, MessageType type, byte[] msg, ushort id = 1)
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
            else
            {
                message.Version = 2;
                message.Content = BitConverter.GetBytes(message.Id).ReverseWhenLittleEndian()
                    .Concat(BitConverter.GetBytes(message.Version).ReverseWhenLittleEndian())
                    .Concat(BitConverter.GetBytes(message.Size).ReverseWhenLittleEndian())
                    .Concat(BitConverter.GetBytes((ushort)message.Type).ReverseWhenLittleEndian())
                    .Concat(msg)
                    .ToArray();
            }
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
            var pre = Content[0..8];


            if (Version == 5)
            {
                Content = pre.Concat(Obfuscate(Content[Range.StartAt(8)], "Growatt")).ToArray();
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
                    Console.WriteLine($"Received unknown message type 0x{Type}");
                    break;
            }
            return null;
        }

        private Dictionary<string, object> DecodeAnnounce()
        {
            Dictionary<string, object> result;
            if (Version == 5)
            {
                //data = C/Cinit/C6pre/a10datalogger/a10inverter/C57jib1/a10iinverteralias/C66jib2/a16make/a9version/C*jib3
                result = new Dictionary<string, object>(6)
                {
                    { "id", Id },
                    { "datalogger", Content[8..18] },
                    { "inverter", Content[18..28] },
                    { "blob1", Content[28..57] },
                    { "build", Content[57..62] },
                    { "blob2", Content[62..85] },
                    { "inverteralias", Content[85..96] },
                    { "model", Content[95..99] },
                    { "blob3", Content[99..161] },
                    { "make", Content[161..177] },
                    { "version", Content[177..186] },
                    { "blob4", Content[186..^0] },
                };
            }
            else
            {
                //data = C/Cinit/C6pre/a10datalogger/a10ident/C58jib1/a10inverterserial/C62jib2/a20make/a8version/C*jib3
                result = new Dictionary<string, object>(6)
                {
                    { "id", Id },
                    { "datalogger", Content[8..18] },
                    { "inverter", Content[18..28] },
                    { "build", Content[57..62] },
                    { "inverteralias", Content[86..96] },
                    { "make", Content[158..178] },
                    { "type", Content[178..186] },
                    { "version", Content[177..186] },
                };

            }
            return result;
        }

        private Telegram DecodeData()
        {
            return new Telegram(Content); //, software);
        }

        private Dictionary<string, object> DecodePing()
        {
            Dictionary<string, object> result = new Dictionary<string, object>(2)
            {
                { "id", Id },
                { "datalogger", Content[8..^2] }

            };

            return result;
        }


        private Config DecodeConfig()
        {
            //data = C/Cinit/C6pre/a10serial/nconfigid/0

            var configid = "0x" + BitConverter.ToUInt16(Content[18..20].ReverseWhenLittleEndian()).ToString("X2");

            if (!IsAck)
            {
                ushort size = BitConverter.ToUInt16(Content[20..22].ReverseWhenLittleEndian());
                var value = Content[22..(22 + size)];

                return new Config(configid, value);
            }
            else
                return new Config(configid, 0);
        }

        public Config DecodeIdentifyDetail()
        {
            //data = C/Cinit/C6pre/a10serial/nconfigid/nsize
            ushort size = BitConverter.ToUInt16(Content[20..22].ReverseWhenLittleEndian());

            var configid = "0x" + BitConverter.ToUInt16(Content[18..20].ReverseWhenLittleEndian()).ToString("X2");
            int endindex = 22 + size;
            if (endindex > Content.Length)
                endindex = Content.Length;

            var value = Content[22..endindex];

            InIdentifyProcess = true;

            return new Config(configid, value); ;
        }

        public Dictionary<string, object> DecodeIdentifyRequest()
        {
            Dictionary<string, object> result = new Dictionary<string, object>(2)
            {
                { "datalogger", Content[8..18] },
                { "first", BitConverter.ToUInt16(Body[^4..^2].ReverseWhenLittleEndian()) },
                { "last", BitConverter.ToUInt16(Body[^2..^0].ReverseWhenLittleEndian()) }

            };

            InIdentifyProcess = true;

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
        /// <param name="infoType">Incoming or outgoing message</param>
        /// <param name="bytes">The byte array to display</param>
        /// <param name="hasCrc">Does the byte[] contain a CRC?</param>
        public void Dump(string infoType, bool hasCrc = true)
        {

            byte[] crc = new byte[] { 0, 0 };

            if (hasCrc)
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

            Console.WriteLine($"\nTimestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}, Length: {msgLength} ({msgLength:X2}), CRC: {crc[0]:X2} {crc[1]:X2}");
            Console.WriteLine($"Direction: {infoType}, Type: {Enum.GetName(typeof(MessageType), Type)} ");

            if (showBytesInDump)
            {
                Console.Write(lines.ToString());
            }
        }

        private StringBuilder GetLines(byte[] bytes)
        {
            int c = 0;
            string sbytes = "";
            StringBuilder lines = new StringBuilder();
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
}
