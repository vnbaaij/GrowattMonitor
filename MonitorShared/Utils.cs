﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GrowattMonitorShared
{

    public static class Utils
    {
        private static DateTime riseTime = DateTime.MinValue;
        private static DateTime setTime = DateTime.MinValue;

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
            // riseTime and setTime already calculated, so now is in daylight range
            if (riseTime > DateTime.MinValue && setTime > DateTime.MinValue)
                return true;

            DateTime currentTime = DateTime.Now;
            bool isSunrise=false, isSunset=false;

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
            setTime = setTime.AddMinutes(15);                

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
}
