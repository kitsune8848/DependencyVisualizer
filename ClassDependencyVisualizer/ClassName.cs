
// ClassNames.cs
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using DependencyAnalyzer.DataStructure;

namespace ClassDependencyVisualizer
{
    public class ClassNamesWpf
    {
        public List<string> Names { get; private set; } = new List<string>();

        public ClassNamesWpf(List<string> names)
        {
            Names = names;
        }

        public static ClassNamesWpf Create(Dictionary<string, ClassDependency> classMap)
        {
            return new ClassNamesWpf(classMap.Keys.ToList());
        }
    }
}
