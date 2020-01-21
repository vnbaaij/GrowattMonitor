using System;
using System.Collections.Generic;
using System.Text;

namespace GrowattMonitorShared
{
    public class InverterValue
    {
        public string Name { get; set; }
        public int Bytes { get; set; }
        public string Unit { get; set; }
        public int Scale { get; set; }
        public string Description { get; set; }
        public double Value  { get; set; }

        public byte[] Remaining { get; private set; }

        public InverterValue(string name, string unit="", int bytes = 2, int scale = 1, string description = " Unknown")
        {
            Name = name;
            Unit = unit;
            Bytes = bytes;
            Scale = scale;
            Description = description;
        }

        public double GetFromBuffer(byte[] buffer)
        {
            if (buffer != null && buffer.Length > Bytes)
            {
                byte[] data = buffer[0..Bytes];

                Value = BitConverter.ToUInt16(data.ReverseWhenLittleEndian()) / Math.Pow(10, Scale);

                Remaining = buffer[Bytes..^0];
            }
            else Value = 0;

            return Value;
        }
    }
}
