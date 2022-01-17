namespace GrowattMonitor.Models;

public interface IInverterValue
{
    public string Name { get; set; }
    public int Length { get; set; }
    public string Description { get; set; }

    public byte[] Remaining { get; set; }

    public object GetFromBuffer(byte[] buffer);

}
public interface IInverterValue<T> : IInverterValue
{

    public T Value { get; set; }

    public new T GetFromBuffer(byte[] buffer);
}
