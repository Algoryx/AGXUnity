using AGXUnity.IO;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace AGXUnityEditor.BuildTools
{
  /// <summary>Bundles text metadata required by native AGX kernels, independently of desktop binary copying.</summary>
  public sealed class AndroidRuntimeResourcePackager : IPreprocessBuildWithReport
  {
    public const string AssetPath = "Assets/AGXUnity/Resources/AGXAndroidRuntime.bytes";
    public const string ActivationAssetPath = "Assets/StreamingAssets/" + AndroidRuntimeLicense.StreamingAssetPath;
    public int callbackOrder => -1000;

    public void OnPreprocessBuild( BuildReport report )
    {
      if ( report.summary.platform == BuildTarget.Android ) Package();
    }

    [MenuItem( "AGXUnity/Build/Package Android Runtime Resources" )]
    public static void Package()
    {
      PackageLicense();

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

    /// <summary>Generates the project's Android activation bundle before the APK/AAB is built or signed.</summary>
    public static void GenerateRuntimeActivation( int licenseId, string activationCode )
    {
      byte[] bundle = CreateRuntimeActivationBundle( licenseId, activationCode, AndroidApplicationIdentifier );
      WriteAsset( ActivationAssetPath, bundle );
      PackageLicense();
    }

    /// <summary>Encrypts locally; this does not contact the license server or consume an activation.</summary>
    public static byte[] CreateRuntimeActivationBundle( int licenseId, string activationCode, string applicationIdentifier )
    {
      if ( string.IsNullOrWhiteSpace( applicationIdentifier ) )
        throw new ArgumentException( "Set the Android application identifier before generating runtime activation." );
      string staging = Path.Combine( Path.GetTempPath(), "AGXUnity-AndroidActivation-" + Guid.NewGuid().ToString( "N" ) );
      Directory.CreateDirectory( staging );
      try {
        // This app-specific reference is packaged byte-for-byte with the request.
        // ASCII-compatible content without CRLF or control characters also avoids
        // Windows text-mode hashing differences when activating on Android.
        string reference = Path.Combine( staging, AndroidRuntimeLicense.ReferenceFilename );
        File.WriteAllText( reference, applicationIdentifier + "\n" + Guid.NewGuid().ToString( "N" ), new UTF8Encoding( false ) );
        string request = null;
        if ( !AGXUnity.LicenseManager.GenerateEncryptedRuntime( licenseId, activationCode, staging,
                                                              AndroidRuntimeLicense.ReferenceFilename,
                                                              filename => request = filename ) )
          throw new InvalidOperationException( AGXUnity.LicenseManager.LastOperationError ?? "Android runtime activation generation failed." );
        using ( var memory = new MemoryStream() ) {
          using ( var zip = new ZipArchive( memory, ZipArchiveMode.Create, true ) ) {
            WriteEntry( zip, reference, AndroidRuntimeLicense.ReferenceFilename );
            WriteEntry( zip, request, AndroidRuntimeLicense.ActivationFilename );
            using ( var target = zip.CreateEntry( AndroidRuntimeLicense.ApplicationIdFilename ).Open() ) {
              byte[] identifier = Encoding.UTF8.GetBytes( applicationIdentifier );
              target.Write( identifier, 0, identifier.Length );
            }
          }
          byte[] bundle = memory.ToArray();
          AndroidRuntimeLicense.ValidateActivationBundle( bundle, applicationIdentifier );
          return bundle;
        }
      }
      finally {
        Directory.Delete( staging, true );
      }
    }

    private static string AndroidApplicationIdentifier => PlayerSettings.GetApplicationIdentifier( UnityEditor.Build.NamedBuildTarget.Android );

    private static void PackageLicense()
    {
      if ( !File.Exists( ActivationAssetPath ) )
        throw new BuildFailedException( "No Android runtime activation is configured. Use AGXUnity > License > Runtime Activation Generator in Android mode before building. Expected bundle: " + ActivationAssetPath );
      try {
        AndroidRuntimeLicense.ValidateActivationBundle( File.ReadAllBytes( ActivationAssetPath ), AndroidApplicationIdentifier );
      }
      catch ( System.Exception e ) {
        throw new BuildFailedException( "Invalid Android runtime activation bundle. " + e.Message );
      }
      PlayerSettings.Android.forceInternetPermission = true;
    }

    private static void WriteAsset( string path, byte[] bytes )
    {
      if ( File.Exists( path ) && File.ReadAllBytes( path ).SequenceEqual( bytes ) )
        return;
      Directory.CreateDirectory( Path.GetDirectoryName( path ) );
      File.WriteAllBytes( path, bytes );
      AssetDatabase.ImportAsset( path, ImportAssetOptions.ForceSynchronousImport );
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
