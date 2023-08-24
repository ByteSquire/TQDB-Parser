using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TQDB_Parser.Blocks;

namespace TQDB_Parser.DBR
{
    public class DBREntry
    {
        private bool isValid;

        public IReadOnlyList<int> InvalidIndices { get; private set; }

        public VariableBlock Template { get; private set; }

        public string Name => Template.Name;

        public string Value { get; private set; }

        public DBREntry(VariableBlock template, string? value = null)
        {
            Template = template;

            Value = value ?? template.GetDefaultValue();
            if (!(isValid = Template.ValidateValue(Value, out var indices)))
                InvalidIndices = indices;
            else
                InvalidIndices = Array.Empty<int>();
        }

        public void UpdateValue(string value)
        {
            Value = value;
            if (!(isValid = Template.ValidateValue(Value, out var indices)))
                InvalidIndices = indices;
        }

        public bool IsValid()
        {
            return isValid;
        }

        public override string ToString()
        {
            return Name + ',' + Value + ',';
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            if (obj is not DBREntry other)
                return false;
            if (!Template.Equals(other.Template))
                return false;
            if (!Value.Equals(other.Value))
                return false;

            return true;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Template, Value);
        }
    }
}
