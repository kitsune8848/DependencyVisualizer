using System.Collections.Generic;
using System.Threading.Tasks;
using DependencyAnalyzer.DataStructure;

namespace DependencyAnalyzer.Interface
{
    public interface ISolutionAnalyzer
    {
        /// <summary>
        /// 指定された Visual Studio ソリューションを解析し、クラス依存関係のマップを返す。
        /// </summary>
        /// <param name="solutionPath">.sln ファイルのフルパス</param>
        /// <returns>
        /// クラス名をキーとし、それに対応する ClassDependency オブジェクトを値とする辞書。
        /// </returns>
        Task<Dictionary<string, ClassDependency>> AnalyzeAsync(string solutionPath);
    }
}
