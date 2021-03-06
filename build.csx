#r "packages/Bullseye.1.2.0/lib/netstandard2.0/Bullseye.dll"
#r "packages/SimpleExec.3.0.0/lib/netstandard2.0/SimpleExec.dll"

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using static Bullseye.Targets;
using static SimpleExec.Command;

// version
var versionSuffix = Environment.GetEnvironmentVariable("VERSION_SUFFIX") ?? "-adhoc";
var buildNumber = Environment.GetEnvironmentVariable("BUILD_NUMBER") ?? "000000";
var buildNumberSuffix = versionSuffix == "" ? "" : "-build" + buildNumber;
var version = File.ReadAllText("src/CommonAssemblyInfo.cs")
    .Split(new[] { "AssemblyInformationalVersion(\"" }, 2, StringSplitOptions.RemoveEmptyEntries)[1]
    .Split('\"').First() + versionSuffix + buildNumberSuffix;

// locations
var solution = "./ConfigR.sln";
var logs = "./artifacts/logs";
var vswhere = "packages/vswhere.2.2.11/tools/vswhere.exe";
string msBuild;
var nuspecs = new[] { "./src/ConfigR/ConfigR.nuspec", "./src/ConfigR.Roslyn.CSharp/ConfigR.Roslyn.CSharp.nuspec", };
var output = "./artifacts/output";
var nuget = "./.nuget/v4.3.0/NuGet.exe";
var acceptanceTests = Path.GetFullPath("./tests/ConfigR.Tests.Acceptance.Roslyn.CSharp/bin/Release/ConfigR.Tests.Acceptance.Roslyn.CSharp.dll");
var xunit = "./packages/xunit.runner.console.2.4.0/tools/net46/xunit.console.exe";

// targets
Target("default", DependsOn("pack", "accept"));

Target("logs", () => Directory.CreateDirectory(logs));

Target("restore", () => Run(nuget, $"restore {solution}"));

Target(
    "find-msbuild",
    () => msBuild = $"{Read(vswhere, "-latest -requires Microsoft.Component.MSBuild -property installationPath").Trim()}/MSBuild/15.0/Bin/MSBuild.exe");

Target(
    "build",
    DependsOn("restore", "logs", "find-msbuild"),
    () => Run(
        msBuild,
        $"{solution} /p:Configuration=Release /nologo /m /v:m /nr:false " +
            $"/fl /flp:LogFile={logs}/msbuild.log;Verbosity=Detailed;PerformanceSummary"));

Target("output", () => Directory.CreateDirectory(output));

Target(
    "pack",
    DependsOn("build", "output"),
    nuspecs,
    nuspec =>
    {
        var originalNuspec = $"{nuspec}.original";
        File.Move(nuspec, originalNuspec);
        var originalContent = File.ReadAllText(originalNuspec);
            var content = originalContent.Replace("[0.0.0]", $"[{version}]");
        File.WriteAllText(nuspec, content);
        try
        {
            Run(nuget, $"pack {nuspec} -Version {version} -OutputDirectory {output} -NoPackageAnalysis");
        }
        finally
        {
            File.Delete(nuspec);
            File.Move(originalNuspec, nuspec);
        }
    });

Target(
    "accept",
    DependsOn("build"),
    () => Run(
        xunit, $"{acceptanceTests} -html {acceptanceTests}.TestResults.html -xml {acceptanceTests}.TestResults.xml"));

RunTargets(Args);
