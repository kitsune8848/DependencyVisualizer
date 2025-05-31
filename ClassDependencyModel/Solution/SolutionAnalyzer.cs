using DependencyAnalyzer.Interface;
using DependencyAnalyzer.SolutionAna;
using DependencyAnalyzer.DataStructure;


namespace DependencyAnalyzer.SolutionAna
{
    public class SolutionAnalyzer:ISolutionAnalyzer
    {
        private readonly MSBuildService msbuildService;
        private readonly SolutionLoader loader;
        private readonly ProjectAnalyzer projectAnalyzer;
        private readonly DependencyLinker linker;

        public SolutionAnalyzer()
        {
            this.msbuildService = new MSBuildService();
            this.loader = new SolutionLoader();
            this.projectAnalyzer = new ProjectAnalyzer();
            this.linker = new DependencyLinker();
        }

        public async Task<Dictionary<string, ClassDependency>> AnalyzeAsync(string solutionPath)
        {
            msbuildService.Register();

            var solution = await loader.LoadSolutionAsync(solutionPath).ConfigureAwait(false);

            var classMap = new Dictionary<string, ClassDependency>();
            var depMap = new Dictionary<string, HashSet<string>>();

            foreach (var project in solution.Projects)
            {
                await projectAnalyzer.AnalyzeAsync(project, classMap, depMap).ConfigureAwait(false);
            }

            linker.Link(classMap, depMap);

            return classMap;
        }
    }
}