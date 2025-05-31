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
        public bool PropagateCheckToChildren { get; set; } = true;

        public bool IsChecked
        {
            get => isChecked;
            set
            {
                if (isChecked != value)
                {
                    isChecked = value;
                    OnPropertyChanged();

                    if (PropagateCheckToChildren)
                    {
                        foreach (var child in Children)
                        {
                            child.IsChecked = value;
                        }
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

        private bool isVisible = true;
        public bool IsVisible
        {
            get => isVisible;
            set
            {
                if (isVisible != value)
                {
                    isVisible = value;
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
        public static ObservableCollection<ClassNode> BuildTree(IEnumerable<string> fullNames, string filterMode)
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
                            Parent = parent,
                            PropagateCheckToChildren = filterMode=="Selection"
                        };
                        currentLevel.Add(existing);
                    }

                    parent = existing;
                    currentLevel = existing.Children;
                }
            }

            return root;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

}
