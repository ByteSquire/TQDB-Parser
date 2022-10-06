using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TQDB_Parser.Blocks
{
    public class GroupBlock : Block
    {
        protected readonly IList<VariableBlock> includeBlocks;

        protected IReadOnlyList<GroupBlock> groupBlocks;
        protected IReadOnlyList<GroupBlock> recursedGroupBlocks;

        protected IReadOnlyList<VariableBlock> variableBlocks;
        protected IReadOnlyList<VariableBlock> recursedVariableBlocks;

        public GroupBlock(string fileName, int lineIndex, IReadOnlyDictionary<string, string> keyValuePairs, IReadOnlyList<Block> innerBlocks, ILogger? logger = null)
            : base(fileName, lineIndex, keyValuePairs, innerBlocks, logger)
        {
            if (!KeyValuePairs.TryGetValue("type", out var type))
                LogException.LogAndThrowException(this.logger, new ParseException(fileName, lineIndex, "Group Block is missing it's type field"), this);
            if (!Enum.TryParse<GroupType>(type, true, out var typeEnum))
                this.logger?.LogWarning("File {fileName}, Group Block in line {lineIndex} is using an unknown type {type}, defaulting to {default}",
                    fileName, lineIndex, type, default(GroupType));
            Type = typeEnum;

            includeBlocks = InnerBlocks
                .WhereType<VariableBlock>()
                .Where(x => x.Name.Equals("Include File") && x.Class == VariableClass.@static && x.Type == VariableType.include)
                .ToList();

            groupBlocks = InnerBlocks.WhereType<GroupBlock>().ToList();
            recursedGroupBlocks = groupBlocks.Concat(groupBlocks.SelectMany(x => x.GetGroups(true))).ToList();

            variableBlocks = InnerBlocks.WhereType<VariableBlock>().ToList();
            recursedVariableBlocks = variableBlocks.Concat(groupBlocks.SelectMany(x => x.GetVariables(true))).ToList();
        }

        public void ResolveIncludes(TemplateManager manager)
        {
            if (!includeBlocks.Any())
                return;

            foreach (var includeBlockRef in includeBlocks)
            {
                // including hack to ignore %TEMPLATE_DIR%, don't know how that is resolved
                var includeBlock = manager.GetRoot(includeBlockRef.DefaultValue.Replace("%TEMPLATE_DIR%", string.Empty));
                includeBlock.ResolveIncludes(manager);
                // ignore header from included templates
                includeBlock.InnerBlocks = includeBlock.InnerBlocks.WhereType<GroupBlock>().Where(x => !(x.Name == "Header" && x.Type == GroupType.@system)).ToList();
                //foreach (var inner in includeBlock.InnerBlocks.WhereType(x => (GroupBlock)x))
                //    inner.ResolveIncludes(manager);

                MergeGroups(includeBlock);
            }
            foreach (var inner in InnerBlocks.WhereType<GroupBlock>())
                inner.ResolveIncludes(manager);

            // Remove include blocks, they are now resolved
            InnerBlocks = InnerBlocks.Except(includeBlocks).ToList();
            includeBlocks.Clear();

            // update filtered blocks
            groupBlocks = InnerBlocks.WhereType<GroupBlock>().ToList();
            recursedGroupBlocks = groupBlocks.Concat(groupBlocks.SelectMany(x => x.GetGroups(true))).ToList();

            variableBlocks = InnerBlocks.WhereType<VariableBlock>().ToList();
            recursedVariableBlocks = variableBlocks.Concat(groupBlocks.SelectMany(x => x.GetVariables(true))).ToList();
        }

        public IReadOnlyList<VariableBlock> GetVariables(bool recurse = false)
        {
            if (recurse)
                return recursedVariableBlocks;
            else
                return variableBlocks;
        }

        public IReadOnlyList<GroupBlock> GetGroups(bool recurse = false)
        {
            if (recurse)
                return recursedGroupBlocks;
            else
                return groupBlocks;
        }

        public bool IsChild(Block other, bool recurse = false)
        {
            if (other is GroupBlock gOther)
                return IsChild(gOther, recurse);
            if (other is VariableBlock vOther)
                return IsChild(vOther, recurse);

            throw new NotImplementedException();
        }

        public bool IsChild(GroupBlock other, bool recurse = false)
        {
            if (recurse)
                return recursedGroupBlocks.Contains(other);
            else
                return groupBlocks.Contains(other);
        }

        public bool IsChild(VariableBlock other, bool recurse = false)
        {
            if (recurse)
                return recursedVariableBlocks.Contains(other);
            else
                return variableBlocks.Contains(other);
        }

        protected void MergeGroups(GroupBlock other)
        {
            if (Type != other.Type)
                LogException.LogAndThrowException(logger, new MergeException($"Trying to merge different block types {Type} and {other.Type}"), this);

            var conflictingBlocks = other.InnerBlocks.Intersect(InnerBlocks, new BlockMetaComparer());
            var filteredBlocks = other.InnerBlocks.Except(conflictingBlocks);
            var groupsToMerge = conflictingBlocks
                .WhereType<GroupBlock>()
                .GroupJoin(InnerBlocks.WhereType<GroupBlock>(), x => x, y => y, (a, b)
                => new { a, b = b.Single() }, new BlockMetaComparer());

            foreach (var groupToMerge in groupsToMerge)
            {
                groupToMerge.a.MergeGroups(groupToMerge.b);
            }

            // Add additional blocks to this group
            InnerBlocks = InnerBlocks.Concat(filteredBlocks).ToImmutableList();
        }

        protected override string BlockName => "Group";

        public GroupType Type { get; private set; }
    }
}
