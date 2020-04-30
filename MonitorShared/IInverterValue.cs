using System;
using System.Collections.Generic;
using System.Text;

namespace GrowattMonitorShared
{
    public interface IInverterValue
    {
        public string Name { get; set; }
        public int Length { get; set; }
        public string Description { get; set; } 
        
        public byte[] Remaining { get; set; }

    }
    public interface IInverterValue<T> : IInverterValue
    {
       
        public T Value  { get; set; }

         public T GetFromBuffer(byte[] buffer);
    }
}