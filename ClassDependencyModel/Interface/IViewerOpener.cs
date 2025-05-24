using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DependencyAnalyzer.Interface
{
    public interface IViewerOpener
    {
        public void Open(string filePath);
    }
}
