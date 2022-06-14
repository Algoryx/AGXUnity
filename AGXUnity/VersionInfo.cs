using System;
using System.IO;
using UnityEngine;

namespace AGXUnity
{
  public struct VersionInfo : IComparable<VersionInfo>
  {
    public struct ReleaseType
    {
      public bool Official;

      public int Alpha;
      public int Beta;
      public int Rc;

      public bool IsAlpha { get { return Alpha >= 0; } }
      public bool IsBeta { get { return Beta >= 0; } }
      public bool IsRc { get { return Rc >= 0; } }


      public bool IsPreview { get { return IsAlpha || IsBeta || IsRc; } }

      public bool IsValid { get { return Official || IsPreview; } }

      public override string ToString()
      {
        if ( !IsValid || Official )
          return "";

        return IsAlpha ? $"-alpha.{Alpha}" :
               IsBeta  ? $"-beta.{Beta}" :
                         $"-rc.{Rc}";
      }
    }

    public static VersionInfo Invalid = new VersionInfo()
    {
      Major = -1,
      Minor = -1,
      Patch = -1,
      Release = new ReleaseType()
      {
        Official = false,
        Alpha = -1,
        Beta = -1,
        Rc = -1
      },
      GitHash = string.Empty,
      Platform = string.Empty,
      PlatformName = string.Empty
    };

    /// <summary>
    /// Read version from json similar to package.json:
    /// {
    ///   ...
    ///   "version": "1.2.3"
    ///   ...
    /// }
    /// </summary>
    /// <param name="json">Json string.</param>
    /// <param name="silent">True to not print warnings while parsing the version.</param>
    /// <returns>Parsed version info - VersionInfo.Invalid if failed.</returns>
    public static VersionInfo FromJson( string json, bool silent = true )
    {
      var packageInfo = JsonUtility.FromJson<PackageInfo>( json );
      if ( packageInfo == null ) {
        if ( !silent )
          Debug.LogWarning( $"Unable to parse json:\n{json}" );
        return Invalid;
      }

      var result = Parse( packageInfo.version, silent );
      if ( result.IsValid ) {
        result.Platform = packageInfo.platform;
        result.PlatformName = packageInfo.platformName;
      }

      return result;
    }

    /// <summary>
    /// Read version from json file.
    /// </summary>
    /// <param name="file">Filename including path.</param>
    /// <param name="silent">True to not print warnings while parsing the version.</param>
    /// <returns>Parsed version info - VersionInfo.Invalid if failed.</returns>
    public static VersionInfo FromFile( string file, bool silent = true )
    {
      var fileInfo = new FileInfo( file );
      if ( !fileInfo.Exists ) {
        if ( !silent )
          Debug.LogWarning( "Unable to read version from file: " + file + " - file doesn't exist." );
        return Invalid;
      }

      return FromJson( File.ReadAllText( fileInfo.FullName ), silent );
    }

    /// <summary>
    /// Parse semantic version string.
    /// </summary>
    /// <param name="str">String containing semantic version.</param>
    /// <param name="silent">True to not print warnings while parsing the version.</param>
    /// <returns>VersionInfo instance if successful, otherwise VersionInfo.Invalid.</returns>
    public static VersionInfo Parse( string str, bool silent = true )
    {
      var packageName = "AGXDynamicsForUnity-";
      var unityPackageExtension = ".unitypackage";

      var startIndex = str.StartsWith( packageName ) ?
                         packageName.Length :
                         0;
      var numCharacters = str.Length -
                          startIndex -
                          ( str.EndsWith( unityPackageExtension ) ? unityPackageExtension.Length : 0 );
      if ( numCharacters < "0.0.0".Length ) {
        if ( !silent )
          IssueWarning( str );
        return Invalid;
      }

      var versionStr = str.Substring( startIndex, numCharacters );

      // Reading git hash and removes it from versionStr if it exist.
      var gitHashStartIndex = versionStr.LastIndexOf( '+' );
      var gitHash = string.Empty;
      if ( gitHashStartIndex > 0 ) {
        gitHash    = versionStr.Substring( gitHashStartIndex + 1 );
        versionStr = versionStr.Substring( 0, gitHashStartIndex );
      }

      ReleaseType releaseType;
      try {
        releaseType = ParseReleaseType( ref versionStr );
      }
      catch ( AGXUnity.Exception ex ) {
        if ( !silent )
          IssueWarning( str + " - " + ex.Message );
        return Invalid;
      }

      var majorMinorPatchStrings = versionStr.Split( '.' );
      if ( majorMinorPatchStrings.Length != 3 ) {
        if ( !silent )
          IssueWarning( str );
        return Invalid;
      }

      var majorMinorPatch = new int[] { -1, -1, -1 };
      for ( int i = 0; i < 3; ++i ) {
        if ( !int.TryParse( majorMinorPatchStrings[ i ], out majorMinorPatch[ i ] ) ) {
          if ( !silent )
            IssueWarning( str );
          return Invalid;
        }
      }

      return new VersionInfo()
      {
        Major   = majorMinorPatch[ 0 ],
        Minor   = majorMinorPatch[ 1 ],
        Patch   = majorMinorPatch[ 2 ],
        Release = releaseType,
        GitHash = gitHash,
      };
    }

    public int Major;
    public int Minor;
    public int Patch;
    public ReleaseType Release;
    public string GitHash;
    public string Platform;
    public string PlatformName;

    public bool IsValid
    {
      get { return Major >= 0 && Minor >= 0 && Patch >= 0 && Release.IsValid; }
    }

    public string VersionStringShort
    {
      get
      {
        return IsValid ?
                 $"{Major}.{Minor}.{Patch}{Release}" :
                 "";
      }
    }

    public string VersionString
    {
      get
      {
        return IsValid ?
                 $"{VersionStringShort}{ ( !string.IsNullOrEmpty( GitHash ) ? "+" + GitHash : "" ) }" :
                 "";
      }
    }

    public override string ToString() => VersionString;

    public int CompareTo( VersionInfo other )
    {
      // Currently no respect if this or other is a pre-release
      // while comparing Major, Minor and Patch.

      if ( Major < other.Major )
        return -1;
      else if ( Major > other.Major )
        return 1;

      if ( Minor < other.Minor )
        return -1;
      else if ( Minor > other.Minor )
        return 1;

      if ( Patch < other.Patch )
        return -1;
      else if ( Patch > other.Patch )
        return 1;

      // Major, Minor and Patch are identical.

      // One is official and the other is not.
      if ( Release.Official && !other.Release.Official )
        return 1;
      else if ( !Release.Official && other.Release.Official )
        return -1;

      // Comparing pre-release values.
      if ( PreReleaseValue < other.PreReleaseValue )
        return -1;
      else if ( PreReleaseValue > other.PreReleaseValue )
        return 1;

      // Versions are identical.
      return 0;
    }

    public static bool operator < ( VersionInfo vi1, VersionInfo vi2 )
    {
      return vi1.CompareTo( vi2 ) < 0;
    }

    public static bool operator > ( VersionInfo vi1, VersionInfo vi2 )
    {
      return vi1.CompareTo( vi2 ) > 0;
    }

    public static bool operator ==( VersionInfo vi1, VersionInfo vi2 )
    {
      return vi1.CompareTo( vi2 ) == 0;
    }

    public static bool operator !=( VersionInfo vi1, VersionInfo vi2 )
    {
      return !( vi1 == vi2 );
    }

    public static bool operator >= ( VersionInfo vi1, VersionInfo vi2 )
    {
      return vi1 > vi2 || vi1 == vi2;
    }

    public static bool operator <= ( VersionInfo vi1, VersionInfo vi2 )
    {
      return vi1 < vi2 || vi1 == vi2;
    }

    public override bool Equals( object obj ) => base.Equals( obj );

    public override int GetHashCode() => base.GetHashCode();

    private int PreReleaseValue
    {
      get
      {
        if ( !IsValid )
          return -10000;

        return Convert.ToInt32( Release.Alpha >= 0 ) * ( -1000 + Release.Alpha ) +
               Convert.ToInt32( Release.Beta >= 0 ) * ( 0 + Release.Beta ) +
               Convert.ToInt32( Release.Rc >= 0 ) * ( 1000 + Release.Rc );
      }
    }

    private static void IssueWarning( string orgStr )
    {
      Debug.LogWarning( $"Unable to parse version string: {orgStr}" );
    }

    private class PackageInfo
    {
      public string version = string.Empty;
      public string platform = "windows";
      public string platformName = "Microsoft Windows";
    }

    private static ReleaseType ParseReleaseType( ref string versionStr )
    {
      var releaseType = new ReleaseType()
      {
        Alpha = ParsePreRelease( ref versionStr, "-alpha." ),
        Beta  = ParsePreRelease( ref versionStr, "-beta." ),
        Rc    = ParsePreRelease( ref versionStr, "-rc." ),
      };
      releaseType.Official = !releaseType.IsPreview;

      return releaseType;
    }

    private static int ParsePreRelease( ref string versionStr, string preReleaseId )
    {
      var preReleaseIndex = versionStr.LastIndexOf( preReleaseId );
      var preReleaseVersion = -1;
      if ( preReleaseIndex > 0 ) {
        if ( !int.TryParse( versionStr.Substring( preReleaseIndex +
                                                  preReleaseId.Length ),
                            out preReleaseVersion ) ) {
          throw new AGXUnity.Exception( $"Unable to parse pre-release id {preReleaseId} with " +
                                        $"version {versionStr.Substring( preReleaseIndex + preReleaseId.Length )}" );
        }

        versionStr = versionStr.Substring( 0, preReleaseIndex );
      }
      return preReleaseVersion;
    }
  }
}
