using GrowattMonitor.Models;

namespace GrowattMonitor.Helpers;

public static class TelegramExtensions
{
    public static int GetMonth(this Telegram telegram)
    {
        return int.Parse(telegram.RowKey.Substring(4, 2));
    }

    public static int GetYear(this Telegram telegram)
    {
        return int.Parse(telegram.RowKey[..4]);
    }

    public static string GetTablename(this Telegram telegram, string prefix)
    {
        return prefix + telegram.RowKey[..6];
    }

}
