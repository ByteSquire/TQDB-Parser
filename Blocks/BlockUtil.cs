using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TQDB_Parser.Blocks
{
    public static class EnumerableBlockExtensions
    {
        public static IEnumerable<T> WhereType<T>(this IEnumerable<Block> blocks) where T : Block
        {
            return blocks.Where(x => typeof(T).IsInstanceOfType(x)).Select(x => (T)x);
        }
    }

    public class BlockMetaComparer : IEqualityComparer<Block>
    {
        public bool Equals(Block? x, Block? y)
        {
            if (x is GroupBlock gX && y is GroupBlock gY)
                return GroupEquals(gX, gY);

            if (x is VariableBlock vX && y is VariableBlock vY)
                return VariableEquals(vX, vY);

            if (x is null && y is null)
                return true;

            return false;
        }

        private static bool GroupEquals(GroupBlock x, GroupBlock y)
        {
            return x.Name == y.Name && x.Type == y.Type;
        }

        private static bool VariableEquals(VariableBlock x, VariableBlock y)
        {
            return x.Name == y.Name && x.Type == y.Type && x.Class == y.Class;
        }

        public int GetHashCode([DisallowNull] Block obj)
        {
            if (obj is GroupBlock gBlock)
                return GetHashCode(gBlock);

            if (obj is VariableBlock vBlock)
                return GetHashCode(vBlock);

            throw new NotImplementedException();
        }

        private static int GetHashCode([DisallowNull] GroupBlock obj)
        {
            return HashCode.Combine(obj.Name, obj.Type);
        }

        private static int GetHashCode([DisallowNull] VariableBlock obj)
        {
            return HashCode.Combine(obj.Name, obj.Type, obj.Class);
        }
    }
}
