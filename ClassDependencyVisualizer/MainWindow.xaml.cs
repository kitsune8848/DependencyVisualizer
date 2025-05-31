using DependencyAnalyzer.DataStructure;
using DependencyAnalyzer.Interface;
using DependencyAnalyzer.PlantUml;
using DependencyAnalyzer.SolutionAna;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using DependencyAnalyzer.Controller;
using DependencyAnalyzer.History;
using DependencyAnalyzer.VSCodeView;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using ClassDependencyVisualizer.Manager;
using System.Windows.Data;
using Microsoft.Build.Tasks;
using System.Xml.Linq;


namespace ClassDependencyVisualizer
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private GodController _controller;
        private LoadingManager _loadingManager;
        private TreeStateManager _treeStateManager;
        private SearchManager _searchManager;
        private FilterManager _filterManager;
        private LogManager _logManager;

        private bool _isViewerOpen = false;
        private const string OutputFilePath = "output.puml";
        private DispatcherTimer _autoReloadTimer;
        private string currentFilterMode = "Selection";



        private ObservableCollection<ClassNode> _rootNodes = new ObservableCollection<ClassNode>();
        public ObservableCollection<ClassNode> RootNodes
        {
            get => _rootNodes;
            set { _rootNodes = value; OnPropertyChanged(nameof(RootNodes)); }
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            InitializeComponents();
            InitializeFromHistory();
        }

        private void InitializeComponents()
        {
            // 依存関係の初期化
            var analyzer = new SolutionAnalyzer();
            var generator = new PlantUmlGenerator();
            var history = new HistoryOnFile();
            var viewer = new VSCodeViewer();
            _controller = new GodController(generator, analyzer, history, viewer);

            // 管理クラスの初期化
            _loadingManager = new LoadingManager(LoadingMessageText);
            _treeStateManager = new TreeStateManager();
            _searchManager = new SearchManager();
            _filterManager = new FilterManager();
            _logManager = new LogManager(LogRichTextBox);

            ShowDisplayMethodFieldCheckBox.IsChecked = true;
        }

        private void InitializeFromHistory()
        {
            var previousPath = _controller.GetPreviousSorceFilePath();
            SolutionPathTextBox.Text = previousPath;

            if (!string.IsNullOrWhiteSpace(previousPath))
            {
                _ = LoadClassTreeAsync();
            }
        }

        #region Event Handlers

        private void PinToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            this.Topmost = true;
            this._logManager.LogInfo("ウィンドウを上部に固定しました。");
        }

        private void PinToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            this.Topmost = false;
            this._logManager.LogInfo("ウィンドウを固定を解除しました");
        }


        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var filePath = SelectSolutionFile();
            if (!string.IsNullOrEmpty(filePath))
            {
                SolutionPathTextBox.Text = filePath;
                _ = LoadClassTreeAsync();
            }
        }

        private void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(SolutionPathTextBox.Text))
            {
                _ = LoadClassTreeAsync();
            }
        }

        private async void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            await ApplyFilterAsync();
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            var keyword = SearchTextBox.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(keyword))
            {
                _logManager.LogInfo("検索キーワードが空です。");
                _searchManager.ClearSearch(RootNodes);
                return;
            }


            var hitCount = _searchManager.SearchAndHighlight(RootNodes, keyword);
            if (hitCount > 0)
            {
                _logManager.LogInfo($"検索キーワード '{keyword}' に一致する項目が {hitCount} 件見つかりました。");
            }
            else
            {
                _logManager.LogInfo($"検索キーワード '{keyword}' に一致する項目は見つかりませんでした。");
                _searchManager.ClearSearch(RootNodes);
            }
        }


        private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = "";
            _searchManager.ClearSearch(RootNodes);

        }

        private void FilterMode_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string tag)
            {
                _filterManager.SetFilterMode(tag);

                DistanceFilterPanel.Visibility = (tag == "Distance") ? Visibility.Visible : Visibility.Collapsed;
                currentFilterMode = tag;

                bool propagate = tag == "Selection"; 
                // ツリー内の全ノードに伝播モードを反映
                foreach (var node in RootNodes)
                {
                    SetPropagateModeRecursive(node, propagate);
                }
            }
        }

        private void ClassNodeCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is ClassNode selectedNode)
            {
                // 一時的にイベントを外す
                checkBox.Checked -= ClassNodeCheckBox_Checked;

                try
                {
                    if (!selectedNode.PropagateCheckToChildren)
                    {
                        ClearCheck(RootNodes);
                    }

                    // 必ずチェックされている状態にする
                    selectedNode.IsChecked = true;
                }
                finally
                {
                    // 再度イベントを追加（確実に戻す）
                    checkBox.Checked += ClassNodeCheckBox_Checked;
                }
            }
        }

        private void ClearCheck(IEnumerable<ClassNode> nodes)
        {
            foreach (var node in nodes)
            {
                ClearCheck(node.Children);
                node.IsChecked = false;
            }
        }

        private void SetPropagateModeRecursive(ClassNode node, bool propagate)
        {
            node.PropagateCheckToChildren = propagate;
            foreach (var child in node.Children)
            {
                SetPropagateModeRecursive(child, propagate);
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            PlaceholderTextBlock.Visibility = string.IsNullOrWhiteSpace(SearchTextBox.Text)
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SearchButton_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
        }
        private void AutoReloadCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(ReloadIntervalTextBox.Text, out int minutes) && minutes > 0)
            {
                StartAutoReload(minutes);
            }
            else
            {
                MessageBox.Show("更新間隔は1以上の整数で入力してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                AutoReloadCheckBox.IsChecked = false;
            }
        }

        private void AutoReloadCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            StopAutoReload();
        }


        // 入力制限: 数字のみ許可
        private void ReloadIntervalTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
        }
        #endregion

        private async Task ApplyFilterAsync()
        {
            if (!ValidateTreeExists()) return;

            // ✅ チェックされたノードがない場合は処理を中断
            var checkedNodes = GetCheckedNodes(RootNodes);
            if (!checkedNodes.Any())
            {
                _logManager.LogInfo("フィルター対象のノードが選択されていません。");
                return;
            }

            if (!int.TryParse(ForwardDistanceComboBox.Text, out int forward) ||
                !int.TryParse(BackwardDistanceComboBox.Text, out int backward))
            {
                _logManager.LogError("距離指定が正しくありません。正の整数を入力してください。");
                return;
            }

            var displaySummary = ShowSummaryCheckBox.IsChecked ?? false;
            var displayFieldAndMethod = ShowDisplayMethodFieldCheckBox.IsChecked ?? true;
            var result = _filterManager.ProcessFilter(RootNodes, forward, backward, displaySummary, displayFieldAndMethod);

            if (!result.IsSuccess)
            {
                _logManager.LogError(result.ErrorMessage);
                return;
            }

            await GenerateAndOpenUmlAsync(result.DisplaySetting, result.LogMessage);
        }


        private string SelectSolutionFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Solution Files (*.sln)|*.sln",
                Title = "ソリューションファイルを選択"
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        private bool ValidateTreeExists()
        {
            if (RootNodes?.Count > 0) return true;

            _logManager.LogError("クラスツリーが空です。クラスを読み込んでください。");
            return false;
        }

        private async Task LoadClassTreeAsync()
        {
            try
            {
                _loadingManager.StartLoading("ファイル解析中");
                await LoadAndDisplayClassTreeAsync();
                await ApplyFilterAsync();  // ← フィルターを自動適用
            }
            catch (Exception ex)
            {
                _logManager.LogError($"クラスツリーの読み込み中にエラーが発生しました: {ex.Message}");
            }
            finally
            {
                _loadingManager.StopLoading();
            }
        }

        private async Task LoadAndDisplayClassTreeAsync()
        {
            var solutionPath = SolutionPathTextBox.Text;
            _logManager.LogInfo($"slnファイルからクラスツリーの読み込みを開始しました。{solutionPath}");

            // 現在の状態を保存
            var expandStates = _treeStateManager.GetExpandStates(RootNodes);
            var checkStates = _treeStateManager.GetCheckStates(RootNodes);

            // クラス解析
            var classNames = await _controller.AnalyzeClassesAsync(solutionPath);
            _logManager.LogInfo($"slnファイルから読み込みを終了しました。クラス数: {classNames.Names.Count}");

            // ツリー構築
            _logManager.LogInfo("表示ツリーを構成中");
            var newRootNodes = BuildClassTree(classNames.Names);
            UpdateRootNodes(newRootNodes);

            // 状態復元
            _treeStateManager.RestoreExpandStates(RootNodes, expandStates);
            _treeStateManager.RestoreCheckStates(RootNodes, checkStates);
            ExpandInitialNodes();

            _logManager.LogInfo("表示ツリーの構成終わり");
        }

        private ObservableCollection<ClassNode> BuildClassTree(List<string> classNames)
        {
            var classNodes = ClassNode.BuildTree(classNames, currentFilterMode);
            var allNode = new ClassNode { Name = "All", IsExpanded = true };

            foreach (var node in classNodes)
                allNode.Children.Add(node);

            return new ObservableCollection<ClassNode> { allNode };
        }

        private void UpdateRootNodes(ObservableCollection<ClassNode> newNodes)
        {
            RootNodes.Clear();
            foreach (var node in newNodes)
                RootNodes.Add(node);
        }

        private void ExpandInitialNodes()
        {
            foreach (var root in RootNodes)
            {
                root.IsExpanded = true;
                foreach (var child in root.Children)
                    child.IsExpanded = true;
            }
        }

        private async Task GenerateAndOpenUmlAsync(IDisplaySetting displaySetting, string logMessage)
        {
            await _controller.GenerateUmlAsync(displaySetting, OutputFilePath);

            if (!_isViewerOpen)
            {
                _controller.OpenViewer(OutputFilePath);
                _isViewerOpen = true;
            }

            _logManager.LogInfo(logMessage);
        }


        private void StartAutoReload(int minutes)
        {
            StopAutoReload();

            _autoReloadTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(minutes)
            };
            _autoReloadTimer.Tick += async (s, ev) =>
            {
                if (!string.IsNullOrWhiteSpace(SolutionPathTextBox.Text))
                {
                    await LoadClassTreeAsync();
                }
            };
            _autoReloadTimer.Start();
            _logManager.LogInfo($"自動更新を開始しました。{minutes}分ごとに更新します。");
        }

        private void StopAutoReload()
        {
            if (_autoReloadTimer != null)
            {
                _autoReloadTimer.Stop();
                _autoReloadTimer = null;
                _logManager.LogInfo("自動更新を停止しました。");
            }
        }

        private List<ClassNode> GetCheckedNodes(IEnumerable<ClassNode> nodes)
        {
            var result = new List<ClassNode>();
            foreach (var node in nodes)
            {
                if (node.IsChecked)
                    result.Add(node);

                result.AddRange(GetCheckedNodes(node.Children));
            }
            return result;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (value is Visibility v && v == Visibility.Visible);
        }
    }
}
