using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using DependencyAnalyzer.DataStructure;

namespace ClassDependencyVisualizer
{
    public class ClassNode : INotifyPropertyChanged
    {
        private string name;
        private bool isChecked;
        private bool isExpanded;
        private bool isHighlighted;

        public string Name
        {
            get => name;
            set { name = value; OnPropertyChanged(); }
        }

        public ClassNode Parent { get; set; }

        public ObservableCollection<ClassNode> Children { get; set; } = new ObservableCollection<ClassNode>();

        public bool IsChecked
        {
            get => isChecked;
            set
            {
                if (isChecked != value)
                {
                    isChecked = value;
                    OnPropertyChanged();
                    // 子にも反映
                    foreach (var child in Children)
                    {
                        child.IsChecked = value;
                    }
                }
            }
        }

        public bool IsExpanded
        {
            get => isExpanded;
            set
            {
                if (isExpanded != value)
                {
                    isExpanded = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsHighlighted
        {
            get => isHighlighted;
            set
            {
                if (isHighlighted != value)
                {
                    isHighlighted = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// ノードの階層パス（例: All.MyNamespace.MyClass）
        /// </summary>
        public string GetFullPath(string prefix = "")
        {
            return string.IsNullOrEmpty(prefix) ? Name : $"{prefix}.{Name}";
        }

        /// <summary>
        /// ドット区切りのフルネームリストをツリーに変換
        /// </summary>
        public static ObservableCollection<ClassNode> BuildTree(IEnumerable<string> fullNames)
        {
            var root = new ObservableCollection<ClassNode>();

            foreach (var fullName in fullNames)
            {
                var parts = fullName.Split('.');
                var currentLevel = root;
                ClassNode parent = null;

                foreach (var part in parts)
                {
                    var existing = currentLevel.FirstOrDefault(n => n.Name == part);
                    if (existing == null)
                    {
                        existing = new ClassNode
                        {
                            Name = part,
                            Parent = parent
                        };
                        currentLevel.Add(existing);
                    }

                    parent = existing;
                    currentLevel = existing.Children;
                }
            }

            return root;
        }

        /// <summary>
        /// 検索語を含むノードをハイライトし、親を展開する
        /// </summary>
        public static void HighlightMatches(IEnumerable<ClassNode> nodes, string keyword)
        {
            foreach (var node in nodes)
            {
                node.IsHighlighted = node.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase);

                if (node.IsHighlighted)
                {
                    // 親をすべて展開
                    var current = node.Parent;
                    while (current != null)
                    {
                        current.IsExpanded = true;
                        current = current.Parent;
                    }
                }

                HighlightMatches(node.Children, keyword);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

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
