using System;
using System.Collections.Generic;
using System.Linq;
using DependencyAnalyzer.DataStructure;
using DependencyAnalyzer.Interface;

namespace ClassDependencyVisualizer.Manager
{
    public class FilterResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public IDisplaySetting DisplaySetting { get; set; }
        public string LogMessage { get; set; }

        public static FilterResult Success(IDisplaySetting displaySetting, string logMessage)
        {
            return new FilterResult
            {
                IsSuccess = true,
                DisplaySetting = displaySetting,
                LogMessage = logMessage
            };
        }

        public static FilterResult Error(string errorMessage)
        {
            return new FilterResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }
    }

    public class FilterManager
    {
        private string _selectedFilterMode = "Selection";

        public void SetFilterMode(string mode)
        {
            _selectedFilterMode = mode;
        }

        public FilterResult ProcessFilter(IEnumerable<ClassNode> rootNodes, int forward, int backward, bool displaySummary, bool displayFieldAndMethod)
        {
            return _selectedFilterMode switch
            {
                "Selection" => ProcessSelectionFilter(rootNodes, displaySummary, displayFieldAndMethod),
                "Distance" => ProcessDistanceFilter(rootNodes, forward, backward, displaySummary, displayFieldAndMethod),
                _ => FilterResult.Error("不明なフィルターモードです。")
            };
        }

        private FilterResult ProcessSelectionFilter(IEnumerable<ClassNode> rootNodes, bool displaySummary, bool displayFieldAndMethod)
        {
            var selectedNames = GetCheckedClassNames(rootNodes.ToList(), "");
            if (selectedNames.Count == 0)
            {
                return FilterResult.Error("クラスを1つ以上選択してください。");
            }

            var displaySetting = new SelectionDisplaySetting(selectedNames, displaySummary, displayFieldAndMethod);
            var logMessage = $"{selectedNames.Count}個のクラスを出力しました";

            return FilterResult.Success(displaySetting, logMessage);
        }

        private FilterResult ProcessDistanceFilter(IEnumerable<ClassNode> rootNodes, int forward, int backward, bool displaySummary, bool displayFieldAndMethod)
        {
            var selectedNames = GetCheckedClassNames(rootNodes.ToList(), "");
            if (selectedNames.Count != 1)
            {
                return FilterResult.Error("距離フィルターは1つのクラスのみ選択してください。");
            }

            var displaySetting = new DistanceDisplaySetting(selectedNames[0], forward, backward, displaySummary, displayFieldAndMethod);
            var logMessage = $"選択項目: {selectedNames[0]}(依存先距離: {forward}、依存元距離: {backward})を出力しました。";

            return FilterResult.Success(displaySetting, logMessage);
        }

        private List<string> GetCheckedClassNames(List<ClassNode> nodes, string prefix)
        {
            var list = new List<string>();

            foreach (var node in nodes)
            {
                var fullPath = string.IsNullOrEmpty(prefix) ? node.Name : $"{prefix}.{node.Name}";

                if (node.IsChecked)
                {
                    var trimmed = fullPath.StartsWith("All.") ? fullPath.Substring(4) : fullPath;
                    list.Add(trimmed);
                }

                list.AddRange(GetCheckedClassNames(node.Children.ToList(), fullPath));
            }

            return list;
        }
    }
}

