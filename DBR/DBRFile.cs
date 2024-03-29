﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TQDB_Parser.Blocks;

namespace TQDB_Parser.DBR
{
    public class DBRFile
    {
        private readonly Dictionary<string, DBREntry> entries;
        private readonly string path;
        private readonly string templateName;
        private readonly ILogger? logger;

        public GroupBlock TemplateRoot { get; private set; }

        public string FileName { get; private set; }
        public string FilePath => path;

        public IReadOnlyList<DBREntry> Entries => entries.Values.ToList();

        public DBRFile(string path, GroupBlock templateRoot, ILogger? logger = null)
            : this(path, templateRoot, Array.Empty<KeyValuePair<string, DBREntry>>(), logger)
        { }

        public DBRFile(string path, GroupBlock templateRoot, IEnumerable<KeyValuePair<string, DBREntry>> entries, ILogger? logger = null)
        {
            this.path = path;
            FileName = Path.GetFileName(path);
            this.logger = logger;
            TemplateRoot = templateRoot;
            templateName = templateRoot.FileName;
            this.entries = new(entries);

            GenerateDefaultEntries();
        }

        public void UpdateEntry(string key, string newValue)
        {
            this[key].UpdateValue(newValue);
        }

        public DBREntry this[string key]
        {
            get
            {
                if (!entries.ContainsKey(key))
                    LogException.LogAndThrowException(logger, new KeyNotFoundException($"The given key {key} does not exist in file {FilePath}"), this);
                return entries[key];
            }
        }

        public IReadOnlyList<DBREntry> this[GroupBlock group]
        {
            get
            {
                if (!TemplateRoot.IsChild(group, true) && !TemplateRoot.Equals(group))
                    LogException.LogAndThrowException(logger, new ArgumentException($"The passed group {group.Name} is not part of template {TemplateRoot.FileName}"), this);

                var ret = new List<DBREntry>();

                foreach (var variable in group.GetVariables())
                    // workaround because eqnVariables all have the same name and they use their default value anyway
                    if (variable.Type == VariableType.eqnVariable)
                        ret.Add(new DBREntry(variable));
                    else
                        ret.Add(this[variable.Name]);

                return ret;
            }
        }

        private void GenerateDefaultEntries()
        {
            var variables = TemplateRoot.GetVariables(true);
            foreach (var variable in variables)
                // workaround because eqnVariables all have the same name and they use their default value anyway
                if (variable.Type != VariableType.eqnVariable)
                    entries.TryAdd(variable.Name, new DBREntry(variable));
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            // always print the templateName at the top, use windows style \ for path separator
            builder.AppendLine(PrintEntry(Constants.TemplateKey, templateName.Replace('/', '\\')));

            foreach (var entry in entries)
                builder.AppendLine(PrintEntry(entry.Value));

            return builder.ToString();
        }

        public void SaveFile(string? saveAs = null, Encoding? encoding = null)
        {
            encoding ??= new UTF8Encoding(false);
            File.WriteAllText(saveAs ?? path, ToString(), encoding);
        }

        private static string PrintEntry(string key, string value)
        {
            return key + ',' + value + ',';
        }

        private static string PrintEntry(DBREntry value)
        {
            return value.ToString();
        }
    }
}
