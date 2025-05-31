using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DependencyAnalyzer.DataStructure;
using DependencyAnalyzer.Interface;
using DependencyAnalyzer.PlantUml;

namespace DependencyAnalyzer.Controller
{
    /// <summary>
    /// 邪悪なる神クラス
    /// ああああああああああああああああああああああああああああああああああああああああああああああああああああ
    /// いいいいいいいいいいいい
    /// いいいいいいいいいいいいいいいいいいいいいいいいいいい
    /// </summary>
    public class GodController
    {
        private ISolutionAnalyzer solutionAnalyzer;
        private IUmlGenerator generatorUml;
        private Dictionary<string, ClassDependency> classMap;
        private IHistory history;
        private IViewerOpener opener;

        public GodController(IUmlGenerator generatorUml, ISolutionAnalyzer solutionAnalyzer, IHistory history, IViewerOpener opener)
        {
            this.generatorUml = generatorUml;
            this.solutionAnalyzer = solutionAnalyzer;
            this.history = history;
            this.opener = opener;
        }

        public string GetPreviousSorceFilePath()
        {
            return history.GetPreviousSourcePath();
        }

        public void OpenViewer(string filePath)
        {
            this.opener.Open(filePath);
        }


        public async Task<ClassNameSet> AnalyzeClassesAsync(string sorcePath)
        {

            classMap = await solutionAnalyzer.AnalyzeAsync(sorcePath).ConfigureAwait(false);

            Console.WriteLine("全クラス数: " + classMap.Count);

            var classNames = ClassNameSet.Create(classMap);
            foreach (var className in classNames.Names)
            {
                Console.WriteLine(className);
            }

            history.SetPreviousSourcePath(sorcePath);

            return classNames;
        }
        public async Task GenerateUmlAsync(IDisplaySetting displaySetting, string filePath)
        {
            if (classMap == null || classMap.Count == 0)
            {
                Console.WriteLine("クラスマップが未解析です。まず AnalyzeClassesAsync を実行してください。");
                return;
            }
            try
            {
                switch (displaySetting)
                {
                    case DistanceDisplaySetting distanceSetting:
                        generatorUml.Generate(classMap, filePath, distanceSetting);
                        break;

                    case SelectionDisplaySetting selectionSetting:
                        generatorUml.Generate(classMap, filePath, selectionSetting);
                        break;

                    default:
                        throw new ArgumentException("未対応の displaySetting タイプです");
                }
                Console.WriteLine($"UML ファイルが {filePath} に出力されました。");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UML 生成中にエラーが発生しました: {ex.Message}");
            }
        }

        public async Task GenerateUmlAsync(DistanceDisplaySetting displaySetting, string filePath)
        {
            if (classMap == null || classMap.Count == 0)
            {
                Console.WriteLine("クラスマップが未解析です。まず AnalyzeClassesAsync を実行してください。");
                return;
            }

            try
            {
                generatorUml.Generate(classMap, filePath, displaySetting);
                Console.WriteLine($"UML ファイルが {filePath} に出力されました。");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UML 生成中にエラーが発生しました: {ex.Message}");
            }
        }


        public async Task GenerateUmlAsync(SelectionDisplaySetting displaySetting, string filePath)
        {
            if (classMap == null || classMap.Count == 0)
            {
                Console.WriteLine("クラスマップが未解析です。まず AnalyzeClassesAsync を実行してください。");
                return;
            }

            try
            {
                generatorUml.Generate(classMap, filePath, displaySetting);
                Console.WriteLine($"UML ファイルが {filePath} に出力されました。");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UML 生成中にエラーが発生しました: {ex.Message}");
            }
        }


    }
}
