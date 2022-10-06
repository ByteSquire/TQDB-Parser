using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TQDB_Parser
{
    public static class Constants
    {
        public const string ClassKey = "Class";
        public const string TemplateKey = "templateName";
        public const string FileNameHistoryKey = "fileNameHistoryEntry";
    }

    public enum FileExtension : ushort
    {
        fnt,
        qst,
        dbr,
        pfx,
        ssh,
        wav,
        mp3,
        anm,
        tex,
        msh,
    }

    public enum VariableClass : ushort
    {
        @variable = 0,
        @static,
        @picklist,
        @array
    }

    public enum VariableType : ushort
    {
        @string = 0,
        @real,
        @int,
        @bool,
        @file,
        @equation,
        @eqnVariable,
        @include,
    }

    public enum GroupType : ushort
    {
        @list = 0,
        @system
    }
}
