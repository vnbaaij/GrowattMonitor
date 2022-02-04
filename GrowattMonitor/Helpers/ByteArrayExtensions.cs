namespace GrowattMonitor.Helpers;

public static class ByteArrayExtensions
{
    public static byte[] ReverseWhenLittleEndian(this byte[] array)
    {

        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(array);
        }
        return array;
    }
}
