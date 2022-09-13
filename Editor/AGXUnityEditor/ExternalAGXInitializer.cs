using System;
using System.IO;
using System.Linq;
using AGXUnity.IO;
using AGXUnity.Utils;
using UnityEngine;
using UnityEditor;

using Environment = AGXUnity.IO.Environment;

namespace AGXUnityEditor
{
  public class ExternalAGXInitializer : ScriptableObject
  {
    public string AGX_DIR         = string.Empty;
    public string AGX_DATA_DIR    = string.Empty;
    public string AGX_PLUGIN_PATH = string.Empty;
    public string[] AGX_BIN_PATH  = new string[] { };

    [NonSerialized]
    public static bool IsApplied = false;

    public bool HasData
    {
      get
      {
        return !string.IsNullOrEmpty( AGX_DIR ) &&
               !string.IsNullOrEmpty( AGX_DATA_DIR ) &&
               !string.IsNullOrEmpty( AGX_PLUGIN_PATH ) &&
               AGX_BIN_PATH.Length > 0;
      }
    }

    public bool ApplyData()
    {
      IsApplied = true;

      Environment.Set( Environment.Variable.AGX_DIR, AGX_DIR );
      Environment.Set( Environment.Variable.AGX_PLUGIN_PATH, AGX_PLUGIN_PATH );
      foreach ( var path in AGX_BIN_PATH ) {
        var dir = new DirectoryInfo( path );
        var isValidPath = dir.Exists || dir.Name.StartsWith( "agxTerrain_" );
        if ( isValidPath ) {
          if ( dir.Exists )
            Environment.AddToPath( path );
        }
        else {
          Debug.LogWarning( $"WARNING: AGX binary path \"{path}\" doesn't exist. This could result in DllNotFoundException when calls are made to AGX Dynamics." );
          Debug.LogWarning( "         Select new AGX Dynamics checkout/install directory using AGXUnity -> Settings -> \"Select AGX Dynamics root folder\" " +
                            "or delete \"Assets/AGXUnity/Editor/Data/AGXInitData.asset.\"" );
        }
      }

      // All binaries should be in path, try initialize agx.
      try {
        AGXUnity.NativeHandler.Instance.Register( null );

        var envInstance = agxIO.Environment.instance();

        for ( int i = 0; i < (int)agxIO.Environment.Type.NUM_TYPES; ++i )
          envInstance.getFilePath( (agxIO.Environment.Type)i ).clear();

        envInstance.getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).pushbackPath( "." );
        envInstance.getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).pushbackPath( AGX_DIR );
        envInstance.getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).pushbackPath( AGX_PLUGIN_PATH );
        envInstance.getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).pushbackPath( AGX_DATA_DIR );
        envInstance.getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).pushbackPath( AGX_DATA_DIR +
                                                                                      Path.DirectorySeparatorChar +
                                                                                      "cfg" );
        envInstance.getFilePath( agxIO.Environment.Type.RUNTIME_PATH ).pushbackPath( AGX_PLUGIN_PATH );
      }
      catch ( Exception ) {
        return false;
      }

      return true;
    }

    public void Clear()
    {
      AGX_DIR         = string.Empty;
      AGX_DATA_DIR    = string.Empty;
      AGX_PLUGIN_PATH = string.Empty;
      AGX_BIN_PATH    = new string[] { };
    }

    public enum AGXDirectoryType
    {
      Unknown,
      Checkout,
      Installed
    }

    public static AGXDirectoryType FindType( DirectoryInfo dir )
    {
      if ( dir == null || !dir.Exists )
        return AGXDirectoryType.Unknown;
      else if ( dir.GetFiles( "AGX_build_settings.txt" ).FirstOrDefault() != null &&
           dir.GetFiles( "unins000.dat" ).FirstOrDefault() != null )
        return AGXDirectoryType.Installed;
      else if ( dir.GetFiles( "CMakeCache.txt" ).FirstOrDefault() != null )
        return AGXDirectoryType.Checkout;
      return AGXDirectoryType.Unknown;
    }

    public static ExternalAGXInitializer Instance
    {
      get
      {
        return EditorSettings.GetOrCreateEditorDataFolderFileInstance<ExternalAGXInitializer>( "/AGXInitData.asset",
                                                                                               () => UserSaidNo = false );
      }
    }

    public static bool UserSaidNo
    {
      get
      {
        return EditorData.Instance.GetStaticData( "ExternalAGXInitializer_UserSaidNo" ).Bool;
      }
      set
      {
        EditorData.Instance.GetStaticData( "ExternalAGXInitializer_UserSaidNo" ).Bool = value;
      }
    }

    public static bool Initialize()
    {
#if UNITY_EDITOR_WIN
      // Dependencies dir set and we're certain setup_env has been executed.
      if ( Environment.IsSet( Environment.Variable.AGX_DEPENDENCIES_DIR ) )
        return true;

      var instance = Instance;

      // Applying already initialized data.
      if ( instance.HasData )
        return instance.ApplyData();

      IsApplied = false;

      UserSaidNo = UserSaidNo ||
                   !EditorUtility.DisplayDialog( "Configure AGX Dynamics",
                                                 "AGX Dynamics binaries and data isn't found - would you " +
                                                 "like to manually select AGX Dynamics root directory?\n\n" +
                                                 "The selected directory may be changed to another under " +
                                                 "AGXUnity -> Settings...",
                                                 "Yes", "No" );
      if ( UserSaidNo )
        return true;

      var agxDir = EditorUtility.OpenFolderPanel( "AGX Dynamics root directory",
                                                  "Assets",
                                                  "" ).Replace( '/', '\\' );
      if ( string.IsNullOrEmpty( agxDir ) ) {
        UserSaidNo = true;
        return false;
      }

      var type = FindType( new DirectoryInfo( agxDir ) );
      var success = false;
      if ( type == AGXDirectoryType.Unknown ) {
        Debug.Log( $"{"ERROR".Color( Color.red )}: Unable to determine directory type as installed or checked out AGX." );
        return false;
      }
      else if ( type == AGXDirectoryType.Checkout )
        success = instance.InitializeCheckout( agxDir );
      else if ( type == AGXDirectoryType.Installed )
        success = instance.InitializeInstalled( agxDir );

      if ( success )
        instance.ApplyData();
      else
        instance.Clear();

      EditorUtility.SetDirty( instance );
      AssetDatabase.SaveAssets();

      return success;
#else
      // No support for local setup in Linux/OSX and we're reaching this during
      // package updates. Avoid users to have to answer "No" to local installs.
      return false;
#endif
    }

    public static void ChangeRootDirectory( DirectoryInfo newAgxDir )
    {
      var type = FindType( newAgxDir );
      if ( type == AGXDirectoryType.Unknown )
        return;

      Environment.Set( Environment.Variable.AGX_DIR, "" );
      Environment.Set( Environment.Variable.AGX_PLUGIN_PATH, "" );
      foreach ( var path in Instance.AGX_BIN_PATH )
        Environment.RemoveFromPath( path );

      Instance.Clear();

      EditorUtility.SetDirty( Instance );
      AssetDatabase.SaveAssets();
      
      var success = false;
      if ( type == AGXDirectoryType.Checkout )
        success = Instance.InitializeCheckout( newAgxDir.FullName );
      else
        success = Instance.InitializeInstalled( newAgxDir.FullName );

      if ( success ) {
        EditorUtility.SetDirty( Instance );
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorApplication.OpenProject( Path.Combine( Application.dataPath, ".." ) );
      }
    }

    private bool InitializeCheckout( string agxDir )
    {
      const int AGX_DEPENDENCIES = 0;
      const int AGXTERRAIN_DEPENDENCIES = 1;
      const int INSTALLED = 2;

      Instance.AGX_DIR = agxDir;

      var cmakeCache = new FileInfo( AGX_DIR + Path.DirectorySeparatorChar + "CMakeCache.txt" );
      var binData = new BinDirData[]
      {
        new BinDirData()
        {
          CMakeKey = "CACHE_DEPENDENCY_DATE:STRING="
        },
        new BinDirData()
        {
          CMakeKey = "TERRAIN_DEPENDENCY_DATE:STRING=",
          IsOptional = true
        },
        new BinDirData()
        {
          CMakeKey = "CMAKE_INSTALL_PREFIX:PATH="
        }
      };
      using ( var stream = cmakeCache.OpenText() ) {
        var line = string.Empty;
        while ( !binData.All( data => data.HasValue ) &&
                ( line = stream.ReadLine()?.Trim() ) != null ) {
          if ( line.StartsWith( "//" ) )
            continue;

          binData.Any( data => data.StoreValue( line ) );
        }
      }

      if ( !binData.All( data => data.IsOptional || data.HasValue ) ) {
        foreach ( var data in binData )
          if ( !data.HasValue )
            Debug.LogError( $"{"ERROR".Color( Color.red )}: {data.CMakeKey}null" );
        return false;
      }

      var dependenciesDir = new DirectoryInfo( AGX_DIR +
                                               Path.DirectorySeparatorChar +
                                               "dependencies" );
      if ( !dependenciesDir.Exists ) {
        Debug.LogError( $"{"ERROR".Color( Color.red )}: Dependencies directory {dependenciesDir.FullName} - doesn't exist." );
        return false;
      }

      binData[ AGX_DEPENDENCIES ].Directory = dependenciesDir.GetDirectories( $"agx_dependencies_{binData[ AGX_DEPENDENCIES ].Value}*" ).FirstOrDefault();
      if ( binData[ AGXTERRAIN_DEPENDENCIES ].HasValue )
        binData[ AGXTERRAIN_DEPENDENCIES ].Directory = dependenciesDir.GetDirectories( $"agxTerrain_dependencies_{binData[ AGXTERRAIN_DEPENDENCIES ].Value}*" ).FirstOrDefault();

      // Handle both absolute and relative CMAKE_INSTALL_PREFIX
      var installPath = binData[ INSTALLED ].Value;
      if ( Path.IsPathRooted( installPath ) )
        binData[ INSTALLED ].Directory = new DirectoryInfo( installPath );
      else
        binData[ INSTALLED ].Directory = new DirectoryInfo( AGX_DIR +
                                                            Path.DirectorySeparatorChar +
                                                            installPath );

      if ( binData.Any( data => !data.IsOptional && ( data.Directory == null || !data.Directory.Exists ) ) ) {
        foreach ( var data in binData )
          if ( !data.IsOptional && ( data.Directory == null || !data.Directory.Exists ) )
            Debug.LogError( $"{"ERROR".Color( Color.red )}: Unable to find directory for key {data.CMakeKey}." );
        return false;
      }

      AGX_BIN_PATH        = ( from data in binData
                              where data.Directory != null
                              select $"{data.Directory.FullName}{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}x64" ).ToArray();
      var installedBinDir = $"{binData[ INSTALLED ].Directory.FullName}{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}x64";
      AGX_PLUGIN_PATH     = $"{installedBinDir}{Path.DirectorySeparatorChar}plugins";
      AGX_DATA_DIR        = $"{installedBinDir}{Path.DirectorySeparatorChar}data";

      return true;
    }

    private bool InitializeInstalled( string agxDir )
    {
      AGX_DIR         = agxDir;
      AGX_DATA_DIR    = $"{AGX_DIR}{Path.DirectorySeparatorChar}data";
      AGX_BIN_PATH    = new string[] { $"{AGX_DIR}{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}x64" };
      AGX_PLUGIN_PATH = $"{AGX_BIN_PATH[ 0 ]}{Path.DirectorySeparatorChar}plugins";

      return true;
    }

    private class BinDirData
    {
      public string CMakeKey         = string.Empty;
      public string Value            = string.Empty;
      public DirectoryInfo Directory = null;
      public bool IsOptional         = false;

      public bool HasValue { get { return !string.IsNullOrEmpty( Value ); } }

      public bool StoreValue( string line )
      {
        if ( !string.IsNullOrEmpty( Value ) || !line.StartsWith( CMakeKey ) )
          return false;

        Value = line.Substring( CMakeKey.Length, line.Length - CMakeKey.Length );

        return true;
      }
    }

    private static string FindCmakeCacheValue( string line, string variable )
    {
      if ( line.StartsWith( variable ) )
        return line.Substring( variable.Length, line.Length - variable.Length );
      return string.Empty;
    }
  }
}
