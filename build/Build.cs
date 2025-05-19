using System;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using static Nuke.Common.Tools.MSBuild.MSBuildTasks;
using Nuke.Common.Tools.MSBuild;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Git;
using Nuke.Common.Tools.DotNet;
using System.Collections.Generic;
using static Nuke.Common.IO.AbsolutePathExtensions;
using System.Diagnostics;
using Nuke.Common.Utilities.Collections;
using System.Runtime.CompilerServices;


[GitHubActions(
	"continuous",
	GitHubActionsImage.WindowsLatest,
	//ConfigOptions.Debug, ConfigOptions.Release,
	OnPushBranchesIgnore = new[] { "trash" },
	//OnPushBranchesIgnore = new[] { MasterBranch, ReleaseBranchPrefix + "/*" },
	Submodules = GitHubActionsSubmodules.Recursive,
	FetchDepth = 0,
	PublishArtifacts = true,
	InvokedTargets = new[] { nameof(Pack) }
)]
class Build : NukeBuild {
	/// Support plugins are available for:
	///   - JetBrains ReSharper        https://nuke.build/resharper
	///   - JetBrains Rider            https://nuke.build/rider
	///   - Microsoft VisualStudio     https://nuke.build/visualstudio
	///   - Microsoft VSCode           https://nuke.build/vscode

	public static int Main() => Execute<Build>(x => x.Compile);
	protected override void OnBuildInitialized() {

		base.OnBuildInitialized();
		ProcessTasks.DefaultLogInvocation = true;
		ProcessTasks.DefaultLogOutput = true;
		Serilog.Log.Information("IsLocalBuild           : {0}", IsLocalBuild.ToString());

		Serilog.Log.Information("Informational   Version: {0}", InformationalVersion);
		Serilog.Log.Information("Assembl Version  Version: {0}", Version);

	}
	[GitRepository] GitRepository GitRepository;
	[GitVersion(NoFetch = true, NoCache = true)] readonly GitVersion GitVersion;
	[Solution(GenerateProjects = true)] readonly Solution Solution = null!;
	AbsolutePath OutputDirectory => RootDirectory / "final";
	[CI] readonly GitHubActions GitHubActions;

	[Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
	readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;
	string Version => _version ??= Solution.ProductionStackTraceStd.GetProperty("Version")+ "." + GitVersion.CommitsSinceVersionSource;
	string _version;
	string InformationalVersion => GitVersion?.InformationalVersion ?? "1.0.0";
	private IReadOnlyCollection<Output> OurMSBuild(Func<MSBuildSettings, MSBuildSettings> action, Project ScopeToSpecificProject = null) {
		var toolsPath = MSBuildToolPathResolver.Resolve(MSBuildVersion.VS2022, MSBuildPlatform.x64);

		MSBuildSettings s = new MSBuildSettings();
		if (ScopeToSpecificProject != null)
			s = s.SetProjectFile(ScopeToSpecificProject);
		else
			s = s.SetSolutionFile(Solution);
		s = s.SetProcessToolPath(toolsPath)
		.SetConfiguration(Configuration.ToString())
		.SetMSBuildPlatform(MSBuildPlatform.x64)
		.SetVerbosity(MSBuildVerbosity.Normal)
		;

		s = action(s);
		return MSBuild(s);
	}


	Target Clean => _ => _
		.Before(Restore)
		.Executes(() => {
			OutputDirectory.CreateOrCleanDirectory();
		});

	Target Restore => _ => _
		.Executes(() => {
			OurMSBuild(s => s.SetRestore(true).SetTargets("restore"));
		});

	//Target Compile => _ => _
	//    .DependsOn(Restore)
	//    .Executes(() =>
	//    {
	//    });
	Target Compile => _ => _
		.DependsOn(Restore)
		.Executes(() => {
			OutputDirectory.CreateOrCleanDirectory();

			var toBuild = new[] { Solution.ProductionStackTrace, Solution.ProductionStackTraceStd, Solution.ProductionStackTrace_Analyze, Solution.ProductionStackTrace_Analyze_Console, Solution.ProductionStackTrace_Analyze_WPF };
			foreach (var proj in toBuild) {
				var OutDir =  OutputDirectory / "test";
				if (proj == Solution.ProductionStackTrace_Analyze_Console)
					OutDir = OutputDirectory / "console";
				else if (proj == Solution.ProductionStackTrace_Analyze_WPF)
					OutDir = OutputDirectory / "wpf";
				var context = Serilog.Log.ForContext("Project", $"Building {proj.Name}");
				context.Information($"Starting build of: {proj.Name}");
				OurMSBuild(s => s
				.SetTargets("Build")
				.SetAssemblyVersion(Version)
				.SetOutDir(OutDir)
				.SetInformationalVersion(InformationalVersion), proj
				);
			}

		});
	Target Pack => _ => _
	.DependsOn(Compile)
	.Produces(OutputDirectory)
		.Executes(() => {
			DotNetPack(_ => _
					.SetProject(Solution.ProductionStackTraceStd)
					.SetOutputDirectory(OutputDirectory / "nuget")
					.SetVersion(Version)


					);





		}
		);
}
