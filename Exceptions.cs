using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TQDB_Parser
{
    public class ParseException : Exception
    {
        public ParseException(string filePath, int? lineNumber = null, string? info = null) : base(CreateMessage(filePath, lineNumber, info)) { }

        private static string CreateMessage(string filePath, int? lineNumber, string? info)
        {
            var messageBuilder = new StringBuilder();
            messageBuilder.Append($"Failed to parse File {filePath}");
            if (lineNumber is not null)
                messageBuilder.Append($" in line {lineNumber}");

            if (info is not null)
                messageBuilder.Append($", {info}");

            return messageBuilder.ToString();
        }
    }

    public class MergeException : Exception
    {
        public MergeException(string message) : base(message) { }
    }
}
