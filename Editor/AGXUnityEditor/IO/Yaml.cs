using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;

namespace AGXUnityEditor.IO
{
  public static partial class Utils
  {
    public static class Yaml
    {
      public static Regex FileIdMatcher = new Regex( @"\{fileID: (?<fileid>[-0-9]+)", RegexOptions.Compiled );

      public static Regex GuidMatcher = new Regex( @" guid: (?<guid>[a-z0-9]+)", RegexOptions.Compiled );
    }

    public class YamlScope
    {
      public string Name = string.Empty;
      public List<string> Lines = new List<string>();

      public bool Parse( StreamReader input )
      {
        Name = string.Empty;
        Lines.Clear();

        var line = string.Empty;
        while ( ( line = input.ReadLine() ) != null && ( line.Length == 0 || line[ 0 ] == '%' || line.StartsWith( "--- !" ) ) )
          ;

        if ( line == null )
          return false;

        Name = line.Substring( 0, line.LastIndexOf( ':' ) );
        while ( ( line = input.ReadLine() ) != null && !line.StartsWith( "--- !" ) )
          Lines.Add( line );

        return true;
      }

      public bool IsScript( string guid )
      {
        foreach ( var line in Lines ) {
          if ( !line.StartsWith( "  m_Script:" ) )
            continue;

          var guidMatcher = Yaml.GuidMatcher.Match( line );
          return guidMatcher.Success && guidMatcher.Value.Contains( "guid: " + guid );
        }

        return false;
      }
    }

    public struct YamlEntry
    {
      public string Value;

      public bool TryGet( out bool value )
      {
        value = false;

        try {
          var intVal = Convert.ToInt32( Value );
          if ( intVal < 0 || intVal > 1 )
            throw new FormatException();
          value = intVal == 1;
          return true;
        }
        catch ( Exception ) {
          return false;
        }
      }

      public bool TryGet( out int value )
      {
        value = 0;

        try {
          value = Convert.ToInt32( Value );
          return true;
        }
        catch ( Exception ) {
          return false;
        }
      }

      public bool TryGet( out float value )
      {
        value = 0.0f;

        try {
          value = Convert.ToSingle( Value );
          return true;
        }
        catch ( Exception ) {
          return false;
        }
      }

      public bool TryGet( out double value )
      {
        value = 0.0;

        try {
          value = Convert.ToDouble( Value );
          return true;
        }
        catch ( Exception ) {
          return false;
        }
      }

      public bool TryGet( out Vector3 value )
      {
        value = Vector3.zero;

        try {
          var start = Value.IndexOf( '{' );
          var end = Value.LastIndexOf( '}' );
          if ( end <= start )
            throw new FormatException();

          var strings = Value.Substring( start, end ).Split( ',' );
          var x = Convert.ToSingle( strings[ 0 ].Split( ':' )[ 1 ].Trim() );
          var y = Convert.ToSingle( strings[ 1 ].Split( ':' )[ 1 ].Trim() );
          var z = Convert.ToSingle( strings[ 2 ].Split( ':' )[ 1 ].Trim() );

          value.x = x;
          value.y = y;
          value.z = z;

          return true;
        }
        catch ( Exception ) {
          return false;
        }
      }

      public bool TryGet( out Quaternion value )
      {
        value = Quaternion.identity;

        try {
          var start = Value.IndexOf( '{' );
          var end = Value.LastIndexOf( '}' );
          if ( end <= start )
            throw new FormatException();

          var strings = Value.Substring( start, end ).Split( ',' );
          var x = Convert.ToSingle( strings[ 0 ].Split( ':' )[ 1 ].Trim() );
          var y = Convert.ToSingle( strings[ 1 ].Split( ':' )[ 1 ].Trim() );
          var z = Convert.ToSingle( strings[ 2 ].Split( ':' )[ 1 ].Trim() );
          var w = Convert.ToSingle( strings[ 3 ].Split( ':' )[ 1 ].Trim() );

          value.x = x;
          value.y = y;
          value.z = z;
          value.w = w;

          return true;
        }
        catch ( Exception ) {
          return false;
        }
      }
    }

    public class YamlObject
    {
      public Dictionary<string, YamlEntry> Fields = new Dictionary<string, YamlEntry>();

      public YamlObject( YamlScope scope )
      {
        foreach ( var line in scope.Lines ) {
          var delimiter = line.IndexOf( ':' );
          if ( delimiter < 0 )
            continue;
          var isEmpty = delimiter == line.Length;
          Fields.Add( line.Substring( 0, delimiter ).Trim(),
                      new YamlEntry()
                      {
                        Value = isEmpty ?
                                  string.Empty :
                                  line.Substring( delimiter + 1, line.Length - delimiter - 1 ).Trim()
                      } );
        }
      }
    }

    public static YamlObject[] FindScriptInSceneFile( string sceneFile, string guid, bool searchMultiple = true )
    {
      if ( sceneFile == string.Empty )
        return new YamlObject[] { };

      List<YamlObject> objects = new List<YamlObject>();
      try {
        var file = new FileInfo( sceneFile );
        var scope = new YamlScope();
        using ( var input = file.OpenText() ) {
          while ( scope.Parse( input ) ) {
            if ( scope.IsScript( guid ) ) {
              objects.Add( new YamlObject( scope ) );
              if ( !searchMultiple )
                break;
            }
          }
        }
      }
      catch ( Exception e ) {
        Debug.LogException( e );
      }

      return objects.ToArray();
    }

    public static YamlObject ParseAsset( string path )
    {
      if ( string.IsNullOrEmpty( path ) )
        return null;

      YamlObject @object = null;
      try {
        var file = new FileInfo( path );
        var scope = new YamlScope();
        using ( var input = file.OpenText() ) {
          if ( scope.Parse( input ) )
            @object = new YamlObject( scope );
        }
      }
      catch ( Exception e ) {
        Debug.LogException( e );
      }

      return @object;
    }
  }
}
