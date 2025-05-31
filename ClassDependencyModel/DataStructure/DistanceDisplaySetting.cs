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
        public bool DisplaySummary {  get; private set; }
        public bool DisplayFieldAndMethod { get; private set; }


        public DistanceDisplaySetting(
            string rootName,
            int distanceDisplayDependency,
            int distanceDisplayDependent,
            bool displaySummary,
            bool displayFieldAndMethod)
        {
            RootName = rootName;
            DistanceDisplayDependency = distanceDisplayDependency;
            DistanceDisplayDependent = distanceDisplayDependent;
            DisplaySummary = displaySummary;
            DisplayFieldAndMethod = displayFieldAndMethod;
        }
    }

}
