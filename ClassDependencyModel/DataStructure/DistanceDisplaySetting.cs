using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DependencyAnalyzer.Interface;

namespace DependencyAnalyzer.DataStructure
{
    public class DistanceDisplaySetting : IDisplaySetting
    {
        public string RootName { get; private set; }
        public int DistanceDisplayDependency { get; private set; }
        public int DistanceDisplayDependent { get; private set; }
        public bool DisplaySammary {  get; private set; }

        public DistanceDisplaySetting(
            string rootName,
            int distanceDisplayDependency,
            int distanceDisplayDependent,
            bool displaySammary)
        {
            RootName = rootName;
            DistanceDisplayDependency = distanceDisplayDependency;
            DistanceDisplayDependent = distanceDisplayDependent;
            DisplaySammary = displaySammary;
        }
    }

}
