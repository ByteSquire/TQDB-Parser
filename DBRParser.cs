﻿using Microsoft.Extensions.Logging;
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
                return new DBRFile(path, templateRoot, ParseEntries(filePath, templateRoot, rawEntries));
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
                        throw new MalformedLineException("Expected to be 3 columns", parser.LineNumber);

                    var key = fields[0];
                    var value = fields[1];

                    //skip file name history
                    if (key == Constants.FileNameHistoryKey)
                        continue;

                    ret.Add(key, value);
                }
                catch (MalformedLineException exc)
                {
                    var lineNumber = parser.ErrorLineNumber;
                    var line = parser.ErrorLine;
                    logger?.LogWarning("Warning, error parsing line {lineNumber} content: {line} in file {filePath}, reason:\n{message}", lineNumber, line, filePath, exc.Message);
                    continue;
                }
            }

            if (!ret.TryGetValue(Constants.TemplateKey, out var templateName))
                LogException.LogAndThrowException(logger, new ParseException(filePath, info: $"the file is missing the required {Constants.TemplateKey} key"), this);
            ret.Remove(Constants.TemplateKey);

            return (templateName, ret);
        }

        //private void ParseFile()
        //{
        //using var dbrFileReader = new StreamReader(path);
        //var line = dbrFileReader.ReadLine();
        //int lineIndex = 1;
        //while (line is not null)
        //{
        //    var columns = line.Split(',');
        //    if (columns.Length != 2)
        //    {
        //        Console.Error.WriteLine($"DBR-File {path} doesn't have two (non-empty) columns in line {lineIndex}?!");
        //        continue;
        //    }

        //    var key = columns[0];
        //    var valueString = columns[1];

        //    rawEntries.Add(key, valueString);
        //}


        //}

        private IReadOnlyDictionary<string, DBREntry> ParseEntries(string filePath, GroupBlock templateRoot, IReadOnlyDictionary<string, string> rawEntries)
        {
            var validVariables = templateRoot.GetVariables(true);

            var concurrentDict = new ConcurrentDictionary<string, DBREntry>();

            Parallel.ForEach(rawEntries, x =>
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
            });

            return concurrentDict;
        }
    }
}