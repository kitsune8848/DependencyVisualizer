using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DependencyAnalyzer.Interface;

namespace DependencyAnalyzer.DataStructure
{
    public class SelectionDisplaySetting: IDisplaySetting
    {
        public List<string> DisplayClassNames { get; private set; }
        public bool DisplaySammary {  get; private set; }

        public SelectionDisplaySetting(List<string> displayClassNames, bool displaySammary)
        {
            DisplayClassNames = displayClassNames ?? new List<string>();
            DisplaySammary = displaySammary;
        }
    }
}
