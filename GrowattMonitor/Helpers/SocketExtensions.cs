using System.Net.Sockets;

namespace GrowattMonitor.Helpers;

public static class SocketExtensions
{
    public static async Task<Socket> AcceptSocketAsync(this TcpListener listener, CancellationToken token)
    {
        Socket t = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
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
