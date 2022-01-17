using System.Net.Sockets;
using System.Text;
using GrowattMonitor.Models;

namespace GrowattMonitor.Helpers;

public static class Utils
{
    public static DateTime riseTime = DateTime.MinValue;
    public static DateTime setTime = DateTime.MinValue;

    public static string ByteArrayToString(byte[] ba)
    {
        StringBuilder hex = new StringBuilder(ba.Length * 2);
        foreach (byte b in ba)
            hex.AppendFormat("{0:x4}", b);
        return hex.ToString();
    }

    public static string ByteArrayToString2(byte[] ba)
    {
        return BitConverter.ToString(ba).Replace("-", "");
    }
    public static byte[] StringToByteArray(String hex)
    {
        int NumberChars = hex.Length;
        byte[] bytes = new byte[NumberChars / 2];
        for (int i = 0; i < NumberChars; i += 2)
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        return bytes;
    }

    public static byte[] ReverseBitConverter(string bcs)
    {
        string[] arr = bcs.Split(' ');
        byte[] array = new byte[arr.Length];
        for (int i = 0; i < arr.Length; i++) array[i] = Convert.ToByte(arr[i], 16);
        return array;
    }

    public static bool IsDaylight(double latitude, double longitude)
    {
        //return true;
        // riseTime and setTime already calculated, so now is in daylight range
        if (riseTime > DateTime.MinValue && setTime > DateTime.MinValue)
            return true;

        DateTime currentTime = DateTime.Now;
        bool isSunrise = false, isSunset = false;

        var result = SunTimes.Instance.CalculateSunRiseSetTimes(latitude, longitude, DateTime.Now.Date, ref riseTime, ref setTime, ref isSunrise, ref isSunset);

        if (!result)
        {
            // failsafe
            riseTime = DateTime.MinValue;
            setTime = DateTime.MinValue;
            return false;
        }

        Console.WriteLine($"Today is {DateTime.Now.Date:dd-MM-yyyy}, sunrise @ {riseTime:HH:mm:ss}, sunset @ {setTime:HH:mm:ss}");

        // account for calculation discrepancies
        riseTime = riseTime.AddMinutes(-20);
        setTime = setTime.AddMinutes(20);

        if (currentTime >= riseTime && currentTime <= setTime)
            return true;
        else
        {
            // no longer in daytime range, reset to defaults
            riseTime = DateTime.MinValue;
            setTime = DateTime.MinValue;
            return false;
        }
    }
}

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

public static class SocketExtensions
{
    public static async Task<Socket> AcceptSocketAsync(this TcpListener listener, CancellationToken token)
    {
        Socket t = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            t = await listener.AcceptSocketAsync();
        }
        catch (Exception ex) when (token.IsCancellationRequested)
        {
            throw new OperationCanceledException("Cancellation was requested while awaiting TCP client connection.", ex);
        }
        return t;
    }
}

public static class TelegramExtensions
{
    public static int GetMonth(this Telegram telegram, string prefix)
    {
        return int.Parse(telegram.RowKey.Substring(4, 2));
    }

    public static int GetYear(this Telegram telegram, string prefix)
    {
        return int.Parse(telegram.RowKey.Substring(0, 4));
    }

    public static string GetTablename(this Telegram telegram, string prefix)
    {
        return prefix + telegram.RowKey.Substring(0, 6);
    }

}
