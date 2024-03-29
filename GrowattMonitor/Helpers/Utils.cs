﻿using System.Text;
using GrowattMonitor.Configuration;
using Microsoft.Extensions.Options;

namespace GrowattMonitor.Helpers;

public class Utils
{
    private static DateTime riseTime = DateTime.MinValue;
    private static DateTime setTime = DateTime.MinValue;
    private readonly ILogger<Utils> logger;
    private readonly AppConfig config;

    public Utils(ILogger<Utils> logger, IOptions<AppConfig> config)
    {
        this.logger = logger;
        this.config = config.Value;
    }

    public static string ByteArrayToString(byte[] ba)
    {
        StringBuilder hex = new(ba.Length * 2);
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

    public bool IsDaylight()
    {
        double latitude = config.Latitude;
        double longitude = config.Longitude;
        int offset = config.Offset;

        DateTime currentTime = DateTime.Now;

        // riseTime and setTime already calculated and current time is in between => in daylight range
        if (riseTime > DateTime.MinValue && setTime > DateTime.MinValue && currentTime >= riseTime && currentTime <= setTime)
            return true;
        
        bool isSunrise = false, isSunset = false;

        var result = SunTimes.Instance.CalculateSunRiseSetTimes(latitude, longitude, DateTime.Now.Date, ref riseTime, ref setTime, ref isSunrise, ref isSunset);

        if (!result)
        {
            // failsafe
            riseTime = DateTime.MinValue;
            setTime = DateTime.MinValue;
            return false;
        }

        logger.LogInformation("Today is {now:dd-MM-yyyy}, sunrise @ {rise:HH:mm:ss}, sunset @ {set:HH:mm:ss}, offset is {offset} minutes", DateTime.Now.Date, riseTime, setTime, offset);
        

        // account for calculation discrepancies
        riseTime = riseTime.AddMinutes(offset);
        setTime = setTime.AddMinutes(-offset);

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
