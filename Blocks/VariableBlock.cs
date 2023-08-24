using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TQDB_Parser.Extensions;

namespace TQDB_Parser.Blocks
{
    public class VariableBlock : Block
    {
        public VariableBlock(string fileName, string lineIndex, IReadOnlyDictionary<string, string> keyValuePairs, IReadOnlyList<Block> innerBlocks, ILogger? logger = null)
            : base(fileName, lineIndex, keyValuePairs, innerBlocks, logger)
        {
            if (!KeyValuePairs.TryGetValue("class", out var @class))
                LogException.LogAndThrowException(this.logger, new ParseException(fileName, lineIndex, "Variable Block is missing it's class field"), this);
            if (!Enum.TryParse<VariableClass>(@class, true, out var classEnum))
                this.logger?.LogWarning("File {fileName}, Variable Block in line {lineIndex} is using an unknown class {class}, defaulting to {default}",
                    fileName, lineIndex, @class, default(VariableClass));
            Class = classEnum;

            if (!KeyValuePairs.TryGetValue("type", out var type))
                LogException.LogAndThrowException(this.logger, new ParseException(fileName, lineIndex, "Variable Block is missing it's type field"), this);
            VariableType typeEnum = default;
            FileExtensions = Array.Empty<string>();

            if (type.StartsWith("file_"))
            {
                typeEnum = VariableType.file;

                var sExtensions = type.Split('_', 2)[1];
                var extensions = sExtensions.Split(',');
                var filteredExtensions = new List<string>(extensions.Length);

                var invalidChars = Path.GetInvalidFileNameChars();
                foreach (var extension in extensions)
                {
                    if (extension.Intersect(invalidChars).Any())
                    {
                        this.logger?.LogWarning("File {fileName}, Variable Block in line {lineIndex} references an invalid file extension {extension} in it's type, skipping",
                            fileName, lineIndex, extension);
                        continue;
                    }
                    var fixedExtension = extension.Trim();
                    if (fixedExtension.Any(x => char.IsWhiteSpace(x) || Path.GetInvalidFileNameChars().Contains(x)))
                    {
                        this.logger?.LogWarning("File {fileName}, Variable Block in line {lineIndex} references an file extension {extension} " +
                            "containing whitespace, ArtManager won't use that",
                            fileName, lineIndex, fixedExtension);
                        //continue;
                    }
                    else if (extension.Length > fixedExtension.Length)
                        this.logger?.LogWarning("File {fileName}, Variable Block in line {lineIndex} references a file extension {extension} " +
                            "with leading or trailing whitespace, ArtManager won't use that",
                            fileName, lineIndex, extension);
                    filteredExtensions.Add("." + fixedExtension.Trim());
                }

                FileExtensions = filteredExtensions;
            }
            else
            {
                if (!Enum.TryParse(type, true, out typeEnum))
                    this.logger?.LogWarning("File {fileName} in line {lineIndex}, Variable Block is using an unknown type {type}, defaulting to {default}",
                        fileName, lineIndex, type, default(VariableType));
            }
            Type = typeEnum;

            if (!KeyValuePairs.TryGetValue("description", out var description))
            {
                this.logger?.LogWarning("File {fileName} in line {lineIndex}, Variable Block is missing it's description field, defaulting to {default}",
                    fileName, lineIndex, "description = \"\"");
                description = string.Empty;
            }
            Description = description;
            if (!KeyValuePairs.TryGetValue("value", out var value))
            {
                this.logger?.LogWarning("File {fileName} in line {lineIndex}, Variable Block is missing it's value field, defaulting to {default}",
                    fileName, lineIndex, "value = \"\"");
                value = string.Empty;
            }
            Value = value;
            if (!KeyValuePairs.TryGetValue("defaultValue", out var defaultValue))
            {
                this.logger?.LogWarning("File {fileName} in line {lineIndex}, Variable Block is missing it's defaultValue field, defaulting to {default}",
                    fileName, lineIndex, "defaultValue = \"\"");
                defaultValue = string.Empty;
            }
            DefaultValue = defaultValue;
        }

        protected override string BlockName => "Variable";

        public VariableClass Class { get; private set; }

        public VariableType Type { get; private set; }

        public IReadOnlyCollection<string> FileExtensions { get; private set; }

        public string Description { get; private set; }

        /// <summary>
        /// ArtManager places this value in new files using this template, overrides <see cref="DefaultValue"/>
        /// </summary>
        public string Value { get; private set; }

        /// <summary>
        /// As far as I know this is just useful for listing picklist items, otherwise behaves the same as <see cref="Value"/>
        /// </summary>
        public string DefaultValue { get; private set; }

        public string GetDefaultValue()
        {
            if (Class == VariableClass.picklist)
                return DefaultValue;
            return string.IsNullOrEmpty(Value) ? DefaultValue : Value;
        }

        public bool ValidateValue(string value, out IReadOnlyList<int> invalidIndices)
        {
            var invalidIdx = new List<int>();
            invalidIndices = invalidIdx;
            if (string.IsNullOrEmpty(value))
                return true;

            var ret = true;
            if (Class == VariableClass.array)
            {
                var values = value.Split(';', StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < values.Length; i++)
                {
                    var element = values[i];
                    var innerInvalidIdx = new List<int>();
                    if (!ValidateValueInternal(element, innerInvalidIdx))
                    {
                        // Maybe support having indices inside array elements, haven't seen arrays of equations yet
                        //invalidIdx.AddRange(innerInvalidIdx);
                        logger?.LogWarning("The given value {element} at index {index} is invalid!", element, i);
                        invalidIdx.Add(i);
                    }
                }

                if (invalidIdx.Count > 0)
                    return false;
            }
            else
            {
                ret = ValidateValueInternal(value, invalidIdx);
            }

            return ret;
        }

        private bool ValidateValueInternal(string value, IList<int> invalidIndices)
        {
            switch (Type)
            {
                case VariableType.@string:
                case VariableType.eqnVariable:
                    return true;

                case VariableType.real:
                    return TQNumberString.TryParseTQString(value, out float _);
                case VariableType.@int:
                    return TQNumberString.TryParseTQString(value, out int _);
                case VariableType.@bool:
                    return value == "0" || value == "1";

                case VariableType.file:
                case VariableType.include:
                    if (!IsValidFilePath(value))
                        return false;

                    var validExtensions = Type == VariableType.include ? new string[] { ".tpl" } : FileExtensions;
                    var fileExtension = Path.GetExtension(value);
                    if (!validExtensions.Any(x => x == fileExtension))
                        return false;

                    return true;
                case VariableType.equation:
                    var validChars = new char[] { '(', ')', '.', '+', '-', '*', '/', '^' };
                    var bracketStack = new Stack<int>();

                    for (var i = 0; i < value.Length; i++)
                    {
                        var character = value[i];
                        if (char.IsWhiteSpace(character))
                            continue;
                        if (char.IsLetterOrDigit(character))
                            continue;
                        if (!validChars.Contains(character))
                            invalidIndices.Add(i);
                        if (character == '(')
                            bracketStack.Push(i);
                        if (character == ')')
                            if (!bracketStack.TryPop(out var _))
                                invalidIndices.Add(i);
                    }
                    if (bracketStack.Count > 0)
                    {
                        foreach (var pos in bracketStack)
                            invalidIndices.Add(pos);
                    }
                    if (invalidIndices.Count > 0)
                        return false;
                    return true;
                default:
                    // Cannot happen, default is VariableType.@string!
                    return false;
            }

            static bool IsValidFilePath(string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                    return false;

                if (!Path.HasExtension(path))
                    return false;

                //check path
                foreach (var invalidChar in Path.GetInvalidPathChars())
                {
                    if (path.Contains(invalidChar))
                        return false;
                }
                //check file name
                var fileName = Path.GetFileName(path);
                foreach (var invalidChar in Path.GetInvalidFileNameChars())
                {
                    if (fileName.Contains(invalidChar))
                        return false;
                }

                return true;
            }
        }
    }
}
