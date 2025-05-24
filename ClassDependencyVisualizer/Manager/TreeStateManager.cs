using System.Collections.Generic;
using System.Linq;
using DependencyAnalyzer.DataStructure;

namespace ClassDependencyVisualizer.Manager
{
    public class TreeStateManager
    {
        public Dictionary<string, bool> GetExpandStates(IEnumerable<ClassNode> nodes)
        {
            var states = new Dictionary<string, bool>();
            CollectExpandStates(nodes, states);
            return states;
        }

        public Dictionary<string, bool> GetCheckStates(IEnumerable<ClassNode> nodes)
        {
            var states = new Dictionary<string, bool>();
            CollectCheckStates(nodes, states);
            return states;
        }

        public void RestoreExpandStates(IEnumerable<ClassNode> nodes, Dictionary<string, bool> states)
        {
            foreach (var node in nodes)
            {
                if (states.TryGetValue(node.GetFullPath(), out var expanded))
                    node.IsExpanded = expanded;
                RestoreExpandStates(node.Children, states);
            }
        }

        public void RestoreCheckStates(IEnumerable<ClassNode> nodes, Dictionary<string, bool> states)
        {
            foreach (var node in nodes)
            {
                if (states.TryGetValue(node.GetFullPath(), out var isChecked))
                    node.IsChecked = isChecked;
                RestoreCheckStates(node.Children, states);
            }
        }

        private void CollectExpandStates(IEnumerable<ClassNode> nodes, Dictionary<string, bool> states)
        {
            foreach (var node in nodes)
            {
                states[node.GetFullPath()] = node.IsExpanded;
                CollectExpandStates(node.Children, states);
            }
        }

        private void CollectCheckStates(IEnumerable<ClassNode> nodes, Dictionary<string, bool> states)
        {
            foreach (var node in nodes)
            {
                states[node.GetFullPath()] = node.IsChecked;
                CollectCheckStates(node.Children, states);
            }
        }
    }
}