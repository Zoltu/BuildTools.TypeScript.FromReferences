using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Zoltu.Collections.Generic.NotNull;
using Zoltu.Linq.NotNull;
using System.Collections.Generic;
namespace Zoltu.BuildTools.TypeScript
{
	public class FromReferencesTask : Task
	{
		[Required]
		public String ProjectFullPath
		{
			get { return _projectFullPath; }
			set { _projectFullPath = value; }
		}
		private String _projectFullPath;

		[Required]
		public String LibraryDirectoryFullPath
		{
			get { return _libraryDirectoryFullPath; }
			set { _libraryDirectoryFullPath = value; }
		}
		private String _libraryDirectoryFullPath;

		[Required]
		public bool CopyAll
		{
			get { return _copyAll; }
			set { _copyAll = value; }
		}
		private bool _copyAll;

		public override Boolean Execute()
		{
			try
			{
				Contract.Assume(!String.IsNullOrEmpty(ProjectFullPath));
				Contract.Assume(!String.IsNullOrEmpty(LibraryDirectoryFullPath));

				var project = ProjectRootElement.Open(ProjectFullPath);
				if (project == null)
					throw new NullReferenceException("project");

				var projectDirectoryPath = project.DirectoryPath;
				if (projectDirectoryPath == null)
					throw new NullReferenceException("projectDirectoryPath");

				Directory.CreateDirectory(LibraryDirectoryFullPath);

				var referencedProjects = GetReferencedProjects(project)
					.NotNullToNull()
					.Skip(1)
					.NotNull();

				foreach (var referencedProject in referencedProjects)
				{
					var sourceProjectBasePath = referencedProject.DirectoryPath;
					Contract.Assume(sourceProjectBasePath != null);
					var typeScriptFullPaths = GetTypeScriptItems(referencedProject)
						.NotNullToNull()
						.Select(x => GetTypeScriptFileFullPath(referencedProject, x))
						.NotNull();

					foreach (var typeScriptFullPath in typeScriptFullPaths)
					{
						var sourceDirectoryPath = Path.GetDirectoryName(typeScriptFullPath);
						Contract.Assume(!String.IsNullOrEmpty(sourceDirectoryPath));
						string destinationPath = GetDestinationPath(sourceProjectBasePath, sourceDirectoryPath);
						Directory.CreateDirectory(destinationPath);
						var sourceFileName = Path.GetFileNameWithoutExtension(typeScriptFullPath);
						Contract.Assume(!String.IsNullOrEmpty(sourceFileName));

						CopyFile(sourceDirectoryPath, sourceFileName, destinationPath, ".d.ts", ".d.ts");
						CopyFile(sourceDirectoryPath, sourceFileName, destinationPath, ".js", ".js");
						if (CopyAll)
						{
							var tsSourceFilePath = CopyFile(sourceDirectoryPath, sourceFileName, destinationPath, ".ts", ".ts.source");
							var jsMapFilePath = CopyFile(sourceDirectoryPath, sourceFileName, destinationPath, ".js.map", ".js.map");

							UpdateSourceMap(sourceFileName, jsMapFilePath, tsSourceFilePath);
						}
					};

				}


				return true;
			}
			catch (Exception exception)
			{
				Log.LogErrorFromException(exception);
				return false;
			}
		}

		private string GetDestinationPath(string sourceProjectBasePath, string sourceDirectoryPath)
		{
			Contract.Requires(sourceProjectBasePath != null);
			Contract.Requires(sourceDirectoryPath != null);
			Contract.Assume(sourceDirectoryPath.Length > sourceProjectBasePath.Length);
			Contract.Assume(!String.IsNullOrEmpty(LibraryDirectoryFullPath));
			var rootRelativePath = sourceDirectoryPath.Remove(0, sourceProjectBasePath.Length);
			if (rootRelativePath[0] == '\\')
			{
				rootRelativePath = rootRelativePath.Substring(1);
			}
			return Path.Combine(LibraryDirectoryFullPath, rootRelativePath);
		}
		private static INotNullEnumerable<ProjectRootElement> GetReferencedProjects(ProjectRootElement parentProject)
		{
			Contract.Requires(parentProject != null);
			Contract.Ensures(Contract.Result<INotNullEnumerable<ProjectRootElement>>() != null);

			var projectRootDirectory = parentProject.DirectoryPath;
			var referencedProjects = parentProject.Items
				.NotNull()
				.Where(item => item.ItemType == "ProjectReference")
				.Select(referencedProject => referencedProject.Include)
				.Select(referencedProjectRelativePath => Path.Combine(projectRootDirectory, referencedProjectRelativePath))
				.Select(referencedProjectFullPath => ProjectRootElement.Open(referencedProjectFullPath))
				.SelectMany(GetReferencedProjects);

			return new[] { parentProject }.NotNull().Concat(referencedProjects);
		}

		private static INotNullEnumerable<ProjectItemElement> GetTypeScriptItems(ProjectRootElement project)
		{
			Contract.Requires(project != null);
			Contract.Ensures(Contract.Result<INotNullEnumerable<ProjectItemElement>>() != null);

			return project.Items.NotNull()
				.Where(item => item.ItemType == "TypeScriptCompile")
				.Where(item => item.Include.EndsWith(".ts"))
				.Where(item => !item.Include.EndsWith(".d.ts"));
		}

		private static String GetTypeScriptFileFullPath(ProjectRootElement project, ProjectItemElement typeScriptItem)
		{
			Contract.Requires(project != null);
			Contract.Requires(typeScriptItem != null);
			Contract.Ensures(Contract.Result<String>() != null);
			Contract.Assume(project.DirectoryPath != null);
			Contract.Assume(typeScriptItem.Include != null);

			return Path.Combine(project.DirectoryPath, typeScriptItem.Include);
		}

		private static String CopyFile(String sourceDirectoryPath, String sourceFileName, String destinationDirectory, String oldExtension, String newExtension)
		{
			Contract.Requires(sourceDirectoryPath != null);
			Contract.Requires(!String.IsNullOrEmpty(sourceFileName));
			Contract.Requires(destinationDirectory != null);
			Contract.Requires(oldExtension != null);
			Contract.Requires(newExtension != null);
			Contract.Ensures(Contract.Result<String>() != null);

			var sourceFileNameAndExtension = sourceFileName + oldExtension;
			var destinationFileNameAndExtension = sourceFileName + newExtension;

			var sourceFilePath = Path.Combine(sourceDirectoryPath, sourceFileNameAndExtension);
			Contract.Assume(!String.IsNullOrEmpty(sourceFilePath));
			var destinationFilePath = Path.Combine(destinationDirectory, destinationFileNameAndExtension);
			Contract.Assume(!String.IsNullOrEmpty(destinationFilePath));
			File.Copy(sourceFilePath, destinationFilePath, true);

			return destinationFilePath;
		}

		private static void UpdateSourceMap(String tsSourceFileName, String jsMapPath, String tsSourcePath)
		{
			Contract.Requires(tsSourceFileName != null);
			Contract.Requires(jsMapPath != null);
			Contract.Requires(tsSourcePath != null);
			Contract.Assume(!String.IsNullOrEmpty(jsMapPath));

			var jsMapContents = File.ReadAllText(jsMapPath);
			var pattern = @"""sources"":\[""(.*?)\.ts""\]";
			var replacement = @"""sources"":[""$1.ts.source""]";
			var regex = new Regex(pattern);
			var newJsMapContents = regex.Replace(jsMapContents, replacement);
			File.WriteAllText(jsMapPath, newJsMapContents);
		}
	}
}
