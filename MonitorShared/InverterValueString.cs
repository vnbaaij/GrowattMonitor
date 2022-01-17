using System;
using System.Collections.Generic;
using System.Text;

namespace GrowattMonitorShared
{
    public class InverterValueString : IInverterValue<string>
    {
        public string Name { get; set; }
        public int Length { get; set; }
        public string Description { get; set; }
        public string Value  { get; set; }

        public byte[] Remaining { get; set; }

        public InverterValueString(string name, int length = 2, string description = "Unknown")
        {
            Name = name;
            Length = length * 2;
            Description = description;
        }

        public string GetFromBuffer(byte[] buffer)
        {
            if (buffer != null && buffer.Length > Length)
            {
                byte[] data = buffer[0..Length];

                for (int i=0; i< Length /2; i++)
                {
                    Value += BitConverter.ToChar(data.ReverseWhenLittleEndian(),i*2); 
                }
                

                Remaining = buffer[Length..^0];
            }
            else Value = "";

            return Value;
        }

        object IInverterValue.GetFromBuffer(byte[] buffer)
        {
            return GetFromBuffer(buffer);
        }
    }
}
