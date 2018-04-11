#addin "nuget:?package=MagicChunks"

public void UpdateAppxManifestVersion(string path, string version)
{
	Information("Updating AppxManifest version @ '{0}' to '{1}'", path, version);

	TransformConfig(path,
          new TransformationCollection {
            { "Package/Identity/@Version", version }
          });	
}