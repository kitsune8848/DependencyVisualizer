using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DependencyAnalyzer.Interface;
using DependencyAnalyzer.DataStructure;

namespace DependencyAnalyzer.PlantUml
{
    public class PlantUmlGenerator : IUmlGenerator
    {
        public void Generate(Dictionary<string, ClassDependency> classMap, string filePath)
        {
            Generate(classMap, filePath, (DistanceDisplaySetting)null);
        }

        public void Generate(Dictionary<string, ClassDependency> classMap, string filePath, DistanceDisplaySetting displaySetting)
        {
            var displayClasses = GetDisplayClasses(classMap, displaySetting);
            GenerateInternal(classMap, filePath, displayClasses, displaySetting?.RootName, displaySammary:displaySetting.DisplaySammary);
        }

        public void Generate(Dictionary<string, ClassDependency> classMap, string filePath, SelectionDisplaySetting displaySetting)
        {
            var displayClasses = GetDisplayClasses(classMap, displaySetting);
            GenerateInternal(classMap, filePath, displayClasses, displaySammary:displaySetting.DisplaySammary);
        }
        private void GenerateInternal(Dictionary<string, ClassDependency> classMap, string filePath, HashSet<string> displayClasses, string rootClassName = null, bool displaySammary = false)
        {
            var sb = new StringBuilder();
            sb.AppendLine("@startuml");
            sb.AppendLine("skinparam classAttributeIconSize 0");

            // クラス名のマッピング（重複を避けるために接尾辞をつける）
            var classNameMap = new Dictionary<string, string>();
            var shortNameCount = new Dictionary<string, int>();

            foreach (var fullName in displayClasses)
            {
                var shortName = fullName.Split('.').Last();
                if (!shortNameCount.ContainsKey(shortName))
                {
                    shortNameCount[shortName] = 0;
                    classNameMap[fullName] = shortName;
                }
                else
                {
                    shortNameCount[shortName]++;
                    var disambiguatedName = $"{shortName}_{shortNameCount[shortName]}";
                    classNameMap[fullName] = disambiguatedName;
                }
            }

            // 名前空間ツリーの構築と出力
            var nsTree = new NamespaceNode("");
            foreach (var fullName in displayClasses)
            {
                var parts = fullName.Split('.');
                if (parts.Length < 2) continue;
                nsTree.Add(parts[..^1], fullName);
            }

            nsTree.Render(sb, classNameMap, classMap, rootClassName, displaySammary:displaySammary);

            // 依存関係の出力
            foreach (var classEntry in classMap.Values)
            {
                if (!displayClasses.Contains(classEntry.ClassName)) continue;

                var from = classNameMap[classEntry.ClassName];
                foreach (var dep in classEntry.Dependencies)
                {
                    if (!displayClasses.Contains(dep.ClassName)) continue;

                    var to = classNameMap[dep.ClassName];
                    string arrow = GetDependencyArrow(classEntry.SymbolType, dep.SymbolType);
                    string color = classEntry.ClassName == rootClassName ? " #red" : "";

                    sb.AppendLine($"{Escape(from)} {arrow} {Escape(to)}{color}");
                }
            }

            sb.AppendLine("@enduml");

            try
            {
                File.WriteAllText(filePath, sb.ToString());
                Console.WriteLine($"ファイル '{filePath}' に正常に書き込みました。");
                Console.WriteLine($"表示クラス数: {displayClasses.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"エラーが発生しました: {ex.Message}");
            }
        }

        private HashSet<string> GetDisplayClasses(Dictionary<string, ClassDependency> classMap, SelectionDisplaySetting displaySetting)
        {
            var displayClasses = new HashSet<string>();

            if (displaySetting == null)
            {
                return new HashSet<string>(classMap.Keys);
            }

            foreach (var className in displaySetting.DisplayClassNames)
            {
                if (classMap.ContainsKey(className))
                {
                    displayClasses.Add(className);
                }
            }

            return displayClasses;
        }

        private HashSet<string> GetDisplayClasses(Dictionary<string, ClassDependency> classMap, DistanceDisplaySetting displaySetting)
        {
            var displayClasses = new HashSet<string>();

            if (displaySetting == null)
            {
                return new HashSet<string>(classMap.Keys);
            }

            if (!string.IsNullOrEmpty(displaySetting.RootName) && classMap.ContainsKey(displaySetting.RootName))
            {
                displayClasses.Add(displaySetting.RootName);
                ExpandDependencies(classMap, displayClasses, displaySetting.RootName,
                                   displaySetting.DistanceDisplayDependency, displaySetting.DistanceDisplayDependent);
            }

            return displayClasses;
        }

        private void ExpandDependencies(Dictionary<string, ClassDependency> classMap, HashSet<string> displayClasses,
                                      string rootName, int dependencyDistance, int dependentDistance)
        {
            if (dependencyDistance > 0)
            {
                var visited = new HashSet<string>();
                ExpandDependenciesRecursive(classMap, displayClasses, rootName, dependencyDistance, visited, true);
            }

            if (dependentDistance > 0)
            {
                var visited = new HashSet<string>();
                ExpandDependentsRecursive(classMap, displayClasses, rootName, dependentDistance, visited);
            }
        }

        private void ExpandDependenciesRecursive(Dictionary<string, ClassDependency> classMap, HashSet<string> displayClasses,
                                               string currentClass, int remainingDistance, HashSet<string> visited, bool forward)
        {
            if (remainingDistance < 0 || visited.Contains(currentClass) || !classMap.ContainsKey(currentClass))
                return;

            visited.Add(currentClass);
            displayClasses.Add(currentClass);

            if (forward)
            {
                foreach (var dep in classMap[currentClass].Dependencies)
                {
                    if (classMap.ContainsKey(dep.ClassName))
                    {
                        ExpandDependenciesRecursive(classMap, displayClasses, dep.ClassName, remainingDistance - 1, visited, forward);
                    }
                }
            }
        }

        private void ExpandDependentsRecursive(Dictionary<string, ClassDependency> classMap, HashSet<string> displayClasses,
                                             string targetClass, int remainingDistance, HashSet<string> visited)
        {
            if (remainingDistance <= 0 || visited.Contains(targetClass))
                return;

            visited.Add(targetClass);

            foreach (var kvp in classMap)
            {
                var className = kvp.Key;
                var classDep = kvp.Value;

                if (classDep.Dependencies.Any(d => d.ClassName == targetClass))
                {
                    displayClasses.Add(className);
                    ExpandDependentsRecursive(classMap, displayClasses, className, remainingDistance - 1, visited);
                }
            }
        }

        private string GetDependencyArrow(SymbolType fromType, SymbolType toType)
        {
            if (toType == SymbolType.Interface &&
                (fromType == SymbolType.InterfaceImplementingClass))
            {
                return "--|>";
            }

            if ((fromType == SymbolType.Class && toType == SymbolType.Class) ||
                (fromType == SymbolType.Class && toType == SymbolType.AbstractClass) ||
                (fromType == SymbolType.AbstractClass && toType == SymbolType.AbstractClass))
            {
                return "-->";
            }

            return "-->";
        }

        private string Escape(string text)
        {
            return text
                .Replace("&", "__")
                .Replace("<", "__")
                .Replace(">", "__")
                .Replace("\"", "__")
                .Replace(",", "__")
                .Replace(" ", "__")
                .Replace("'", "__");
        }
    }
}
