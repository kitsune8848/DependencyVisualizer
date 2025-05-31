using System.Collections.Generic;

namespace DependencyAnalyzer.DataStructure
{
    public class ClassDependency
    {
        public string ClassName { get; set; }

        public SymbolType SymbolType { get; set; }

        public HashSet<ClassDependency> Dependencies { get; set; } = new();

        public HashSet<ClassDependency> Dependents { get; set; } = new();

        // 中段：属性（フィールド、プロパティ）一覧
        public List<string> Fields { get; set; } = new();

        // 下段：操作（メソッド）一覧
        public List<string> Methods { get; set; } = new();

        public string summary { get; set; }
    }

}
