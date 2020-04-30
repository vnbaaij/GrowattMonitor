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

        public (string, dynamic)[] GetDataList()
        {
              return new (string, dynamic)[] {
                ("InvStat", new InverterValueDouble("InvStat", "", 2, 0, "Inverter run state")),
                ("Ppv", new InverterValueDouble("Ppv", "W", 4, 1, "Input power")),
                ("Vpv1", new InverterValueDouble("Vpv1", "V", 2, 1, "PV1 voltage")),
                ("Ipv1", new InverterValueDouble("Ipv1", "A", 2, 1, "PV1 input current")),
                ("Ppv1", new InverterValueDouble("Ppv1", "W", 4, 1, "PV1 input power")),
                ("Vpv2", new InverterValueDouble("Vpv2", "V", 2, 1, "PV2 voltage")),
                ("Ipv2", new InverterValueDouble("Ipv2", "A", 2, 1, "PV2 input current")),
                ("Ppv2", new InverterValueDouble("Ppv2", "W", 4, 1, "PV2 input power")),
                ("Pac", new InverterValueDouble("Pac", "W", 4, 1, "Output power")),
                ("Fac", new InverterValueDouble("Fac", "Hz", 2, 2, "Grid frequency")),
                ("VacR", new InverterValueDouble("VacR", "V", 2, 1, "Single phase grid voltage")),
                ("IacR", new InverterValueDouble("IacR", "A", 2, 1, "Single phase grid output current")),
                ("PacR", new InverterValueDouble("PacR", "W", 4, 1, "Single phase grid output watt", 16)),
                //("VacS", new InverterValueDouble("VacS", "V", 2, 1, "Three phase grid voltage")),
                //("IacS", new InverterValueDouble("IacS", "A", 2, 1, "Three/single phase grid output current")),
                //("PacS", new InverterValueDouble("PacS", "W", 4, 1, "Three/single phase grid output watt")),
                //("VacT", new InverterValueDouble("VacT", "V", 2, 1, "Three phase grid voltage")),
                //("IacT", new InverterValueDouble("IacT", "A", 2, 1, "Three phase grid current")),
                //("PacT", new InverterValueDouble("PacT", "W", 4, 1, "Three phase grid power")),
                ("EacToday", new InverterValueDouble("EacToday", "kWh", 4, 1, "Today generate energy")),
                ("EacTotal", new InverterValueDouble("EacTotal", "kWh", 4, 1, "Total generate energy")),
                ("Total", new InverterValueDouble("Total", "s", 4, 0, "Work time total")),
                ("Temp", new InverterValueDouble("Temp", "&deg;C", 2, 1, "Inverter temperature", 16)),
                //("ISOFault", new InverterValueDouble("ISOFault", "V", 2, 1)),
                //("GFCIFault", new InverterValueDouble("GFCIFault", "mA", 2, 1)),
                //("DCIFault", new InverterValueDouble("DCIFault", "Hz", 2, 2)),
                //("VpvFault", new InverterValueDouble("VpvFault", "V", 2, 1)),
                //("VacFault", new InverterValueDouble("VacFault", "V", 2, 1)),
                //("FacFault", new InverterValueDouble("FacFault", "", 2, 2)),
                //("TempFault", new InverterValueDouble("TempFault", "&deg;C", 2, 1)),
                //("Faultcode", new InverterValueDouble("Faultcode", "", 2, 0)),
                ("IPMTemp", new InverterValueDouble("IPMtemp", "&deg;C", 2, 1, "IPM in inverter temperature")),
                ("Pbusvolt", new InverterValueDouble("Pbusvolt", "V", 2, 1, "P Bus inside Voltage")),
                ("Nbusvolt", new InverterValueDouble("Nbusvolt", "V", 2, 1, "N Bus inside Voltage")),
                ("CheckStep", new InverterValueDouble("CheckStep","", 2, 0, "Product check step")),
                ("Unknown", new InverterValueDouble("Unknown", "", 2, 0)),
                ("ResetCheck", new InverterValueDouble("REsetCheck", "", 2, 0)),
                ("IPF", new InverterValueDouble("IPF", "", 2, 0, "Inverter output PF")),
                ("DeratingMode", new InverterValueString("DeratingMode", 2, "Derating mode")),
                ("Epv1Today", new InverterValueDouble("Epv1Today", "kWh", 4, 1, "PV Energy today")),
                ("Epv1Total", new InverterValueDouble("Epv1Total", "kWh", 4, 1, "PV Energy total")),
                ("Epv2Today", new InverterValueDouble("Epv2Today", "kWh", 4, 1, "PV Energy today")),
                ("Epv2Total", new InverterValueDouble("Epv2Total", "kWh", 4, 1, "PV Energy total")),
                ("EpvTotal", new InverterValueDouble("EpvTotal", "kWh", 4, 1, "PV Energy total", 16)),
                //("Rac", new InverterValueDouble("Rac", "Var", 4, 1, "AC Reactive power")),
                //("ERacToday", new InverterValueDouble("RacToday", "Kvarh", 4, 1, "AC Reactive energy today")),
                //("ERacTotal", new InverterValueDouble("RacTotal", "Kvarh", 4, 1, "AC Reactive energy total")),
                //("WarningCode", new InverterValueDouble("WarningCode", "", 2, 0)),
                //("WarningValue1", new InverterValueDouble("WarningValue1", "", 2, 0)),
                ("RealOPPercent", new InverterValueDouble("RealOPPercent", "", 2, 0, "Operating percentage")), // Uncomment if unknowns need to be included in Data, 46)),
                //("OPFullWatt", new InverterValueDouble("OPFullWatt", "W", 4, 1)),
                //("WarningValue2", new InverterValueDouble("WarningValue2", "", 2, 0)),
                //("V_String1", new InverterValueDouble("V_String1", "V", 2, 1)),
                //("Curr_String1", new InverterValueDouble("Curr_String1", "A", 2, 1)),
                //("V_String2", new InverterValueDouble("V_String2", "V", 2, 1)),
                //("Curr_String2", new InverterValueDouble("Curr_String2", "A", 2, 1)),
                //("V_String3", new InverterValueDouble("V_String3", "V", 2, 1)),
                //("Curr_String3", new InverterValueDouble("Curr_String3", "A", 2, 1)),
                //("V_String4", new InverterValueDouble("V_String4", "V", 2, 1)),
                //("Curr_String4", new InverterValueDouble("Curr_String4", "A", 2, 1)),
                //("V_String5", new InverterValueDouble("V_String5", "V", 2, 1)),
                //("Curr_String5", new InverterValueDouble("Curr_String5", "A", 2, 1)),
                //("V_String6", new InverterValueDouble("V_String6", "V", 2, 1)),
                //("Curr_String6", new InverterValueDouble("Curr_String6", "A", 2, 1)),
                //("V_String7", new InverterValueDouble("V_String7", "V", 2, 1)),
                //("Curr_String7", new InverterValueDouble("Curr_String7", "A", 2, 1)),
                //("V_String8", new InverterValueDouble("V_String8", "V", 2, 1)),
                //("Curr_String8", new InverterValueDouble("Curr_String8", "A", 2, 1)),
                //("Str_Fault", new InverterValueDouble("Str_Fault", "", 2, 0)),
                //("Str_Warning", new InverterValueDouble("Str_Warning", "", 2, 0)),
                //("Str_Break", new InverterValueDouble("Str_Break", "", 2, 0)),
                //("PIDFaultCode", new InverterValueDouble("PIDFaultCode", "", 2, 0)),
                //("UnknownA", new InverterValueDouble("UnknownA", "", 2, 0)),
                //("UnknownB", new InverterValueDouble("UnknownB", "", 2, 0)),
                //("UnknownC", new InverterValueDouble("UnknownC", "", 2, 0)),
                //("UnknownD", new InverterValueDouble("UnknownD", "", 2, 0)),
                //("UnknownE", new InverterValueDouble("UnknownE", "", 2, 0)),
                //("UnknownF", new InverterValueDouble("UnknownF", "", 2, 0)),
                //("UnknownG", new InverterValueDouble("UnknownG", "", 2, 0)),
                //("UnknownH", new InverterValueDouble("UnknownH", "", 2, 0)),
                //("UnknownI", new InverterValueDouble("UnknownI", "", 2, 0)),
                //("UnknownJ", new InverterValueDouble("UnknownJ", "", 2, 0)),
                //("UnknownK", new InverterValueDouble("UnknownK", "", 2, 0)),
                //("UnknownL", new InverterValueDouble("UnknownL", "", 2, 0)),
                //("UnknownM", new InverterValueDouble("UnknownM", "", 2, 0)),
                //("UnknownN", new InverterValueDouble("UnknownN", "", 2, 0)),
                //("UnknownO", new InverterValueDouble("UnknownO", "", 2, 0)),
                //("UnknownP", new InverterValueDouble("UnknownP", "", 2, 0)),
                //("UnknownQ", new InverterValueDouble("UnknownQ", "", 2, 0)),
                //("UnknownR", new InverterValueDouble("UnknownR", "", 2, 0)),
                //("UnknownS", new InverterValueDouble("UnknownS", "", 2, 0)),
                //("UnknownT", new InverterValueDouble("UnknownT", "", 2, 0)),
                //("UnknownU", new InverterValueDouble("UnknownU", "", 2, 0)),
                //("UnknownV", new InverterValueDouble("UnknownV", "", 2, 0)),
                //("UnknownW", new InverterValueDouble("UnknownW", "", 2, 0)),
                //("UnknownX", new InverterValueDouble("UnknownX", "", 2, 0)),
                //("UnknownY", new InverterValueDouble("UnknownY", "", 2, 0))
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
