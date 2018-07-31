using System;
using System.ComponentModel.Design;
using System.IO;
using System.Text;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Shuttle.MSBuild.VSIX
{
    internal sealed class ConfigureShuttleProject
    {
        private const string AssemblyInfoTemplate =
            @"using System.Reflection;
using System.Runtime.InteropServices;

#if NET46
[assembly: AssemblyTitle("".NET Framework 4.6"")]
#endif

#if NET461
[assembly: AssemblyTitle("".NET Framework 4.6.1"")]
#endif

#if NET462
[assembly: AssemblyTitle("".NET Framework 4.6.2"")]
#endif

#if NET47
[assembly: AssemblyTitle("".NET Framework 4.7"")]
#endif

#if NET471
[assembly: AssemblyTitle("".NET Framework 4.7.1"")]
#endif

#if NETCOREAPP2_0
[assembly: AssemblyTitle("".NET Core 2.0"")]
#endif

#if NETSTANDARD2_0
[assembly: AssemblyTitle("".NET Standard 2.0"")]
#endif

[assembly: AssemblyVersion(""1.0.0.0"")]
[assembly: AssemblyCopyright(""Copyright © Eben Roux {year}"")]
[assembly: AssemblyProduct(""{package-name}"")]
[assembly: AssemblyCompany(""Shuttle"")]
[assembly: AssemblyConfiguration(""Release"")]
[assembly: AssemblyInformationalVersion(""1.0.0"")]
[assembly: ComVisible(false)]
";

        public const int CommandId = 0x0100;
        public static readonly Guid CommandSet = new Guid("2d2c8a94-86e8-4fc8-9f12-1512b8a4a182");
        private static string _extensionPath;
        private readonly Package _package;

        private ConfigureShuttleProject(Package package)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));

            var commandService = ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;

            if (commandService != null)
            {
                var menuItem = new MenuCommand(MenuItemCallback, new CommandID(CommandSet, CommandId));
                commandService.AddCommand(menuItem);
            }
        }

        public static ConfigureShuttleProject Instance { get; private set; }
        private IServiceProvider ServiceProvider => _package;

        public static void Initialize(Package package)
        {
            Instance = new ConfigureShuttleProject(package);

            var codebase = typeof(ConfigureShuttleProject).Assembly.CodeBase;
            var uri = new Uri(codebase, UriKind.Absolute);

            _extensionPath = Path.GetDirectoryName(uri.LocalPath);
        }

        private void MenuItemCallback(object sender, EventArgs e)
        {
            const string title = "Configure Shuttle Project";
            var invoked = false;

            var dte = (DTE) ServiceProvider.GetService(typeof(DTE));
            var activeSolutionProjects = dte.ActiveSolutionProjects as object[];

            if (activeSolutionProjects != null)
            {
                foreach (var activeSolutionProject in activeSolutionProjects)
                {
                    var project = activeSolutionProject as Project;

                    if (project != null)
                    {
                        invoked = true;

                        if (project.Kind != "{9A19103F-16F7-4668-BE54-9A1E7A4F7556}")
                        {
                            VsShellUtilities.ShowMessageBox(
                                ServiceProvider,
                                $"The project '{project.FullName}' does not appear to be a .Net Core project.",
                                title,
                                OLEMSGICON.OLEMSGICON_INFO,
                                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

                            continue;
                        }

                        ConfigureBuildFolder(project);
                    }
                }
            }

            if (!invoked)
            {
                VsShellUtilities.ShowMessageBox(
                    ServiceProvider,
                    "This command may only be executed on a project.",
                    title,
                    OLEMSGICON.OLEMSGICON_INFO,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }

        private void ConfigureBuildFolder(Project project)
        {
            var projectFolder = Path.GetDirectoryName(project.Properties.Item("FullPath").Value.ToString());

            if (string.IsNullOrEmpty(projectFolder))
            {
                throw new ApplicationException("Could not determine project path.");
            }

            var buildFolderProjectItem = FindFolder(project, ".build");
            var buildFolder = Path.Combine(projectFolder, ".build");

            if (buildFolderProjectItem == null)
            {
                buildFolderProjectItem = project.ProjectItems.AddFromDirectory(Path.Combine(projectFolder, ".build"));
            }

            CopyBuildRelatedFile(buildFolder, "Shuttle.MSBuild.dll");
            CopyBuildRelatedFile(buildFolder, "Shuttle.MSBuild.targets");
            ProcessBuildRelatedFile(project, buildFolder, "package.msbuild.template", "package.msbuild");
            ProcessBuildRelatedFile(project, buildFolder, "package.nuspec.template", "package.nuspec");

            buildFolderProjectItem.ProjectItems.AddFromFile(Path.Combine(buildFolder, "Shuttle.MSBuild.dll"));
            buildFolderProjectItem.ProjectItems.AddFromFile(Path.Combine(buildFolder, "Shuttle.MSBuild.targets"));
            buildFolderProjectItem.ProjectItems.AddFromFile(Path.Combine(buildFolder, "package.msbuild"));
            buildFolderProjectItem.ProjectItems.AddFromFile(Path.Combine(buildFolder, "package.nuspec"));

            project.Save();

            OverwriteAssemblyInfo(project, projectFolder);
            ConfigureProjectFile(project, projectFolder);
        }

        private ProjectItem FindFolder(Project project, string name)
        {
            ProjectItem result = null;

            foreach (ProjectItem projectItem in project.ProjectItems)
            {
                if (!projectItem.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result = projectItem;
                break;
            }

            return result;
        }

        private void ConfigureProjectFile(Project project, string projectFolder)
        {
            var projectFilePath = Path.Combine(projectFolder, project.FileName);

            if (!File.Exists(projectFilePath))
            {
                return;
            }

            try
            {
                var result = new StringBuilder();

                using (var sr = new StreamReader(projectFilePath))
                {
                    string line;

                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.Contains("<GenerateAssemblyInfo>"))
                        {
                            continue;
                        }

                        if (line.Contains("<TargetFrameworks>") || line.Contains("<TargetFramework>"))
                        {
                            result.AppendLine(
                                "    <TargetFrameworks>net46;net461;net462;net47;net471;netstandard2.0;netcoreapp2.0;netcoreapp2.1</TargetFrameworks>");
                            result.AppendLine(
                                "    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>");
                        }
                        else
                        {
                            result.AppendLine(line);
                        }
                    }
                }

                File.WriteAllText(projectFilePath, result.ToString());
            }
            catch
            {
            }
        }

        private void OverwriteAssemblyInfo(Project project, string projectFolder)
        {
            var assemblyInfoPath = Path.Combine(projectFolder, "Properties\\AssemblyInfo.cs");

            if (!File.Exists(assemblyInfoPath))
            {
                var folderProjectItem = FindFolder(project, "Properties");
                var path = Path.Combine(projectFolder, "Properties");

                if (folderProjectItem == null)
                {
                    if (Directory.Exists(path))
                    {
                        throw new InvalidOperationException("The 'Properties' folder already exists.");
                    }

                    folderProjectItem = project.ProjectItems.AddFromDirectory(path);

                    File.WriteAllText(assemblyInfoPath,
                        AssemblyInfoTemplate
                            .Replace("{package-name}", project.Name)
                            .Replace("{year}", DateTime.Now.ToString("yyyy")));

                    folderProjectItem.ProjectItems.AddFromFile(assemblyInfoPath);
                }

                return;
            }

            if (File.ReadAllText(assemblyInfoPath).Contains("#if NET"))
            {
                return;
            }

            try
            {
                File.WriteAllText(assemblyInfoPath,
                    AssemblyInfoTemplate.Replace("{package-name}", project.Name)
                        .Replace("{year}", DateTime.Now.ToString("yyyy")));
            }
            catch
            {
            }
        }

        public void CopyBuildRelatedFile(string buildFolder, string fileName)
        {
            var sourceFileName = Path.Combine(_extensionPath, ".build", fileName);
            var targetFileName = Path.Combine(buildFolder, fileName);

            try
            {
                File.Copy(sourceFileName, targetFileName, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[CopyBuildRelatedFile] : could not copy '{0}' tp '{1}' / exception = {2}",
                    sourceFileName,
                    targetFileName, ex.Message);
            }
        }

        private void ProcessBuildRelatedFile(Project project, string buildFolder, string sourceFileName,
            string targetFileName)
        {
            var targetPath = Path.Combine(buildFolder, targetFileName);

            if (File.Exists(targetPath))
            {
                return;
            }

            File.Copy(Path.Combine(_extensionPath, ".build", sourceFileName), targetPath);

            var packageName = project.Name;

            File.WriteAllText(targetPath, File.ReadAllText(targetPath)
                .Replace("{package-name}", packageName));
        }
    }
}