using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using UnityEngine;

namespace AGXUnity.IO
{
  /// <summary>Places AGX component metadata on a real filesystem for Android native task loading.</summary>
  public static class AndroidRuntimeResources
  {
    public static void Prepare()
    {
      // Android's initial process directory may be read-only. Establish a
      // writable working directory and stage licensing before any AGX call,
      // including calls that load the native libraries and their initializers.
      string workingDirectory = GetWorkingDirectory();
      bool hasSavedLicense = File.Exists( Path.Combine( workingDirectory, AndroidRuntimeLicense.ServiceFilename ) );
      AndroidRuntimeLicense.Prepare( workingDirectory, hasSavedLicense ? null : ReadActivationBundle(), Application.identifier );
      Directory.SetCurrentDirectory( workingDirectory );

      var archive = Resources.Load<TextAsset>( "AGXAndroidRuntime" );
      if ( archive == null )
        throw new AGXUnity.Exception( "AGX Android runtime resources are missing. Rebuild with the AGX Android resource packager enabled." );

      byte[] bytes = archive.bytes;
      string version;
      using ( var sha = SHA256.Create() )
        version = BitConverter.ToString( sha.ComputeHash( bytes ) ).Replace( "-", "" ).Substring( 0, 16 );
      string root = Path.Combine( workingDirectory, "agx-runtime", version );
      string complete = Path.Combine( root, ".complete" );
      if ( !File.Exists( complete ) ) {
        Directory.CreateDirectory( root );
        string prefix = Path.GetFullPath( root ) + Path.DirectorySeparatorChar;
        using ( var memory = new MemoryStream( bytes, false ) )
        using ( var zip = new ZipArchive( memory, ZipArchiveMode.Read ) ) {
          foreach ( var entry in zip.Entries ) {
            if ( string.IsNullOrEmpty( entry.Name ) ) continue;
            string destination = Path.GetFullPath( Path.Combine( root, entry.FullName ) );
            if ( !destination.StartsWith( prefix, StringComparison.Ordinal ) )
              throw new InvalidDataException( "Invalid AGX runtime archive entry." );
            Directory.CreateDirectory( Path.GetDirectoryName( destination ) );
            using ( var source = entry.Open() )
            using ( var target = File.Create( destination ) )
              source.CopyTo( target );
          }
        }
        File.WriteAllText( complete, version );
      }

      var environment = agxIO.Environment.instance();
      // Prefer the component metadata compiled into the Android libraries;
      // extracted resources remain available for data files and fallback lookup.
      environment.setEnableEmbeddedComponents( true );
      environment.getFilePath( agxIO.Environment.Type.RUNTIME_PATH ).pushfrontPath( root );
      environment.getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).pushfrontPath( Path.Combine( root, "data" ) );
      environment.getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).pushfrontPath( workingDirectory );
      string header = environment.findComponent( "Physics/RigidBody/ApplyUniformGravity.agxKernel" );
      if ( string.IsNullOrEmpty( header ) )
        throw new AGXUnity.Exception( "AGX Android gravity kernel metadata was not found after extraction." );
      Debug.Log( "AGX Android runtime metadata ready: " + root );
    }

    public static bool InitializeLicense()
    {
      string directory = GetWorkingDirectory();
      bool success = AndroidRuntimeLicense.LoadOrActivate( directory,
                                                          LicenseManager.LoadFile,
                                                          (request, targetDirectory) => LicenseManager.ActivateEncryptedRuntime( request, targetDirectory ) );
      // LicenseManager's activation workaround temporarily adds/removes the
      // current directory. Restore our persistent resource path afterwards.
      var paths = agxIO.Environment.instance().getFilePath( agxIO.Environment.Type.RESOURCE_PATH );
      paths.removeFilePath( directory );
      paths.pushfrontPath( directory );
      LogLicenseStatus();
      if ( !success ) {
        string status = LicenseManager.LastOperationError;
        if ( string.IsNullOrEmpty( status ) )
          status = LicenseManager.Runtime.getStatus();
        if ( string.IsNullOrEmpty( status ) )
          status = "No valid license or runtime activation request was found in private app storage.";
        bool hasSavedLicense = File.Exists( Path.Combine( directory, AndroidRuntimeLicense.ServiceFilename ) );
        Debug.LogError( "AGX Android licensing failed: " + status +
                        ( hasSavedLicense ? " The saved agx.lfx was preserved; inspect the rejected license before attempting recovery."
                                          : " Runtime activation requires Internet access. Restart the app to retry after resolving the error." ) );
      }
      return success;
    }

    private static byte[] ReadActivationBundle()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
      // StreamingAssets live inside the APK. AssetManager provides synchronous
      // access here, before native initialization, without blocking a web request
      // that needs Unity's main thread to advance.
      using ( var player = new AndroidJavaClass( "com.unity3d.player.UnityPlayer" ) )
      using ( var activity = player.GetStatic<AndroidJavaObject>( "currentActivity" ) )
      using ( var assets = activity.Call<AndroidJavaObject>( "getAssets" ) )
      using ( var stream = assets.Call<AndroidJavaObject>( "open", AndroidRuntimeLicense.StreamingAssetPath ) ) {
        IntPtr buffer = AndroidJNI.NewSByteArray( 4096 );
        try {
          using ( var javaBuffer = new AndroidJavaObject( buffer ) )
          using ( var memory = new MemoryStream() ) {
            // An explicit byte[] signature avoids treating the JNI array as a
            // regular Java object when selecting the InputStream.read overload.
            IntPtr read = AndroidJNIHelper.GetMethodID( stream.GetRawClass(), "read", "([B)I" );
            byte[] bytes = new byte[ 4096 ];
            int count;
            while ( ( count = stream.Call<int>( read, javaBuffer ) ) > 0 ) {
              Buffer.BlockCopy( AndroidJNI.FromSByteArray( buffer ), 0, bytes, 0, count );
              memory.Write( bytes, 0, count );
            }
            return memory.ToArray();
          }
        }
        finally {
          AndroidJNI.DeleteLocalRef( buffer );
          stream.Call( "close" );
        }
      }
#else
      return File.ReadAllBytes( Path.Combine( Application.streamingAssetsPath, AndroidRuntimeLicense.StreamingAssetPath ) );
#endif
    }

    private static string GetWorkingDirectory()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
      // getFilesDir is private, app-owned storage. External persistentDataPath
      // can contain files owned by adb's shell user that the player cannot replace.
      using ( var player = new AndroidJavaClass( "com.unity3d.player.UnityPlayer" ) )
      using ( var activity = player.GetStatic<AndroidJavaObject>( "currentActivity" ) )
      using ( var files = activity.Call<AndroidJavaObject>( "getFilesDir" ) )
        return files.Call<string>( "getAbsolutePath" );
#else
      return Application.persistentDataPath;
#endif
    }

    public static void ValidateGravityTasks()
    {
      foreach ( string path in new[] { "Physics.RigidBody.ApplyUniformGravity", "Physics.ParticleSystem.ApplyUniformGravity" } ) {
        using ( var task = agx.Task.load( path ) ) {
          if ( task == null || task.getParameter( "gravity" ) == null )
            throw new AGXUnity.Exception( "AGX Android task metadata is incomplete: " + path + " has no gravity parameter. Simulation creation was stopped to prevent a native crash." );
        }
      }
      Debug.Log( "AGX Android gravity tasks validated." );
    }

    public static void LogLicenseStatus()
    {
      var runtime = agx.Runtime.instance();
      Debug.Log( "AGX Android license: valid=" + runtime.isValid() +
                 ", core=" + runtime.isModuleEnabled( "AgX" ) +
                 ", wires=" + runtime.isModuleEnabled( "AgX-Wires" ) +
                 ", terrain=" + runtime.isModuleEnabled( "AgX-Terrain" ) );
      if ( !runtime.isValid() )
        Debug.LogWarning( "AGX Android license rejected: " + runtime.getStatus() );
    }
  }
}
