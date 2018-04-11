#l "./continuaci.cake"

var buildServerVariables = ContinuaCI.Environment.Variable;

var appName = defaultProjectName;
var windowsStoreAppId = GetContinuaCIVariable("WindowsStoreAppId", defaultWindowsStoreAppId);
var windowsStoreClientId = GetContinuaCIVariable("WindowsStoreClientId", defaultWindowsStoreClientId);
var windowsStoreClientSecret = GetContinuaCIVariable("WindowsStoreClientSecret", defaultWindowsStoreClientSecret);
var windowsStoreTenantId = GetContinuaCIVariable("WindowsStoreTenantId", defaultWindowsStoreTenantId);

var target = GetContinuaCIVariable("Target", "Default");
var versionMajorMinorPatch = GetContinuaCIVariable("GitVersion_MajorMinorPatch", defaultVersion);
var versionFullSemVer = GetContinuaCIVariable("GitVersion_FullSemVer", defaultVersion);
var versionNuGet = GetContinuaCIVariable("GitVersion_NuGetVersion", defaultVersion);
var solutionName = GetContinuaCIVariable("SolutionName", defaultProjectName);
var configurationName = GetContinuaCIVariable("ConfigurationName", "Release");
var outputRootDirectory = GetContinuaCIVariable("OutputRootDirectory", string.Format("./output/{0}", configurationName));
var nuGetPackageSources = GetContinuaCIVariable("NuGetPackageSources", string.Empty);

var solutionAssemblyInfoFileName = "./src/SolutionAssemblyInfo.cs";
var solutionFileName = string.Format("./src/{0}.Build.sln", solutionName);
var platforms = new Dictionary<string, PlatformTarget>();
//platforms["AnyCPU"] = PlatformTarget.MSIL;
platforms["x86"] = PlatformTarget.x86;
platforms["x64"] = PlatformTarget.x64;
platforms["arm"] = PlatformTarget.ARM;
