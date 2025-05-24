using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DependencyAnalyzer.DataStructure
{
    public enum SymbolType
    {
        Class,
        Interface,
        AbstractClass,
        Struct,
        Enum,
        Delegate,
        InterfaceImplementingClass,
        Unknown
    }
}
