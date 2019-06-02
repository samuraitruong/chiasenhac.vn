using System;
using System.Web;
using Newtonsoft.Json;

namespace ChiaSeNhac.VN
{
    public static class Extensions
    {
        public static void Print(this Object obj)
        {
            Console.WriteLine(JsonConvert.SerializeObject(obj, Formatting.Indented));
        }

        public static string UrlDecode(this string input)
        {
            return HttpUtility.UrlDecode(input);
        }
        public static string HumanRead(this long bytes)
        {
            var kb = bytes / 1024;
            if (kb < 1000) return $"{kb:0.00} KB";
            var mb = kb / 1024;

            return $"{mb:0.00} MB";
        }
    }
}
