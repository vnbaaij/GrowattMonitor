using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GrowattMonitorShared
{
    public class Telegram
    {
        public Dictionary<string, object> Data { get; private set; } = new Dictionary<string, object>();

        public Telegram(byte[] buffer) //, string software = "1.0.0.0")
        {
            Data["datalogger"] = Encoding.Default.GetString(buffer[8..18]);
            Data["inverter"] = Encoding.Default.GetString(buffer[18..28]);
            Data["datetime"] = DateTime.Now;

            byte[] energy;

            //if (software.StartsWith("1"))
                energy = buffer[28..^0];
            //else
            //    energy = buffer[28..33];

            
            foreach (var d in GetDataList())
            {
                var value = d.Item2.GetFromBuffer(energy);
                Data[d.Item1] = value;
                energy = d.Item2.Remaining;
            }
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
                ("Vac1", new InverterValue("VacR", "V", 2, 1)),
                ("Iac1", new InverterValue("IacR", "A", 2, 1)),
                ("Pac1", new InverterValue("PacR", "W", 4, 1)),
                ("Vac2", new InverterValue("VacS", "V", 2, 1)),
                ("Iac2", new InverterValue("IacS", "A", 2, 1)),
                ("Pac2", new InverterValue("PacS", "W", 4, 1)),
                ("Vac3", new InverterValue("VacT", "V", 2, 1)),
                ("Iac3", new InverterValue("IacT", "A", 2, 1)),
                ("Pac3", new InverterValue("PacT", "W", 4, 1)),
                ("EacToday", new InverterValue("EacToday", "kWh", 4, 1)),
                ("EacTotal", new InverterValue("EacTotal", "kWh", 4, 1)),
                ("Tall", new InverterValue("Total", "s", 4, 1)),
                ("Temp", new InverterValue("Temp", "&deg;C", 2, 1)),
                ("ISOFault", new InverterValue("ISOFault", "V", 2, 1)),
                ("GFCIFault", new InverterValue("GFCIFault", "mA", 2, 1)),
                ("VpvFault", new InverterValue("VpvFault", "V", 2, 1)),
                ("VacFault", new InverterValue("VacFault", "V", 2, 1)),
                ("FacFault", new InverterValue("DCIFault", "Hz", 2, 1)),
                ("TempFault", new InverterValue("TempFault", "&deg;C", 2, 1)),
                ("Unknown0", new InverterValue("Unknown0", "", 2, 0)),
                ("Faultcode", new InverterValue("Faultcode", "", 2, 0)),
                ("IPMTemp", new InverterValue("IPMtemp", "&deg;C", 2, 1)),
                ("Pbusvolt", new InverterValue("Pbusvolt", "V", 2, 1)),
                ("Nbusvolt", new InverterValue("Nbusvolt", "V", 2, 1)),
                ("Unknown1", new InverterValue("Unknown1", "", 4, 0)),
                ("Unknown2", new InverterValue("Unknown2", "", 4, 0)),
                ("Unknown3", new InverterValue("Unknown3", "", 4, 0)),
                ("Epv1Today", new InverterValue("Epv1today", "kWh", 4, 1)),
                ("Epv1Total", new InverterValue("Epv1total", "kWh", 4, 1)),
                ("Epv2Today", new InverterValue("Epv2today", "kWh", 4, 1)),
                ("Epv2Total", new InverterValue("Epv2total", "kWh", 4, 1)),
                ("EpvTotal", new InverterValue("Epvtotal", "kWh", 4, 1)),
                ("Rac", new InverterValue("Rac", "Var", 4, 1)),
                ("ERactoday", new InverterValue("RacToday", "Kvarh", 4, 1)),
                ("ERactotal", new InverterValue("RacTotal", "Kvarh", 4, 1))
            };
        }

    }
}
