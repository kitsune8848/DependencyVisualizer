using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DependencyAnalyzer.DataStructure;

namespace DependencyAnalyzer.Interface
{
    public interface IUmlGenerator
    {
        void Generate(Dictionary<string, ClassDependency> classMap, string filePath);
        void Generate(Dictionary<string, ClassDependency> classMap, string filePath, DistanceDisplaySetting displaySetting);
        void Generate(Dictionary<string, ClassDependency> classMap, string filePath, SelectionDisplaySetting displaySetting);

    }
}
