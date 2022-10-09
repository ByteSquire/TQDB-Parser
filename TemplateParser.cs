using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TQDB_Parser.Blocks;

namespace TQDB_Parser
{
    public class TemplateParser
    {
        private readonly string baseDir;
        private string filePath;
        private StreamReader? reader;
        private int lineIndex;
        private readonly Stack<int> openCurlyBrackets;
        private readonly ILogger? logger;

        /// <summary>
        /// Create a new TemplateParser for a given working directory
        /// </summary>
        /// <param name="baseDir"></param>
        /// <param name="filePath"></param
        /// <exception cref="FileNotFoundException"></exception>
        /// <exception cref="ParseException"></exception>
        public TemplateParser(string baseDir, ILogger? logger = null)
        {
            this.logger = logger;
            if (!Directory.Exists(baseDir))
                LogException.LogAndThrowException(logger, new DirectoryNotFoundException($"The specified base directory {baseDir} could not be found!"), this);
            //throw new DirectoryNotFoundException($"The specified directory {baseDir} could not be found!");
            this.baseDir = baseDir;
            openCurlyBrackets = new();
            filePath = string.Empty;
        }

        public GroupBlock ParseFile(string path)
        {
            this.filePath = path;
            var filePath = Path.Combine(baseDir, path);
            openCurlyBrackets.Clear();

            if (!File.Exists(filePath))
                LogException.LogAndThrowException(logger, new FileNotFoundException($"The specified template file {this.filePath} could not be found!"), this);

            if (string.IsNullOrEmpty(File.ReadAllText(filePath)))
                LogException.LogAndThrowException(logger, new ParseException(this.filePath, info: $"file is empty"), this);

            reader = new StreamReader(filePath);
            lineIndex = 0;
            var root = ParseTemplateRootGroup();

            if (openCurlyBrackets.Count > 0)
            {
                var openLines = openCurlyBrackets.ToArray();
                LogException.LogAndThrowException(logger,
                    new ParseException(this.filePath, info: $"{openLines.Length} }} missing! " +
                    $"Dangling {{ in line{(openLines.Length > 1 ? 's' : string.Empty)}:{Environment.NewLine}\t{string.Join(Environment.NewLine + "\t", openLines)}"),
                    this);
            }

            if (!reader.EndOfStream && logger is not null)
            {
                var leftover = reader.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(leftover))
                    logger?.LogDebug("Didn't parse file {FilePath} all the way to the end, remaining content: {Leftover}", this.filePath, leftover);
            }

            return root;
        }

        ~TemplateParser()
        {
            reader?.Close();
        }

        private void CheckLine(string expected, string? actual)
        {
            if (!expected.Equals(actual))
                LogException.LogAndThrowException(logger, new ParseException(filePath, lineIndex, $"expected {expected} but was {actual}"), this);
        }

        private string? ReadLine()
        {
            lineIndex++;
            var line = reader?.ReadLine()?.Trim();
            if (line is null)
                return null;

            var openCurlyBracketRegex = new Regex(@"""[^""]*""|(\{)");
            var closeCurlyBracketRegex = new Regex(@"""[^""]*""|(\})");

            var openMatches = openCurlyBracketRegex.Matches(line);
            foreach (var match in openMatches.AsEnumerable())
            {
                if (match.Groups[0].Success)
                    openCurlyBrackets.Push(lineIndex);
            }

            var closeMatches = closeCurlyBracketRegex.Matches(line);
            var remainingOpenCurlyBrackets = openCurlyBrackets.Count;
            foreach (var match in closeMatches.AsEnumerable())
            {
                if (match.Groups[0].Success && !openCurlyBrackets.TryPop(out var _))
                    LogException.LogAndThrowException(logger, new ParseException(filePath, lineIndex, $"expected a maximum of {remainingOpenCurlyBrackets} }} but was {line}"), this);
            }

            return line.Trim();
        }

        private GroupBlock ParseTemplateRootGroup()
        {
            // Templates have to start with a Group block
            var line = ReadLine();
            CheckLine("Group", line);

            return ParseGroupBlock();
        }

        public enum BlockType : byte
        {
            Group,
            Variable
        }

        private GroupBlock ParseGroupBlock()
        {
            (var blockStart, var innerBlocks, var keyValuePairs) = ParseBlock();
            return new GroupBlock(filePath, blockStart, keyValuePairs, innerBlocks, logger);
        }

        private VariableBlock ParseVariableBlock()
        {
            (var blockStart, var innerBlocks, var keyValuePairs) = ParseBlock();
            return new VariableBlock(filePath, blockStart, keyValuePairs, innerBlocks, logger);
        }

        private (int, IReadOnlyList<Block>, IReadOnlyDictionary<string, string>) ParseBlock()
        {
            var blockStart = lineIndex;
            var line = ReadLine();
            CheckLine("{", line);
            line = ReadLine();

            var innerBlocks = new List<Block>();
            var keyValuePairs = new Dictionary<string, string>();
            while (line is not null && !line.Contains('}'))
            {
                if (string.IsNullOrEmpty(line))
                {
                    line = ReadLine();
                    continue;
                }
                if ("Group".Equals(line))
                {
                    innerBlocks.Add(ParseGroupBlock());
                    line = ReadLine();
                    continue;
                }
                if ("Variable".Equals(line))
                {
                    innerBlocks.Add(ParseVariableBlock());
                    line = ReadLine();
                    continue;
                }
                var regex = new Regex(@"""[^""]*""|(=)");
                IList<Match> equalsMatches = regex.Matches(line);
                equalsMatches = equalsMatches.Where(x => x.Groups[0].Success).ToList();

                if (equalsMatches.Count != 2)
                    CheckLine("(a key) = \"(a value)\"", line);

                var key = line[..equalsMatches[0].Index].Trim();
                var value = line[equalsMatches[1].Index..].Trim().Trim('"');

                keyValuePairs.Add(key, value);
                line = ReadLine();
            }

            return (blockStart, innerBlocks, keyValuePairs);
        }
    }
}
