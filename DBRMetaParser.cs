using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TQDB_Parser.DBRMeta
{
    public struct DBRMetadata
    {
        public string TemplateName { get; private set; }

        public string FileDescription { get; private set; }

        public DBRMetadata(string templateName, string? fileDescription)
        {
            TemplateName = templateName;
            FileDescription = fileDescription ?? string.Empty;
        }
    }

    public static class DBRMetaParser
    {
        public static DBRMetadata ParseFile(string filePath, ILogger? logger = null)
        {
            if (!File.Exists(filePath))
                LogException.LogAndThrowException(logger, new FileNotFoundException($"The specified dbr file {filePath} could not be found!"), typeof(DBRMetaParser));
            var lines = File.ReadAllLines(filePath);

            string? template = null;
            string? description = null;
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (template is not null && description is not null)
                    break;
                if (line.StartsWith("templateName,"))
                {
                    var split = line.Split(',');
                    if (split.Length != 3)
                        LogException.LogAndThrowException(logger, new ParseException(filePath, i + 1, info: "templateName contains invalid character , !"), caller: typeof(DBRMetaParser));
                    template = split[1];
                    continue;
                }
                if (line.StartsWith("FileDescription"))
                {
                    var split = line.Split(',');
                    if (split.Length != 3)
                        LogException.LogAndThrowException(logger, new ParseException(filePath, i + 1, info: "FileDescription contains invalid character , !"), caller: typeof(DBRMetaParser));
                    description = split[1];
                }
            }
            if (string.IsNullOrWhiteSpace(template))
                LogException.LogAndThrowException(logger, new ParseException(filePath, info: "missing templateName, this is a mandatory value!"), caller: typeof(DBRMetaParser));

            return new DBRMetadata(template, description);
        }
    }
}
