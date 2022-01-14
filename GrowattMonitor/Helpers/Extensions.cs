using System.Net.Sockets;
using GrowattMonitor.Models;

namespace GrowattMonitor;

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
        //_ = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Socket t;
        try
        {
            t = await listener.AcceptSocketAsync(token);
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
    public static int? GetMonth(this Telegram telegram)
    {
        if (telegram.RowKey != null)
            return int.Parse(telegram.RowKey.Substring(4, 2));

        return null;
    }

    public static int? GetYear(this Telegram telegram)
    {
        if (telegram.RowKey != null)
            return int.Parse(telegram.RowKey[..4]);

        return null;
    }

    public static string? GetTablename(this Telegram telegram, string prefix)
    {
        if (telegram.RowKey != null)
            return string.Concat(prefix, telegram.RowKey[..6]);

        return null;
    }

}

