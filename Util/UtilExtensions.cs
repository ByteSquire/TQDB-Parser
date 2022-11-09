using System.Globalization;

namespace TQDB_Parser.Extensions
{
    public static class UtilExtensions
    {
        public static string ToTQString(this float self, bool convertToInt = false)
        {
            if (convertToInt)
                return ToTQString((int)self);
            return self.ToString("F6", CultureInfo.InvariantCulture);
        }

        public static string ToTQString(this int self, bool convertToFloat = false)
        {
            if (convertToFloat)
                return ToTQString((float)self);
            return self.ToString("D", CultureInfo.InvariantCulture);
        }
    }
}
