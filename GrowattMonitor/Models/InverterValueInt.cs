namespace GrowattMonitor.Models;

public class InverterValueInt : IInverterValue<int>
{
    
    public string Unit { get; set; }
    
    public int Skip { get; set; }
    public int Value { get; set; }
    public string Name { get; set; }
    public int Length { get; set; }
    public string Description { get; set; }
    public byte[] Remaining { get; set; } = Array.Empty<byte>();

    public InverterValueInt(string name, string unit="", string description = "Unknown", int skip = 0)
    {
        Name = name;
        Unit = unit;
        Length = 2;
        Description = description;
        Skip = skip;
    }

    public int GetFromBuffer(byte[] buffer)
    {
        if (buffer != null && buffer.Length > Length)
        {
            byte[] data = buffer[0..Length];

            if (Length == 2)
                Value = BitConverter.ToUInt16(data.ReverseWhenLittleEndian());
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
