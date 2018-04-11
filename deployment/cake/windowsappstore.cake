public string GetArtifactsDirectory(string outputRootDirectory)
{
	// 1 directory up since we want to turn "/output/release" into "/output/"
	var artifactsDirectoryString = string.Format("{0}/..", outputRootDirectory);
	var artifactsDirectory = MakeAbsolute(Directory(artifactsDirectoryString)).FullPath;

	return artifactsDirectory;
}

public string GetAppxUploadFileName(string artifactsDirectory, string solutionName, string versionMajorMinorPatch)
{
	var appxUploadSearchPattern = artifactsDirectory + string.Format("/{0}_{1}.0_*.appxupload", solutionName, versionMajorMinorPatch);

	Information("Searching for appxupload using '{0}'", appxUploadSearchPattern);

	var filesToZip = GetFiles(appxUploadSearchPattern);

	Information("Found '{0}' files to upload", filesToZip.Count);

	var appxUploadFile = filesToZip.FirstOrDefault();
	if (appxUploadFile == null)
	{
		return null;
	}
	
    var appxUploadFileName = appxUploadFile.FullPath;
	return appxUploadFileName;
}