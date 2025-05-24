using System;
using System.Collections.Generic;
using DependencyAnalyzer.DataStructure;

namespace ClassDependencyVisualizer.Manager
{
    public class SearchManager
    {
        public int SearchAndHighlight(IEnumerable<ClassNode> nodes, string keyword)
        {
            int count = 0;
            foreach (var node in nodes)
            {
                var isMatch = !string.IsNullOrEmpty(keyword) &&
                    node.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;

                node.IsHighlighted = isMatch;

                if (isMatch)
                {
                    ExpandParentNodes(node);
                    count++; 
                }

                count += SearchAndHighlight(node.Children, keyword);
            }
            return count;
        }


        private void ExpandParentNodes(ClassNode node)
        {
            var parent = node.Parent;
            while (parent != null)
            {
                parent.IsExpanded = true;
                parent = parent.Parent;
            }
        }
    }
}