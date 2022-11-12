using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace TQDB_Parser.Blocks
{
    public abstract class Block
    {
        protected readonly ILogger? logger;

        protected abstract string BlockName { get; }

        public string FileName { get; private set; }

        public string Line { get; private set; }

        public string Name { get; private set; }

        //public string Type { get; private set; }

        public IReadOnlyDictionary<string, string> KeyValuePairs { get; protected set; }

        public IReadOnlyList<Block> InnerBlocks { get; protected set; }

        public Block(string fileName, string lineIndex, IReadOnlyDictionary<string, string> keyValuePairs, IReadOnlyList<Block> innerBlocks, ILogger? logger = null)
        {
            FileName = fileName;
            Line = lineIndex;
            this.logger = logger;
            KeyValuePairs = keyValuePairs;
            InnerBlocks = innerBlocks;

            if (!KeyValuePairs.TryGetValue("name", out var name))
                LogException.LogAndThrowException(logger, new ParseException(fileName, lineIndex, $"{BlockName} Block is missing it's name field"), this);
            Name = name;
            //if (!KeyValuePairs.TryGetValue("type", out var type))
            //    throw new ParseException($"Failed to parse File {fileName}, the Block in line {lineIndex} is missing it's type field");
            //Type = type;
        }

        public override string ToString()
        {
            return ToIndentedString(0);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            if (obj is Block b)
            {
                if (Name != b.Name)
                    return false;
                //if (Type != b.Type)
                //    return false;
                if (!KeyValuePairs.SequenceEqual(b.KeyValuePairs))
                    return false;
                if (!InnerBlocks.SequenceEqual(b.InnerBlocks))
                    return false;

                return true;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name/*, Type*/, KeyValuePairs.GetHashCode(), InnerBlocks.GetHashCode());
        }

        //public string ToString(bool printIncludes)
        //{
        //    return ToIndentedString(0, printIncludes);
        //}

        private string ToIndentedString(int numTabs/*, bool printIncludes = true*/)
        {
            var tabBuilder = new StringBuilder();
            for (int i = 0; i < numTabs; i++)
                tabBuilder.Append('\t');
            var indentation = tabBuilder.ToString();

            StringBuilder builder = new();
            builder.Append(indentation);
            builder.AppendLine(BlockName);

            builder.Append(indentation);
            builder.AppendLine("{");

            var fieldIndentation = tabBuilder.Append('\t').ToString();
            foreach (var pair in KeyValuePairs)
            {
                builder.Append(fieldIndentation);
                builder.AppendLine($"{pair.Key} = \"{pair.Value}\"");
            }

            //var printedIncludes = false;
            //if (printIncludes && IncludeBlocks.Any())
            //{
            //    builder.AppendLine();
            //    foreach (var block in IncludeBlocks)
            //    {
            //        builder.AppendLine(block.ToIndentedString(numTabs + 1, printIncludes));
            //        builder.AppendLine();
            //    }
            //    printedIncludes = true;
            //}

            if (InnerBlocks.Any())
            {
                //if (!printedIncludes)
                builder.AppendLine();
                foreach (var block in InnerBlocks)
                {
                    builder.AppendLine(block.ToIndentedString(numTabs + 1/*, printIncludes*/));
                    builder.AppendLine();
                }
            }

            builder.Append(indentation);
            builder.Append('}');

            return builder.ToString();
        }
    }
}
