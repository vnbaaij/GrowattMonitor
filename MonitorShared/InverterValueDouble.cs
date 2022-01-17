using System;
using System.Collections.Generic;
using System.Text;

namespace GrowattMonitorShared
{
    
    public class InverterValueDouble : IInverterValue<double>
    {
        
        public string Unit { get; set; }
        public int Scale { get; set; }
        public int Skip { get; set; }
        public double Value { get; set; }
        public string Name { get; set; }
        public int Length { get; set; }
        public string Description { get; set; }
        public byte[] Remaining { get; set; }

        public InverterValueDouble(string name, string unit="", int bytes = 2, int scale = 1, string description = "Unknown", int skip = 0)
        {
            Name = name;
            Unit = unit;
            Length = bytes;
            Scale = scale;
            Description = description;
            Skip = skip;
        }

        public double GetFromBuffer(byte[] buffer)
        {
            if (buffer != null && buffer.Length > Length)
            {
                byte[] data = buffer[0..Length];

                if (Length == 2)

                    Value = BitConverter.ToUInt16(data.ReverseWhenLittleEndian()) / Math.Pow(10, Scale);
                if (Length == 4)
                    Value = BitConverter.ToUInt32(data.ReverseWhenLittleEndian()) / Math.Pow(10, Scale);

                //amount of half seconds, converted to hours
                if (Unit == "s")
                    Value /= (2 * 60 * 60);

                Remaining = buffer[(Length + Skip)..^0];
            }
            else Value = 0;

            return Value;
        }

        object IInverterValue.GetFromBuffer(byte[] buffer)
        {
            return GetFromBuffer(buffer);
        }
    }
}
