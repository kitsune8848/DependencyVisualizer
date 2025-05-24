using System;
using System.Linq;
using System.Threading.Tasks;
using DependencyAnalyzer.PlantUml;
using DependencyAnalyzer;
using DependencyAnalyzer.SolutionAna;
using DependencyAnalyzer.History;
using DependencyAnalyzer.Controller;
using DependencyAnalyzer.VSCodeView;


namespace DependencyAnalyzer.Client
{
    class Program
    {
        static async Task Main(string[] args) 
        { 
            var controller = new GodController(new PlantUmlGenerator(), new SolutionAnalyzer(), new HistoryOnFile(), new VSCodeViewer());
            await controller.AnalyzeClassesAsync(args[0]);
        }
    }
}
