using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DependencyAnalyzer.DataStructure;

namespace DependencyAnalyzer.PlantUml
{
    public class NamespaceNode
    {
        public string Name;
        public Dictionary<string, NamespaceNode> Children = new();
        public List<string> Classes = new();

        public NamespaceNode(string name)
        {
            Name = name;
        }

        public void Add(string[] nsParts, string fullClassName)
        {
            if (nsParts.Length == 0)
            {
                Classes.Add(fullClassName);
                return;
            }

            var head = nsParts[0];
            var tail = nsParts.Skip(1).ToArray();

            if (!Children.ContainsKey(head))
                Children[head] = new NamespaceNode(head);

            Children[head].Add(tail, fullClassName);
        }

        public void Render(StringBuilder sb, Dictionary<string, string> classNameMap,
                   Dictionary<string, ClassDependency> classMap, string rootClassName = null, int indent = 0,
                   bool displaySummary = false, bool displyaFieldAndMethod = true)
        {
            string indentStr = new string(' ', indent * 2);

            foreach (var (ns, childNode) in Children)
            {
                sb.AppendLine($"{indentStr}package \"{Escape(ns)}[namepspace]\" {{");
                childNode.Render(sb, classNameMap, classMap, rootClassName, indent + 1, displaySummary, displyaFieldAndMethod);
                sb.AppendLine($"{indentStr}}}");
            }

            foreach (var classFullName in Classes)
            {
                var shortName = classNameMap[classFullName];
                var classDependency = classMap[classFullName];
                var elementType = GetPlantUmlElementType(classDependency.SymbolType);

                string colorSuffix = (classFullName == rootClassName) ? " #orange" : "";

                sb.AppendLine($"{indentStr}{elementType} {Escape(shortName)}{colorSuffix} {{");

                if (displyaFieldAndMethod)
                {
                    foreach (var field in classDependency.Fields)
                    {
                        sb.AppendLine($"{indentStr}  +{Escape(field)}");
                    }

                    foreach (var method in classDependency.Methods)
                    {
                        sb.AppendLine($"{indentStr}  +{Escape(method)}()");
                    }
                }
                else
                {
                    sb.AppendLine($"{indentStr}  +{classDependency.Fields.Count}");
                    sb.AppendLine($"{indentStr}  +{classDependency.Methods.Count}()");
                }
                sb.AppendLine($"{indentStr}}}");
                if (displaySummary && !string.IsNullOrWhiteSpace(classDependency.summary))
                {
                    var sanitizedSummary = classDependency.summary
                        .Replace("\r", "")
                        .Replace("\"", "'");

                    sb.AppendLine($"{indentStr}note right of {Escape(shortName)}");

                    var lines = sanitizedSummary.Split('\n');
                    foreach (var line in lines)
                    {
                        var trimmedLine = line.Trim(); // 余計な空白を除
                        foreach (var wrapped in WrapText(trimmedLine, 20))
                        {
                            sb.AppendLine($"{indentStr}    {wrapped}");  
                        }
                    }

                    sb.AppendLine($"{indentStr}end note");
                }


            }
        }
        private static IEnumerable<string> WrapText(string text, int maxLineLength)
        {
            for (int i = 0; i < text.Length; i += maxLineLength)
            {
                yield return text.Substring(i, Math.Min(maxLineLength, text.Length - i));
            }
        }

        private string GetPlantUmlElementType(SymbolType symbolType)
        {
            return symbolType switch
            {
                SymbolType.Interface => "interface",
                SymbolType.AbstractClass => "abstract class",
                SymbolType.Struct => "class",
                SymbolType.Enum => "enum",
                SymbolType.Delegate => "class",
                SymbolType.Class => "class",
                _ => "class"
            };
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
