using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DependencyAnalyzer.VSCodeView
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using DependencyAnalyzer.Interface;

    public class VSCodeViewer : IViewerOpener
    {
        public void Open(string filePath)
        {
            // VSCodeのworkspaceルート（作業ディレクトリ）を取得
            string currentDir = Directory.GetCurrentDirectory();

            // 絶対パスに変換
            string fullPath = Path.Combine(currentDir, filePath);

            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"ファイルが存在しません: {fullPath}");
                return;
            }

            // codeコマンドを起動してファイルを開く
            var psi = new ProcessStartInfo
            {
                FileName = "code",  // VSCodeのコマンド
                Arguments = $"\"{fullPath}\"",
                UseShellExecute = true,
                CreateNoWindow = false,
            };

            try
            {
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"VSCodeの起動に失敗しました: {ex.Message}");
            }
        }
    }

}
