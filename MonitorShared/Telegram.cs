using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

namespace GrowattMonitorShared
{
    public class Telegram : TableEntity
    {

        private readonly ILogger<Telegram> _logger;

        public string Datalogger { get; set; }

        public string Inverter { get; set; }

        public DateTime InverterTimestamp { get; set; }

        public int InvStat { get; set; }
        public double Ppv { get; set; }
        public double Vpv1 { get; set; }
        public double Ipv1 { get; set; }
        public double Ppv1 { get; set; }
        public double Vpv2 { get; set; }
        public double Ipv2 { get; set; }
        public double Ppv2 { get; set; }
        public double Pac { get; set; }
        public double Fac { get; set; }
        public double VacR { get; set; }
        public double IacR { get; set; }
        public double PacR { get; set; }
        public double EacToday { get; set; }
        public double EacTotal { get; set; }
        public double TimeTotal { get; set; }
        public double Temp { get; set; }
        public double IPMTemp { get; set; }
        public double Pbusvolt { get; set; }
        public double Nbusvolt { get; set; }
        public double CheckStep { get; set; }
        public double Unknown1 { get; set; }
        public double ResetCheck  { get; set; }
        public double IPF { get; set; }
        public string DeratingMode { get; set; }
        public double Epv1Today { get; set; }
        public double Epv1Total { get; set; }
        public double Epv2Today { get; set; }
        public double Epv2Total { get; set; }
        public double EpvTotal { get; set; }
        public int RealOPPercent { get; set; }

        public Telegram(ILoggerFactory loggerFactory)
        {
            //var factory = (ILoggerFactory)new LoggerFactory();

            _logger = loggerFactory.CreateLogger<Telegram>();
        }

        public Telegram(byte[] buffer, ILoggerFactory factory) : this(factory)
        {
            
            Datalogger = Encoding.Default.GetString(buffer[8..18]);
            Inverter = Encoding.Default.GetString(buffer[18..28]);
            InverterTimestamp = new DateTime(2000+buffer[28], buffer[29], buffer[30], buffer[31], buffer[32], buffer[33]);

            SetEntityProperties();

            byte[] energy;

            energy = buffer[39..^0];
            var classType= typeof(Telegram);

            foreach (var d in GetDataList())
            {
                var value = d.GetFromBuffer(energy);

                try
                {
                    classType.GetProperty(d.Name).SetValue(this, value);
                }
                catch (System.Exception)
                {

                    throw;
                }

                energy = d.Remaining;
            }
        }

        public List<IInverterValue> GetDataList()
        {
              return new List<IInverterValue> {
                new InverterValueInt("InvStat", "", "Inverter run state"),
                new InverterValueDouble("Ppv", "W", 4, 1, "Input power"),
                new InverterValueDouble("Vpv1", "V", 2, 1, "PV1 voltage"),
                new InverterValueDouble("Ipv1", "A", 2, 1, "PV1 input current"),
                new InverterValueDouble("Ppv1", "W", 4, 1, "PV1 input power"),
                new InverterValueDouble("Vpv2", "V", 2, 1, "PV2 voltage"),
                new InverterValueDouble("Ipv2", "A", 2, 1, "PV2 input current"),
                new InverterValueDouble("Ppv2", "W", 4, 1, "PV2 input power"),
                new InverterValueDouble("Pac", "W", 4, 1, "Output power"),
                new InverterValueDouble("Fac", "Hz", 2, 2, "Grid frequency"),
                new InverterValueDouble("VacR", "V", 2, 1, "Single phase grid voltage"),
                new InverterValueDouble("IacR", "A", 2, 1, "Single phase grid output current"),
                new InverterValueDouble("PacR", "W", 4, 1, "Single phase grid output watt", 16),
                new InverterValueDouble("EacToday", "kWh", 4, 1, "Today generate energy"),
                new InverterValueDouble("EacTotal", "kWh", 4, 1, "Total generate energy"),
                new InverterValueDouble("TimeTotal", "s", 4, 0, "Work time total"),
                new InverterValueDouble("Temp", "&deg;C", 2, 1, "Inverter temperature", 16),
                new InverterValueDouble("IPMTemp", "&deg;C", 2, 1, "IPM in inverter temperature"),
                new InverterValueDouble("Pbusvolt", "V", 2, 1, "P Bus inside Voltage"),
                new InverterValueDouble("Nbusvolt", "V", 2, 1, "N Bus inside Voltage"),
                new InverterValueDouble("CheckStep","", 2, 0, "Product check step"),
                new InverterValueDouble("Unknown1", "", 2, 0),
                new InverterValueDouble("ResetCheck", "", 2, 0),
                new InverterValueDouble("IPF", "", 2, 0, "Inverter output PF"),
                new InverterValueString("DeratingMode", 2, "Derating mode"),
                new InverterValueDouble("Epv1Today", "kWh", 4, 1, "PV Energy today"),
                new InverterValueDouble("Epv1Total", "kWh", 4, 1, "PV Energy total"),
                new InverterValueDouble("Epv2Today", "kWh", 4, 1, "PV Energy today"),
                new InverterValueDouble("Epv2Total", "kWh", 4, 1, "PV Energy total"),
                new InverterValueDouble("EpvTotal", "kWh", 4, 1, "PV Energy total", 16),
                new InverterValueInt("RealOPPercent", "", "Operating percentage")
            };
        }

        public void SetEntityProperties()
        {
            PartitionKey = InverterTimestamp.ToString("dd");
            RowKey = InverterTimestamp.ToString("yyyyMMddHHmmss");
        }

        public void Dump()
        {
            if (this == null)
                return;

            _logger.LogDebug("==> Telegram data:");
            _logger.LogDebug("Datalogger: {Datalogger}", Datalogger);
            _logger.LogDebug("Inverter: {Inverter}", Inverter);
            _logger.LogDebug("Timestamp: {RowKey}", RowKey);

            var classType = typeof(Telegram);
            foreach (var item in GetDataList())
            {
                _logger.LogDebug("{name}: {value}", item.Name, classType.GetProperty(item.Name).GetValue(this));
            }
        }
    }
}
