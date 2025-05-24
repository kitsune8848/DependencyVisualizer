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

        public FilterResult ProcessFilter(IEnumerable<ClassNode> rootNodes, int forward, int backward, bool displaySammary)
        {
            return _selectedFilterMode switch
            {
                "Selection" => ProcessSelectionFilter(rootNodes, displaySammary),
                "Distance" => ProcessDistanceFilter(rootNodes, forward, backward, displaySammary),
                _ => FilterResult.Error("不明なフィルターモードです。")
            };
        }

        private FilterResult ProcessSelectionFilter(IEnumerable<ClassNode> rootNodes, bool displaySammary)
        {
            var selectedNames = GetCheckedClassNames(rootNodes.ToList(), "");
            if (selectedNames.Count == 0)
            {
                return FilterResult.Error("クラスを1つ以上選択してください。");
            }

            var displaySetting = new SelectionDisplaySetting(selectedNames, displaySammary);
            var logMessage = $"{selectedNames.Count}個のクラスを出力しました";

            return FilterResult.Success(displaySetting, logMessage);
        }

        private FilterResult ProcessDistanceFilter(IEnumerable<ClassNode> rootNodes, int forward, int backward, bool displaySammary)
        {
            // 距離の取得ロジックは元のコードから移植が必要
            // 現在は簡略化した実装
            var selectedNames = GetCheckedClassNames(rootNodes.ToList(), "");
            if (selectedNames.Count != 1)
            {
                return FilterResult.Error("距離フィルターは1つのクラスのみ選択してください。");
            }

            var displaySetting = new DistanceDisplaySetting(selectedNames[0], forward, backward, displaySammary);
            var logMessage = $"選択クラス: {selectedNames[0]}(依存先距離: {forward}、依存元距離: {backward})を出力しました。";

            return FilterResult.Success(displaySetting, logMessage);
        }

        private List<string> GetCheckedClassNames(List<ClassNode> nodes, string prefix)
        {
            var list = new List<string>();

            foreach (var node in nodes)
            {
                var fullPath = string.IsNullOrEmpty(prefix) ? node.Name : $"{prefix}.{node.Name}";

                if (node.IsChecked && node.Children.Count == 0)
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

