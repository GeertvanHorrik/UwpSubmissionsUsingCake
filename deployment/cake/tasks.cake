#addin nuget:?package=Newtonsoft.Json&version=11.0.2
#addin nuget:?package=WindowsAzure.Storage&version=9.1.1
#addin nuget:?package=Cake.WindowsAppStore

#l "./appxmanifest.cake"
#l "./windowsappstore.cake"

Information("Running target '{0}'", target);
Information("Using output directory '{0}'", outputRootDirectory);

//-------------------------------------------------------------

Task("RestorePackages")
    .Does(() =>
{
    var solutions = GetFiles("./**/*.sln");
    
    foreach(var solution in solutions)
    {
        Information("Restoring packages for {0}", solution);
        
        var nuGetRestoreSettings = new NuGetRestoreSettings();

        if (!string.IsNullOrWhiteSpace(nuGetPackageSources))
        {
            var sources = new List<string>();

            foreach (var splitted in nuGetPackageSources.Split(new [] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                sources.Add(splitted);
            }
            
            if (sources.Count > 0)
            {
                nuGetRestoreSettings.Source = sources;
            }
        }

        NuGetRestore(solution, nuGetRestoreSettings);
    }
});

//-------------------------------------------------------------

// Note: it might look weird that this is dependent on restore packages,
// but to clean, the msbuild projects must be able to load. However, they need
// some targets files that come in via packages

Task("Clean")
    .IsDependentOn("RestorePackages")
    .Does(() => 
{
    if (DirectoryExists(outputRootDirectory))
    {
        DeleteDirectory(outputRootDirectory, new DeleteDirectorySettings
        {
            Force = true,
            Recursive = true    
        });
    }

    foreach (var platform in platforms)
    {
        Information("Cleaning output for platform '{0}'", platform.Value);

        MSBuild(solutionFileName, configurator =>
            configurator.SetConfiguration(configurationName)
                .SetVerbosity(Verbosity.Minimal)
                .SetMSBuildPlatform(MSBuildPlatform.x86)
                .SetPlatformTarget(platform.Value)
                .WithTarget("Clean"));
    }
});

//-------------------------------------------------------------

Task("UpdateInfo")
    .Does(() =>
{
    Information("Updating assembly info to '{0}'", versionFullSemVer);

    var assemblyInfoParseResult = ParseAssemblyInfo(solutionAssemblyInfoFileName);

    var assemblyInfo = new AssemblyInfoSettings {
        Company = assemblyInfoParseResult.Company,
        Version = versionMajorMinorPatch,
        FileVersion = versionMajorMinorPatch,
        InformationalVersion = versionFullSemVer,
        Copyright = string.Format("Copyright © {0} {1} - {2}", company, startYear, DateTime.Now.Year)
    };

    CreateAssemblyInfo(solutionAssemblyInfoFileName, assemblyInfo);

    var appxManifestFile = string.Format("./src/{0}/Package.appxmanifest", solutionName);
    
    UpdateAppxManifestVersion(appxManifestFile, string.Format("{0}.0", versionMajorMinorPatch));
});

//-------------------------------------------------------------

Task("Build")
    .IsDependentOn("UpdateInfo")
    .IsDependentOn("RestorePackages")
    .Does(() =>
{
    // Important note: we only have to build for ARM, it will auto-build x86 / x64 as well
    var platform = platforms.FirstOrDefault(x => x.Key == "arm");

    var artifactsDirectory = GetArtifactsDirectory(outputRootDirectory);
    var appxUploadFileName = GetAppxUploadFileName(artifactsDirectory, solutionName, versionMajorMinorPatch);

    // If already exists, skip for store upload debugging
    if (appxUploadFileName != null && FileExists(appxUploadFileName))
    {
        Information(string.Format("File '{0}' already exists, skipping build", appxUploadFileName));
        return;
    }

    var msBuildSettings = new MSBuildSettings {
        Verbosity = Verbosity.Quiet, // Verbosity.Diagnostic
        ToolVersion = MSBuildToolVersion.VS2017,
        Configuration = configurationName,
        MSBuildPlatform = MSBuildPlatform.x86, // Always require x86, see platform for actual target platform
        PlatformTarget = platform.Value
    };

    // See https://docs.microsoft.com/en-us/windows/uwp/packaging/auto-build-package-uwp-apps for all the details
    //msBuildSettings.Properties["UseDotNetNativeToolchain"] = new List<string>(new [] { "false" });
    //msBuildSettings.Properties["UapAppxPackageBuildMode"] = new List<string>(new [] { "StoreUpload" });
    msBuildSettings.Properties["UapAppxPackageBuildMode"] = new List<string>(new [] { "CI" });
    msBuildSettings.Properties["AppxBundlePlatforms"] = new List<string>(new [] { string.Join("|", platforms.Keys) });
    msBuildSettings.Properties["AppxBundle"] = new List<string>(new [] { "Always" });
    msBuildSettings.Properties["AppxPackageDir"] = new List<string>(new [] { artifactsDirectory });

    Information("Building project for platform {0}, artifacts directory is '{1}'", platform.Key, artifactsDirectory);

    MSBuild(solutionFileName, msBuildSettings);

    // Recalculate!
    appxUploadFileName = GetAppxUploadFileName(artifactsDirectory, solutionName, versionMajorMinorPatch);
    if (appxUploadFileName == null)
    {
        Error("Couldn't determine the appxupload file using base directory '{0}'", artifactsDirectory);
    }

    Information("Created appxupload file '{0}'", appxUploadFileName, artifactsDirectory);
});

//-------------------------------------------------------------

Task("Deploy")
    .IsDependentOn("Build")
    .Does(() =>
{
    var artifactsDirectory = GetArtifactsDirectory(outputRootDirectory);
    var appxUploadFileName = GetAppxUploadFileName(artifactsDirectory, solutionName, versionMajorMinorPatch);

    CreateWindowsStoreAppSubmission(appxUploadFileName, new WindowsStoreAppSubmissionSettings
    {
        ApplicationId = windowsStoreAppId,
        ClientId = windowsStoreClientId,
        ClientSecret = windowsStoreClientSecret,
        TenantId = windowsStoreTenantId
    });
});

//-------------------------------------------------------------

Task("Default")
    .IsDependentOn("Build");

//-------------------------------------------------------------

RunTarget(target);