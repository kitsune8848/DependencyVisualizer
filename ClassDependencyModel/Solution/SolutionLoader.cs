using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis;

namespace DependencyAnalyzer.SolutionAna
{
    public class SolutionLoader
    {
        public async Task<Solution> LoadSolutionAsync(string solutionPath)
        {
            // RegisterMSBuild は外で呼ぶ前提
            var workspace = MSBuildWorkspace.Create();
            // エラー監視など含めて既存の処理を移行
            return await workspace.OpenSolutionAsync(solutionPath).ConfigureAwait(false);
        }
    }
}