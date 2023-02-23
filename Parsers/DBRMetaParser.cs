using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;

namespace TQDB_Parser.DBRMeta
{
    public struct DBRMetadata
    {
        public string TemplateName { get; private set; }

        public string FileDescription { get; private set; }

        public DBRMetadata(string? templateName, string? fileDescription)
        {
            TemplateName = templateName ?? string.Empty;
            FileDescription = fileDescription ?? string.Empty;
        }
    }

    public static class DBRMetaParser
    {
        public static DBRMetadata ParseFile(string filePath, ILogger? logger = null)
        {
            if (!File.Exists(filePath))
                LogException.LogAndThrowException(logger, new FileNotFoundException($"The specified dbr file {filePath} could not be found!"), typeof(DBRMetaParser));

            string? template = null;
            string? description = null;
            using TextFieldParser parser = new(filePath)
            {
                TextFieldType = FieldType.Delimited,
            };
            parser.SetDelimiters(",");
            while (!parser.EndOfData)
            {
                if (template != null && description != null)
                    break;
                try
                {
                    //Processing row
                    string[] fields = parser.ReadFields()!;
                    if (fields.Length != 3)
                        throw new MalformedLineException("Expected to be 3 columns", parser.LineNumber);

                    var key = fields[0];
                    var value = fields[1];
                    if (key.Equals("templateName"))
                    {
                        template = value;
                        continue;
                    }
                    if (key.Equals("FileDescription"))
                    {
                        description = value;
                    }
                }
                catch (MalformedLineException exc)
                {
                    var lineNumber = parser.ErrorLineNumber;
                    var line = parser.ErrorLine;
                    logger?.LogWarning("Warning, error parsing line {lineNumber} content: {line} in file {filePath}, reason:\n{message}", lineNumber, line, filePath, exc.Message);
                    continue;
                }
            }
            //if (string.IsNullOrWhiteSpace(template))
            //    LogException.LogAndThrowException(logger, new ParseException(filePath, info: "missing templateName, this is a mandatory value!"), caller: typeof(DBRMetaParser));

            return new DBRMetadata(template, description);
        }
    }
}
