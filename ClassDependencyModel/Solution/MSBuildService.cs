using Microsoft.Build.Locator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DependencyAnalyzer.SolutionAna
{
    public class MSBuildService 
    {
        public void Register()
        {
            if (MSBuildLocator.IsRegistered)
            {
                Console.WriteLine("MSBuild already registered.");
                return;
            }

            try
            {
                Console.WriteLine("Registering MSBuild...");

                // 利用可能なインスタンスを検索
                var instances = MSBuildLocator.QueryVisualStudioInstances().ToArray();

                if (instances.Length > 0)
                {
                    // 最新のVisual Studioインスタンスを選択
                    var bestInstance = instances
                        .OrderByDescending(i => i.Version)
                        .First();

                    Console.WriteLine($"Using: {bestInstance.Name} {bestInstance.Version}");
                    Console.WriteLine($"Path: {bestInstance.MSBuildPath}");

                    MSBuildLocator.RegisterInstance(bestInstance);
                }
                else
                {
                    Console.WriteLine("No Visual Studio instances found. Using defaults...");
                    //MSBuildLocator.RegisterDefaults();
                    // MSBuildのパスを明示的に指定
                    MSBuildLocator.RegisterMSBuildPath(@"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin");
                }

                Console.WriteLine("MSBuild registered successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MSBuild registration failed: {ex.Message}");
                throw;
            }
        }
    }
}
