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
      // writable working directory and copy the license before any AGX call,
      // including calls that load the native libraries and their initializers.
      var license = Resources.Load<TextAsset>( "AGXAndroidLicense" );
      if ( license == null )
        throw new AGXUnity.Exception( "The packaged Assets/agx.lic is missing. Rebuild the Android player." );
      string workingDirectory = GetWorkingDirectory();
      Directory.CreateDirectory( workingDirectory );
      File.WriteAllBytes( Path.Combine( workingDirectory, "agx.lic" ), license.bytes );
      Directory.SetCurrentDirectory( workingDirectory );
      Debug.Log( "AGX Android license copied to working directory: " + workingDirectory );

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
