using System.Globalization;

namespace TQDB_Parser.Extensions
{
    public static class TQNumberString
    {
        public static bool TryParseTQString(string? s, out int result)
        {
            result = 0;
            if (s is null)
                return false;

            return int.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out result);
        }

        public static bool TryParseTQString(string? s, out bool result)
        {
            result = false;
            if (s is null)
                return false;
            if (!(s == "0" || s == "1"))
                return false;

            return int.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var b) && b != 0;
        }

        public static bool TryParseTQString(string? s, out float result)
        {
            result = 0;
            if (s is null)
                return false;

            return float.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out result);
        }
    }
}
