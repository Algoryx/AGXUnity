using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;

namespace AGXUnityEditor.IO
{
  public class DllToScriptResolver
  {
    public static string[] GetFiles( string directory, string searchPattern, SearchOption searchOption )
    {
      List<string> files = new List<string>();
      var patterns = searchPattern.Split( '|' );
      foreach ( var pattern in patterns ) {
        var filtered = ( from filename
                         in Directory.GetFiles( directory, pattern, searchOption )
                         where !filename.Replace( '\\', '/' ).Contains( "/Assets/AGXUnity/" )
                         select filename ).ToArray();
        files.AddRange( filtered );
      }
      return files.ToArray();
    }

    public static Regex FileIdMatcher = new Regex( @"\{fileID: (?<fileid>[-0-9]+)", RegexOptions.Compiled );

    public static Regex GuidMatcher = new Regex( @" guid: (?<guid>[a-z0-9]+)", RegexOptions.Compiled );

    public bool IsValid { get { return m_typeMapsLoaded; } }

    public DllToScriptResolver()
    {
      try {
        TryLoadTypeMaps();
      }
      catch ( Exception e ) {
        Debug.LogError( "Caught exception while loading data: " + e.Message );
      }
    }

    public int PatchFilesInDirectory( string directory,
                                      SearchOption searchOption = SearchOption.AllDirectories,
                                      bool saveBackup = true )
    {
      var filenames = GetFiles( directory, "*.asset|*.unity|*.prefab|*.controller|*.anim|*.cs", searchOption );
      return PatchFiles( filenames, saveBackup );
    }

    public int PatchFiles( string[] filenames, bool saveBackup = true )
    {
      if ( filenames.Length == 0 )
        return 0;

      int numChanges = 0;
      try {
        float progress = 0.0f;
        foreach ( var filename in filenames ) {
          EditorUtility.DisplayProgressBar( "Updating broken assets for AGXUnity.", Path.GetFileName( filename ), progress );
          numChanges += Convert.ToInt32( PatchFile( filename, saveBackup ) );
          progress += 1.0f / filenames.Length;
        }
      }
      catch ( Exception e ) {
        Debug.LogException( e );
      }

      EditorUtility.ClearProgressBar();

      return numChanges;
    }

    public bool PatchFile( string filename, bool saveBackup = true )
    {
      if ( !IsValid )
        throw new Exception( "Unable to patch file - type maps not properly loaded." );

      var file = new FileInfo( filename );
      if ( !file.Exists )
        throw new FileNotFoundException( "Unable to find file.", filename );

      var changed = false;
      if ( file.Extension == ".cs" )
        changed = TryPatchCsFile( file, saveBackup );
      else
        changed = TryPatchYamlFile( file, saveBackup );

      AssetDatabase.Refresh();

      return changed;
    }

    private Dictionary<int, string> m_oldFileIdTypeMap = null;
    private Dictionary<string, AGXUnityScriptData> m_newNameScriptMap = null;
    private bool m_typeMapsLoaded = false;

    private bool TryPatchCsFile( FileInfo file, bool saveBackup )
    {
      var tmpFile = Path.GetTempFileName();
      var numChanges = 0;
      try {
        using ( var input = file.OpenText() ) {
          using ( var output = new StreamWriter( tmpFile ) ) {
            var line = string.Empty;
            while ( ( line = input.ReadLine() ) != null ) {
              var newLine = line.Replace( "AgXUnity", "AGXUnity" );
              numChanges += Convert.ToInt32( newLine != line );
              output.WriteLine( newLine );
            }
          }
        }
      }
      catch ( Exception e ) {
        File.Delete( tmpFile );
        throw new Exception( "Caught exception while patching script.", e );
      }

      if ( numChanges > 0 ) {
        if ( saveBackup )
          File.Move( file.FullName, file.FullName + ".bak" );
        else
          File.Delete( file.FullName );

        File.Copy( tmpFile, file.FullName );

        Debug.Log( "Patched: " + Utils.MakeRelative( file.FullName, Application.dataPath ) + " with " + numChanges + " changes." );
      }

      File.Delete( tmpFile );

      return numChanges > 0;
    }

    private bool TryPatchYamlFile( FileInfo file, bool saveBackup )
    {
      var tmpFile = Path.GetTempFileName();
      var numChanges = 0;
      try {
        using ( var input = file.OpenText() ) {
          using ( var output = new StreamWriter( tmpFile ) ) {
            var line = string.Empty;
            var lineNum = 0;
            // For all lines in the file.
            while ( ( line = input.ReadLine() ) != null ) {
              ++lineNum;

              var fileIdMatch = FileIdMatcher.Match( line );

              // We've matched "{fileID: %d".
              if ( fileIdMatch.Success ) {
                try {
                  // There are fileID that are larger than Int32 but we can ignore them - ours are Int32.
                  var fileId = Convert.ToInt32( fileIdMatch.Groups[ "fileid" ].Value );
                  if ( fileId == 0 || fileId == 100100000 )
                    throw new Exception( "Known file id." );

                  // Match fileID -> AgXUnity.dll type.
                  if ( m_oldFileIdTypeMap.ContainsKey( fileId ) ) {
                    var guidMatch = GuidMatcher.Match( line );
                    if ( !m_newNameScriptMap.ContainsKey( m_oldFileIdTypeMap[ fileId ] ) ) {
                      Debug.LogWarning( "Unable to find new script: " + m_oldFileIdTypeMap[ fileId ] );
                      throw new Exception( "Removed script?" );
                    }

                    // We've matched " guid: %s".
                    if ( guidMatch.Success ) {
                      var script = m_newNameScriptMap[ m_oldFileIdTypeMap[ fileId ] ];
                      line = FileIdMatcher.Replace( line, @"{fileID: " + script.FileId.ToString() );
                      line = GuidMatcher.Replace( line, @" guid: " + script.Guid );
                      numChanges += 1;
                    }
                    else {
                      Debug.LogWarning( "Matched fileID but guid is not given (pure fileID reference?) on line: " + lineNum );
                      throw new Exception( "Pure fileID reference." );
                    }
                  }
                }
                catch ( Exception ) {
                  // Parse int failed (too large) but we're only interested in the cases where it's an Int32.
                }
              }

              output.WriteLine( line );
            }
          }
        }
      }
      catch ( Exception e ) {
        Debug.LogError( "Caught exception while patching file: " + file.FullName );
        File.Delete( tmpFile );

        throw new Exception( "Exception while patching file.", e );
      }

      if ( numChanges > 0 ) {
        if ( saveBackup )
          File.Move( file.FullName, file.FullName + ".bak" );
        else
          File.Delete( file.FullName );

        File.Copy( tmpFile, file.FullName );

        Debug.Log( "Patched: " + Utils.MakeRelative( file.FullName, Application.dataPath ) + " with " + numChanges + " changes." );
      }

      File.Delete( tmpFile );

      return numChanges > 0;
    }

    private void TryLoadTypeMaps()
    {
      if ( m_typeMapsLoaded )
        return;

      m_oldFileIdTypeMap = new Dictionary<int, string>();
      m_newNameScriptMap = AGXUnityScriptData.CollectAll();

      var path = Utils.AGXUnityEditorDirectoryFull + "/Data/dep_fileID_types.txt";
      var file = new FileInfo( path );
      if ( !file.Exists )
        throw new FileNotFoundException( "Unable to find file which maps fileID to script.", "dep_fileID_types.txt" );

      using ( var input = file.OpenText() ) {
        string line;
        int lineNum = 0;
        while ( ( line = input.ReadLine() ) != null ) {
          ++lineNum;

          if ( line.Length < 3 )
            continue;

          var idType = line.Split( ' ' );
          if ( idType.Length != 2 )
            throw new Exception( "Undefined entry on line: " + lineNum.ToString() );

          var fileId = Convert.ToInt32( idType[ 0 ] );
          var typeName = idType[ 1 ];
          m_oldFileIdTypeMap.Add( fileId, typeName );
        }
      }

      m_typeMapsLoaded = m_oldFileIdTypeMap.Count > 0 && m_newNameScriptMap.Count > 0;
      if ( !m_typeMapsLoaded )
        throw new Exception( "Type maps not properly loaded." );
    }
  }
}
