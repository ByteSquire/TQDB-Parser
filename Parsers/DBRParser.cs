using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection.Metadata;
using TQDB_Parser.Blocks;
using TQDB_Parser.DBR;

namespace TQDB_Parser
{
    public class DBRParser
    {
        private readonly TemplateManager manager;
        private readonly ILogger? logger;

        public DBRParser(TemplateManager manager, ILogger? logger = null)
        {
            this.manager = manager;
            this.logger = logger;
        }

        public DBRFile ParseFile(string path)
        {
            var filePath = Path.Combine(manager.TemplateBaseDir, path);
            if (!File.Exists(filePath))
                LogException.LogAndThrowException(logger, new FileNotFoundException($"The specified dbr file {path} could not be found!"), this);

            var extension = Path.GetExtension(filePath);
            if (extension != ".dbr")
            {
                logger?.LogWarning("The file {path} uses extension {extension} instead of dbr", path, extension);
            }

            (var templateName, var rawEntries) = ParseRawEntries(filePath);

            try
            {
                var templateRoot = manager.GetRoot(templateName);
                if (!templateRoot.AreIncludesResolved(true))
                    manager.ResolveIncludes(templateRoot);
                return new DBRFile(path, templateRoot, ParseEntries(filePath, templateRoot, rawEntries));
            }
            catch (Exception exc)
            {
                logger?.LogError(exc, "File {path}, could not parse template file {templateName}, reason:\n{message}", path, templateName, exc.Message);
                throw;
            }
        }

        public DBRFile ChangeFileTemplate(DBRFile file, string templateName)
        {
            if (file.TemplateRoot.FileName == templateName)
                return file;

            var path = file.FilePath;
            var rawEntries = file.Entries.ToDictionary(x => x.Name, x => x.Value);
            try
            {
                var templateRoot = manager.GetRoot(templateName);
                return new DBRFile(path, templateRoot, ParseEntries(path, templateRoot, rawEntries));
            }
            catch (Exception exc)
            {
                logger?.LogError(exc, "File {path}, could not parse template file {templateName}, reason:\n{message}", path, templateName, exc.Message);
                throw;
            }
        }

        private (string, IReadOnlyDictionary<string, string>) ParseRawEntries(string filePath)
        {
            var ret = new Dictionary<string, string>();

            using TextFieldParser parser = new(filePath)
            {
                TextFieldType = FieldType.Delimited,
            };
            parser.SetDelimiters(",");
            while (!parser.EndOfData)
            {
                try
                {
                    //Processing row
                    string[] fields = parser.ReadFields()!;
                    if (fields.Length != 3)
                    {
                        logger?.LogWarning("Warning, error parsing line {lineNumber} content: \"{line}\" in file {filePath}, reason:\nExpected to be 3 columns (separated by ,)", parser.LineNumber, string.Join(',', fields), filePath);
                        continue;
                    }

                    var key = fields[0];
                    var value = fields[1];

                    //skip file name history
                    if (key == Constants.FileNameHistoryKey)
                        continue;

                    if (!ret.TryAdd(key, value))
                        logger?.LogWarning("File {filePath} in line {line}, variable {key} has already been defined", filePath, parser.LineNumber, key);
                }
                catch (MalformedLineException exc)
                {
                    var lineNumber = parser.ErrorLineNumber;
                    var line = parser.ErrorLine;
                    logger?.LogWarning("Warning, error parsing line {lineNumber} content: \"{line}\" in file {filePath}, reason:\n{message}", lineNumber, line, filePath, exc.Message);
                    continue;
                }
            }

            if (!ret.TryGetValue(Constants.TemplateKey, out var templateName))
                LogException.LogAndThrowException(logger, new ParseException(filePath, info: $"the file is missing the required {Constants.TemplateKey} key"), this);
            ret.Remove(Constants.TemplateKey);

            return (templateName!, ret);
        }

        public IReadOnlyDictionary<string, DBREntry> ParseEntries(string filePath, GroupBlock templateRoot, IReadOnlyDictionary<string, string> rawEntries)
        {
            var validVariables = templateRoot.GetVariables(true);

            var concurrentDict = new ConcurrentDictionary<string, DBREntry>();

            if (manager.UseParallel)
                Parallel.ForEach(rawEntries, x => ParseEntry(x));
            else
                foreach (var x in rawEntries)
                    ParseEntry(x);

            return concurrentDict;

            void ParseEntry(KeyValuePair<string, string> x)
            {
                try
                {
                    var variable = validVariables.Single(y => y.Name == x.Key);
                    var value = new DBREntry(variable, x.Value);

                    if (!value.IsValid())
                        logger?.LogWarning("File {filePath}, variable {key} has an invalid value!", filePath, x.Key);
                    concurrentDict.TryAdd(x.Key, value);
                }
                catch (InvalidOperationException)
                {
                    logger?.LogWarning("File {filePath}, unexpected variable {key}", filePath, x.Key);
                }
            }
        }
    }
}