using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

namespace GrowattMonitorShared
{
    public class Telegram
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        public string Key { get; set; }

        public string Datalogger { get; set; }

        public string Inverter { get; set; }

        public DateTime Timestamp { get; set; }

        public Dictionary<string, object> Data { get; private set; } = new Dictionary<string, object>();

        public Telegram()
        {

        }
        
        public Telegram(byte[] buffer)
        {
            Datalogger = Encoding.Default.GetString(buffer[8..18]);
            Inverter = Encoding.Default.GetString(buffer[18..28]);
            Timestamp = new DateTime(2000+buffer[28], buffer[29], buffer[30], buffer[31], buffer[32], buffer[33]);

            byte[] energy;

            energy = buffer[39..^0];

            foreach (var d in GetDataList())
            {
                var value = d.Item2.GetFromBuffer(energy);
                Data[d.Item1] = value;
                energy = d.Item2.Remaining;
            }

            SetCosmosDBProperties();
        }

        public (string, InverterValue)[] GetDataList()
        {
            return new (string, InverterValue)[] {
                ("InvStat", new InverterValue("InvStat", "", 2, 0, "Status")),
                ("Ppv", new InverterValue("Ppv", "W", 4, 1)),
                ("Vpv1", new InverterValue("Vpv1", "V", 2, 1)),
                ("Ipv1", new InverterValue("Ipv1", "A", 2, 1)),
                ("Ppv1", new InverterValue("Ppv1", "W", 4, 1)),
                ("Vpv2", new InverterValue("Vpv2", "V", 2, 1)),
                ("Ipv2", new InverterValue("Ipv2", "A", 2, 1)),
                ("Ppv2", new InverterValue("Ppv2", "W", 4, 1)),
                ("Pac", new InverterValue("Pac", "W", 4, 1)),
                ("Fac", new InverterValue("Fac", "Hz", 2, 2)),
                ("VacR", new InverterValue("VacR", "V", 2, 1)),
                ("IacR", new InverterValue("IacR", "A", 2, 1)),
                ("PacR", new InverterValue("PacR", "W", 4, 1)),
                ("VacS", new InverterValue("VacS", "V", 2, 1)),
                ("IacS", new InverterValue("IacS", "A", 2, 1)),
                ("PacS", new InverterValue("PacS", "W", 4, 1)),
                ("VacT", new InverterValue("VacT", "V", 2, 1)),
                ("IacT", new InverterValue("IacT", "A", 2, 1)),
                ("PacT", new InverterValue("PacT", "W", 4, 1)),
                ("EacToday", new InverterValue("EacToday", "kWh", 4, 1)),
                ("EacTotal", new InverterValue("EacTotal", "kWh", 4, 1)),
                ("Total", new InverterValue("Total", "s", 4, 1)),
                ("Temp", new InverterValue("Temp", "&deg;C", 2, 1)),
                ("ISOFault", new InverterValue("ISOFault", "V", 2, 1)),
                ("GFCIFault", new InverterValue("GFCIFault", "mA", 2, 1)),
                ("DCIFault", new InverterValue("DCIFault", "Hz", 2, 1)),
                ("VpvFault", new InverterValue("VpvFault", "V", 2, 1)),
                ("VacFault", new InverterValue("VacFault", "V", 2, 1)),
                ("FacFault", new InverterValue("FacFault", "", 2, 0)),
                ("TempFault", new InverterValue("TempFault", "&deg;C", 2, 1)),
                ("Faultcode", new InverterValue("Faultcode", "", 2, 0)),
                ("IPMTemp", new InverterValue("IPMtemp", "&deg;C", 2, 1)),
                ("Pbusvolt", new InverterValue("Pbusvolt", "V", 2, 1)),
                ("Nbusvolt", new InverterValue("Nbusvolt", "V", 2, 1)),
                ("Unknown1", new InverterValue("Unknown1", "", 4, 0)),
                ("Unknown2", new InverterValue("Unknown2", "", 4, 0)),
                ("Unknown3", new InverterValue("Unknown3", "", 4, 0)),
                ("Epv1Today", new InverterValue("Epv1Today", "kWh", 4, 1)),
                ("Epv1Total", new InverterValue("Epv1Total", "kWh", 4, 1)),
                ("Epv2Today", new InverterValue("Epv2Today", "kWh", 4, 1)),
                ("Epv2Total", new InverterValue("Epv2Total", "kWh", 4, 1)),
                ("EpvTotal", new InverterValue("EpvTotal", "kWh", 4, 1)),
                ("Rac", new InverterValue("Rac", "Var", 4, 1)),
                ("ERacToday", new InverterValue("RacToday", "Kvarh", 4, 1)),
                ("ERacTotal", new InverterValue("RacTotal", "Kvarh", 4, 1))
            };
        }

        private void SetCosmosDBProperties()
        {
            Id = Timestamp.ToString("yyyyMMddHHmmss");
            Key = Timestamp.ToString("yyyyMMdd");
        }

        public void Dump()
        {
            if (this == null)
                return;
            Console.WriteLine("==>");
            Console.WriteLine("Telegram data:");
            Console.WriteLine($"Datalogger: {Datalogger}");
            Console.WriteLine($"Inverter: {Inverter}");
            Console.WriteLine($"Timestamp: {Id}");
            foreach (var item in Data)
            {
                Console.WriteLine($"{item.Key} : {item.Value}");
            }
        }
    }
}
