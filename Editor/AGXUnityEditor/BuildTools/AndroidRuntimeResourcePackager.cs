using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace AGXUnityEditor.BuildTools
{
  /// <summary>Bundles text metadata required by native AGX kernels, independently of desktop binary copying.</summary>
  public sealed class AndroidRuntimeResourcePackager : IPreprocessBuildWithReport
  {
    public const string AssetPath = "Assets/AGXUnity/Resources/AGXAndroidRuntime.bytes";
    public const string LicenseSourcePath = "Assets/agx.lic";
    public const string LicenseAssetPath = "Assets/AGXUnity/Resources/AGXAndroidLicense.bytes";
    public int callbackOrder => -1000;

    public void OnPreprocessBuild( BuildReport report )
    {
      if ( report.summary.platform == BuildTarget.Android ) Package();
    }

    [MenuItem( "AGXUnity/Build/Package Android Runtime Resources" )]
    public static void Package()
    {
      // Package the project's license independently of the metadata cache so
      // changes to Assets/agx.lic always reach the next Android build.
      if ( !File.Exists( LicenseSourcePath ) )
        throw new BuildFailedException( "Assets/agx.lic is missing. The Android player requires this license in its working directory." );
      byte[] license = File.ReadAllBytes( LicenseSourcePath );
      if ( !File.Exists( LicenseAssetPath ) || !File.ReadAllBytes( LicenseAssetPath ).SequenceEqual( license ) ) {
        Directory.CreateDirectory( Path.GetDirectoryName( LicenseAssetPath ) );
        File.WriteAllBytes( LicenseAssetPath, license );
        AssetDatabase.ImportAsset( LicenseAssetPath, ImportAssetOptions.ForceSynchronousImport );
      }

      string sourceRoot = FindSourceRoot();
      if ( sourceRoot == null ) {
        if ( File.Exists( AssetPath ) ) return;
        throw new BuildFailedException( "Cannot find AGX Components/agx. Package the Android runtime resources from a matching AGX installation before building." );
      }
      string components = Path.Combine( sourceRoot, "Components", "agx" );
      var extensions = new[] { ".agxEntity", ".agxKernel", ".agxTask", ".agxComponent", ".agxEvent", ".agxValue", ".xml" };
      byte[] bytes;
      int count = 0;
      using ( var memory = new MemoryStream() ) {
        using ( var zip = new ZipArchive( memory, ZipArchiveMode.Create, true ) ) {
          foreach ( string file in Directory.EnumerateFiles( components, "*", SearchOption.AllDirectories ).OrderBy( path => path, StringComparer.Ordinal ) ) {
            if ( !extensions.Contains( Path.GetExtension( file ) ) ) continue;
            WriteEntry( zip, file, "Components/agx/" + Path.GetRelativePath( components, file ).Replace( '\\', '/' ) );
            count++;
          }
          string materials = Path.Combine( sourceRoot, "data", "MaterialLibrary", "TerrainMaterials" );
          if ( Directory.Exists( materials ) )
            foreach ( string file in Directory.EnumerateFiles( materials, "*.json" ).OrderBy( path => path, StringComparer.Ordinal ) )
              WriteEntry( zip, file, "data/MaterialLibrary/TerrainMaterials/" + Path.GetFileName( file ) );
          string configuration = Path.Combine( sourceRoot, "data", "cfg" );
          if ( Directory.Exists( configuration ) )
            foreach ( string file in Directory.EnumerateFiles( configuration, "*", SearchOption.AllDirectories ).OrderBy( path => path, StringComparer.Ordinal ) ) {
              if ( Path.GetExtension( file ) != ".cfg" && Path.GetExtension( file ) != ".schema" ) continue;
              WriteEntry( zip, file, "data/cfg/" + Path.GetRelativePath( configuration, file ).Replace( '\\', '/' ) );
            }
        }
        bytes = memory.ToArray();
      }
      if ( !File.Exists( AssetPath ) || !File.ReadAllBytes( AssetPath ).SequenceEqual( bytes ) ) {
        File.WriteAllBytes( AssetPath, bytes );
        AssetDatabase.ImportAsset( AssetPath, ImportAssetOptions.ForceSynchronousImport );
      }
      UnityEngine.Debug.Log( "Packaged " + count + " AGX Android component metadata files." );
    }

    private static void WriteEntry( ZipArchive zip, string file, string path )
    {
      var entry = zip.CreateEntry( path, CompressionLevel.Optimal );
      entry.LastWriteTime = new DateTimeOffset( 2000, 1, 1, 0, 0, 0, TimeSpan.Zero );
      using ( var source = File.OpenRead( file ) )
      using ( var destination = entry.Open() ) source.CopyTo( destination );
    }

    private static string FindSourceRoot()
    {
      string plugin = AGXUnity.IO.Environment.Get( AGXUnity.IO.Environment.Variable.AGX_PLUGIN_PATH );
      string install = AGXUnity.IO.Environment.AGXDynamicsPath;
      foreach ( string candidate in new[] { install, plugin } ) {
        if ( string.IsNullOrEmpty( candidate ) ) continue;
        var directory = new DirectoryInfo( candidate );
        for ( int i = 0; directory != null && i < 8; i++, directory = directory.Parent )
          if ( File.Exists( Path.Combine( directory.FullName, "Components", "agx", "Referenced.agxEntity" ) ) )
            return directory.FullName;
      }
      return null;
    }
  }
}
