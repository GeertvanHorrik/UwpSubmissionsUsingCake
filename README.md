UWP Submission using Cake
=========================

This blog post has been in the makings for about a year now. But due to some technical store issues, it was delayed for a while. But no fear, the time has finally come here!
While working on several UWP apps for the Microsoft store, I found the deployment part very tedious. Especially building the 3 (x86, x64 and ARM) packages with the native toolchain were taking a long time (about 20 minutes).

I had a few ultimate goals while developing UWP apps:

* Get rid of the native compilation
* Automate the store submission

This pipeline (image taken from the build configuration in Continua CI) was the ultimate goal:


# Getting rid of native compilation

Luckily for me, I didn’t have to do a lot of research on this front. Oren Novotny took care of this for the community by investigating this.

To summarize his hard work, you will need to use the following property values:

Property | Value
--- | ---
UapAppxPackageBuildMode | CI
AppxBundlePlatforms | x86|x64|ARM
AppxBundle | Always


# Automate the store deployment

Next up was to automate the deployment to the store. I wanted to be able to trigger a build and only had to check the newly created deployment in the ~~Windows~~ Microsoft Store to submit it. For me, a logical first step was to take a look at [Cake](https://cakebuild.net/), which is an open-source tool (with lots of add-ons) for automating builds. There are several things that need automation in the deployment process:

1. Determine the version number - GitVersion
1. Update the version number in the appxmanifest
1. Build the app for each target platform (x86, x64 and ARM), but without the slow native compilation
1. Create a submission in the Microsoft Store (based on the previous one) and upload the new packages


## Determine the version number

To determine the version number of the app, you can simply tag a specific commit with the version number and use [GitVersion](https://gitversion.readthedocs.io/en/latest/). I won’t go into too much details for this blog post.


## Update the version number in the appxmanifest

At this stage, we have the version number and need to update it. Since this is UWP, updating the AssemblyInfo isn’t sufficient. To overcome this in Cake, one can use the [MagicChunks add-on](https://github.com/sergeyzwezdin/magic-chunks) for Cake:

```
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
```

The `UpdateAppxManifestVersion` manifest is defined in `/deployment/cake/appxmanifest.cake`.


## Building the app

Below is the code to build the app using Cake without native compilation:

```
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
```

## Submitting the app

Unfortunately, due to the level of popularity of the Microsoft Store, there was no add-on yet for submissions. But now there is the [Cake.WindowsAppStore add-on](https://github.com/cake-contrib/Cake.WindowsAppStore), making it very easy to submit the app to the store. The following values are required:

- Windows Store App Id
- Windows Store Client Id
- Windows Store Client Secret
- Windows Store Tenant ID

Check the official guide on [Microsoft Docs](https://docs.microsoft.com/en-us/windows/uwp/monetize/create-and-manage-submissions-using-windows-store-services#prerequisites) on how to obtain these prerequisites.

Below is the Cake task for the submission:

```
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
```

# How to use the example script

## Setting up the scripts

The scripts are built for re-usability. To use the scripts, copy the following items:

* /deployment/**
* /build.cake
* /build.ps1
* /cake.config

Once copied, open `build.cake` in your favorite editor and customer the default values at the top of the file.

If you are not using Continua CI, you can customize `deployment/cake/variables.cake` by retrieving the values from your favorite CI environment.

## Running the scripts

Once the scripts are set up for your app, you can open a `Visual Studio Code` or a `Powershell` instance and run the following command:

```
.\build.ps1 -Target Deploy
```

The following build targets (that make sense) are available:

Target | Description
--- | ---
Build | Build the app and prepare a package for store submission in release mode (but without the native compilation).
Deploy | Depends on `Build`. Once built, it will clone the most recent submission and deploy an update to the store automatically.


# Conclusion

Achievement unlocked! After over 200 manual submissions, I can finally publish app updates automatically via a build server.

All scripts described in this blog post are open source and can be found at https://github.com/GeertvanHorrik/UwpSubmissionsUsingCake


# References

[Continuous Integration for UWP projects – Making Builds Faster](https://oren.codes/2015/12/03/continuous-integration-for-uwp-projects-making-builds-faster/) (Oren Novotny, December 3, 2015)