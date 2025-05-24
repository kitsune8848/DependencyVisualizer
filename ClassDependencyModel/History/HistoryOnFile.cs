using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DependencyAnalyzer.Interface;

namespace DependencyAnalyzer.History
{
    public class HistoryOnFile: IHistory
    {

        private readonly string historyFilePath;

        public HistoryOnFile(string historyFilePath = "history.txt")
        {
            this.historyFilePath = historyFilePath;
        }

        public void SetPreviousSourcePath(string sourcePath)
        {
            try
            {
                File.WriteAllText(historyFilePath, sourcePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[エラー] 履歴ファイルの保存に失敗: {ex.Message}");
            }
        }

        public string GetPreviousSourcePath()
        {
            try
            {
                if (File.Exists(historyFilePath))
                {
                    return File.ReadAllText(historyFilePath);
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[エラー] 履歴ファイルの読み取りに失敗: {ex.Message}");
                return null;
            }
        }
    }
}
