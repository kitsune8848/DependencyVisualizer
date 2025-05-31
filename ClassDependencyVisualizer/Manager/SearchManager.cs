using System;
using System.Collections.Generic;
using System.Windows.Input;
using DependencyAnalyzer.DataStructure;

namespace ClassDependencyVisualizer.Manager
{
    public class SearchManager
    {
        private bool displayAllItem = false;
        private string previousKeyword = "";

        public int SearchAndHighlight(IEnumerable<ClassNode> nodes, string keyword)
        {
            if (keyword == previousKeyword) { displayAllItem = !displayAllItem; }
            else { displayAllItem = false; }
                int count = SearchAndHighlight(nodes, keyword, displayAllItem);
            previousKeyword = keyword;
            return count;
        }

        public int SearchAndHighlight(IEnumerable<ClassNode> nodes, string keyword, bool displayAllItem)
        {
            int count = 0;
            foreach (var node in nodes)
            {
                // 再帰的に子ノードもチェック
                int childMatches = SearchAndHighlight(node.Children, keyword, displayAllItem);

                bool isMatch = !string.IsNullOrEmpty(keyword) &&
                               node.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;

                node.IsHighlighted = isMatch;

                if (displayAllItem)
                {
                    node.IsVisible = true;
                }
                else
                {
                    node.IsVisible = isMatch || childMatches > 0;

                    if (node.IsVisible)
                    {
                        ExpandParentNodes(node);
                    }
                }

                if (isMatch)
                    count++;

                count += childMatches;
            }

            return count;
        }

        public void ClearSearch(IEnumerable<ClassNode> nodes)
        {
            foreach (var node in nodes)
            {
                // 再帰的に子ノードもチェック
                ClearSearch(node.Children);

                node.IsVisible = true;
                node.IsHighlighted = false;
            }
            previousKeyword = "";
            displayAllItem = false;
        }

        private void ExpandParentNodes(ClassNode node)
        {
            var parent = node.Parent;
            while (parent != null)
            {
                parent.IsExpanded = true;
                parent.IsVisible = true; 
                parent = parent.Parent;
            }
        }
    }
}