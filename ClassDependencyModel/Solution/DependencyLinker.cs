using DependencyAnalyzer.DataStructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DependencyAnalyzer.SolutionAna
{
    public class DependencyLinker 
    {
        public void Link(Dictionary<string, ClassDependency> classMap, Dictionary<string, HashSet<string>> dependencyNamesMap)
        {
            Console.WriteLine("Building dependency links...");

            int linkCount = 0;
            foreach (var (className, depNames) in dependencyNamesMap)
            {
                var classNode = classMap[className];
                foreach (var depName in depNames)
                {
                    if (depName == className) continue; // 自己依存の除外
                    if (classMap.TryGetValue(depName, out var depNode))
                    {
                        classNode.Dependencies.Add(depNode);
                        depNode.Dependents.Add(classNode);
                        linkCount++;
                    }
                }
            }

            Console.WriteLine($"Created {linkCount} dependency links.");
        }
    }
}
