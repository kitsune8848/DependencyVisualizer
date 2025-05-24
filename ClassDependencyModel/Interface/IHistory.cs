using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DependencyAnalyzer.Interface
{
    public interface IHistory
    {
        void SetPreviousSourcePath(string sourcePath);
        string GetPreviousSourcePath();
    }
}
