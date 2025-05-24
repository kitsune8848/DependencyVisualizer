using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DependencyAnalyzer.DataStructure
{
    public class ClassNameSet
    {
        public List<String> Names { get; private set; } = new List<String>();
        
        public ClassNameSet(List<string> names) 
        {
            Names = names;
        }

        public static ClassNameSet Create(Dictionary<string, ClassDependency> classMap)
        {
            return new ClassNameSet(classMap.Keys.ToList());

        }
    }
}
