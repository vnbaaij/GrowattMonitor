using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GrowattMonitorShared
{
    public class Config
    {
        public string Index { get; set; }
        public object Value { get; set; }
        public string Name
        {
            get
            {
                return LookupName();
            }
        }

        public Config(string index, object value)
        {
            Index = index;
            Value = value;
        }

        public string Display()
        {
            return LookupDescription() +"(" + Index + "): " + Encoding.Default.GetString((byte[])Value);
        }

        private string LookupDescription()
        {
            var config = new Dictionary<string, string>
            {
                {"0x04", "Update Interval" },
                {"0x05", "Modbus Range low"},
                {"0x06", "Modbus Range high"},
                {"0x07", "Modbus Address"},
                {"0x08", "Serial Number"},
                {"0x09", "Hardware Version"},
                {"0x0A", "?"},
                {"0x0B", "FTP credentials"},
                {"0x0C", "DNS"},
                {"0x0D", "?"},
                {"0x0E", "Local IP"},
                {"0x0F", "Local Port"},
                {"0x10", "Mac Address"},
                {"0x11", "Server IP"},
                {"0x12", "Server Port"},
                {"0x13", "Server"},
                {"0x14", "Device Type"},
                {"0x15", "Software Version"},
                {"0x16", "Hardware Version"},
                {"0x1E", "Timezone"},
                {"0x1F", "Date"}
            };

            if (config[Index] != null)
            {
                return config[Index];
            }
            else
            {
                return "Unknown (0x" + Index + ")";
            };
        }

        private string LookupName()
        {

            var config = new Dictionary<string, string>
            {
                {"0x04", "interval"},
                {"0x05", "range1"},
                {"0x06", "range2"},
                {"0x07", "dataloggerid"},
                {"0x08", "serial"},
                {"0x09", "hwversion"},
                {"0x0A", "?"},
                {"0x0B", "ftpcred"},
                {"0x0C", "dns"},
                {"0x0D", "?"},
                {"0x0E", "localip"},
                {"0x10", "mac"},
                {"0x0F", "Localport"},
                {"0x11", "serverip"},
                {"0x12", "serverport"},
                {"0x13", "server"},
                {"0x14", "type"},
                {"0x15", "swversion"},
                {"0x16", "version"},
                {"0x1E", "timezone"},
                {"0x1F", "date"}
            };
            if (config[Index] != null)
            {
                return config[Index];
            }
            else
            {
                return "0x" + Index + "_unknown";
            }
        }

        public static List<Config> CreateFromTLV(byte[] msg)
        {
            var cfg = new List<Config>();

            
            //$data = unpack("C4/nsize/C4type/a10serial/a10ident/a10ident2/C*", $msg);
            

            // cut off header
            msg = msg[8..^0];

            while (msg.Length > 2)
            {
                //$data = unpack("ntype/nlength/a*value", $msg);
                string configid = "0x" + BitConverter.ToUInt16(msg[0..2].ReverseWhenLittleEndian()).ToString("X2");
                int length = BitConverter.ToInt16(msg[2..4].ReverseWhenLittleEndian());
                var value = msg[4..length];

                cfg.Add(new Config(configid, value));

                msg = msg[(length+4)..^0];
            }
            return cfg;
        }
    }
}
