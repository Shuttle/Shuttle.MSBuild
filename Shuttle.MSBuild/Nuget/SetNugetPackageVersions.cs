using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Shuttle.MSBuild
{
	public class SetNugetPackageVersions : Task
	{
		public override bool Execute()
		{
			var openTag = string.IsNullOrEmpty(OpenTag) ? "{" : OpenTag;
			var closeTag = string.IsNullOrEmpty(CloseTag) ? "}" : CloseTag;

			var projectFilePath = ProjectFile.ItemSpec;

			if (!Path.IsPathRooted(projectFilePath))
			{
			    projectFilePath = Path.GetFullPath(projectFilePath);
			}

			if (!File.Exists(projectFilePath))
			{
				Log.LogError("ProjectFile '{0}' does not exist.", projectFilePath);

				return false;
			}

			var files = new List<string>();

			foreach (var file in Files)
			{
				if (File.Exists(file.ItemSpec))
				{
					files.Add(file.ItemSpec);
				}
				else
				{
					Log.LogWarning("File '{0}' does not exist.", file.ItemSpec);
				}
			}

			var projectFile = new ProjectFile(projectFilePath);

			foreach (var package in projectFile.Packages)
			{
				foreach (var file in files)
				{
					File.WriteAllText(file, File.ReadAllText(file).Replace($"{openTag}{package.Name}-version{closeTag}", package.Version));
				}
			}

			return true;
		}

		[Required]
		public ITaskItem[] Files { get; set; }

		[Required]
		public ITaskItem ProjectFile { get; set; }

		public string OpenTag { get; set; }
		public string CloseTag { get; set; }
	}
}