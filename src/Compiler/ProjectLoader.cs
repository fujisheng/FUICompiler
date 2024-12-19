using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace FUICompiler
{
    internal class ProjectLoader
    {
        internal static async Task<(Project project, Compilation compilation)> Load(BuildParam buildParam)
        {
            if (MSBuildLocator.CanRegister)
            {
                var instance = MSBuildLocator.QueryVisualStudioInstances().OrderByDescending(item => item.Version).First();
                MSBuildLocator.RegisterInstance(instance);
            }

            var workspace = MSBuildWorkspace.Create();
            Console.WriteLine($"Loading solution {buildParam.solutionPath}...");
            var solution = await workspace.OpenSolutionAsync(buildParam.solutionPath);
            var project = solution.Projects.FirstOrDefault(item => item.Name == buildParam.projectName);

            if (project == null)
            {
                throw new Exception($"build error: project {buildParam.projectName} not found in solution {buildParam.solutionPath}");
            }

            var compilation = await project.GetCompilationAsync();
            return (project, compilation);
        }
    }
}
