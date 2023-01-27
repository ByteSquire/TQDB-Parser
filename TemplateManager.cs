using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TQDB_Parser.Blocks;

namespace TQDB_Parser
{
    public class TemplateManager
    {
        private readonly Dictionary<string, GroupBlock> templateRootsByPath;
        private readonly ILogger? logger;

        private readonly string[] templatePathFixes = new string[]
        {
            "Custommaps\\Art_TQX3\\",
            "Custommaps\\Art_TQX4\\",
            "%TEMPLATE_DIR%"
        };

        public string TemplateBaseDir { get; private set; }

        public bool UseParallel { get; private set; }

        public TemplateManager(string baseDir, bool useParallel = true, ILogger? logger = null)
        {
            this.logger = logger;
            UseParallel = useParallel;
            if (!Directory.Exists(baseDir))
                LogException.LogAndThrowException(logger, new DirectoryNotFoundException($"The specified directory {baseDir} could not be found!"), this);
            TemplateBaseDir = baseDir;
            templateRootsByPath = new();
        }

        public void ResolveIncludes(GroupBlock root)
        {
            if (!root.AreIncludesResolved(true))
                root.ResolveIncludes(this);
        }

        public void ResolveAllIncludes()
        {
            var roots = templateRootsByPath.Values.ToList();
            foreach (var root in roots)
                ResolveIncludes(root);
        }

        public string GetTemplateName(string filePath)
        {
            string templateName = Path.GetRelativePath(TemplateBaseDir, filePath);

            FixTemplateName(ref templateName);

            return templateName;
        }

        private void FixTemplateName(ref string templateName)
        {
            // workaround to be able to parse templates that someone saved with their path
            foreach (var pathFix in templatePathFixes)
            {
                if (templateName.StartsWith(pathFix) && !Directory.Exists(Path.Combine(TemplateBaseDir, pathFix)))
                    templateName = templateName[pathFix.Length..];
            }
        }

        public GroupBlock GetRoot(string templateName)
        {
            FixTemplateName(ref templateName);

            if (templateRootsByPath.TryGetValue(templateName.ToLower(), out var root))
                return root;
            else
                return ParseTemplate(templateName);
        }

        private IReadOnlyCollection<GroupBlock> ParseTemplatesInDir(string path, bool recursive, bool overwriteCache)
        {
            var entries = Directory.EnumerateFiles(path, "*.tpl", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

            //var partitioner = Partitioner.Create(entries);

            var concurrentDict = new ConcurrentDictionary<string, GroupBlock>();

            if (UseParallel)
                Parallel.ForEach(entries, entry => AddEntry(entry));
            else
                foreach (var entry in entries)
                    AddEntry(entry);

            foreach (var pair in concurrentDict)
            {
                var key = pair.Key.ToLower();
                if (templateRootsByPath.TryGetValue(key, out var value))
                {
                    if (!overwriteCache)
                        concurrentDict[key] = value;
                }
                templateRootsByPath[key] = pair.Value;
            }

            return (IReadOnlyCollection<GroupBlock>)concurrentDict.Values;

            //foreach (var entry in entries)
            //{
            //    var relativePath = entry[(templateBaseDir.Length + 1)..];
            //    templateRootsByPath.Add(relativePath, parser.ParseFile(relativePath));
            //}

            void AddEntry(string entry)
            {
                var relativePath = Path.GetRelativePath(TemplateBaseDir, entry);
                concurrentDict.TryAdd(relativePath, new TemplateParser(TemplateBaseDir, logger).ParseFile(relativePath));
            }
        }

        public IReadOnlyCollection<GroupBlock> ParseAllTemplates(bool overwriteCache = false)
        {
            return ParseTemplatesInDir(TemplateBaseDir, true, overwriteCache);
        }

        public GroupBlock ParseTemplate(string path, bool overwriteCache = false)
        {
            if (templateRootsByPath.TryGetValue(path.ToLower(), out var block))
            {
                if (overwriteCache)
                {
                    block = new TemplateParser(TemplateBaseDir, logger).ParseFile(path);
                    templateRootsByPath[path.ToLower()] = block;
                    return block;
                }
                else
                    return block;
            }
            else
            {
                block = new TemplateParser(TemplateBaseDir, logger).ParseFile(path);
                templateRootsByPath.Add(path.ToLower(), block);
                return block;
            }
        }

        public IReadOnlyCollection<GroupBlock> ParseTemplates(string folder, bool recursive = true, bool overwriteCache = false)
        {
            return ParseTemplatesInDir(Path.Combine(TemplateBaseDir, folder), recursive, overwriteCache);
        }
    }
}
