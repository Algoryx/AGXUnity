using System;
using UnityEngine;

namespace AGXUnity
{
  public struct VersionInfo : IComparable<VersionInfo>
  {
    public static VersionInfo Invalid = new VersionInfo()
    {
      Major = -1,
      Minor = -1,
      Patch = -1,
      Beta = -1,
      GitHash = string.Empty
    };

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

      // Reading beta version and removes it from versionStr if it exist.
      var betaId = "-beta.";
      var betaIndex = versionStr.LastIndexOf( betaId );
      var betaVersion = -1;
      if ( betaIndex > 0 ) {
        if ( !int.TryParse( versionStr.Substring( betaIndex + betaId.Length ), out betaVersion ) ) {
          if ( !silent )
            IssueWarning( str );
          return Invalid;
        }
        versionStr = versionStr.Substring( 0, betaIndex );
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
        Beta    = betaVersion,
        GitHash = gitHash,
      };
    }

    public int Major;
    public int Minor;
    public int Patch;
    public int Beta;
    public string GitHash;

    public bool IsValid
    {
      get { return Major >= 0 && Minor >= 0 && Patch >= 0; }
    }

    public bool IsBeta
    {
      get { return Beta >= 0; }
    }

    public string VersionStringShort
    {
      get
      {
        return IsValid ?
                 $"{Major}.{Minor}.{Patch}{( Beta >= 0 ? "-beta." + Beta.ToString() : "" )}" :
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

      if ( IsBeta && !other.IsBeta )
        return -1;
      else if ( !IsBeta && other.IsBeta )
        return 1;

      if ( IsBeta && other.IsBeta && Beta < other.Beta )
        return -1;
      else if ( IsBeta && other.IsBeta && Beta > other.Beta )
        return 1;

      if ( IsBeta && !other.IsBeta )
        return -1;
      else if ( !IsBeta && other.IsBeta )
        return 1;

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

    private static void IssueWarning( string orgStr )
    {
      Debug.LogWarning( $"Unable to parse version string: {orgStr}" );
    }
  }
}
