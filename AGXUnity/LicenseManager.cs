using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AGXUnity
{
  public static class LicenseManager
  {
    /// <summary>
    /// License types that AGX Dynamics supports.
    /// </summary>
    public enum LicenseType
    {
      /// <summary>
      /// An agx.lic file or obfuscated string.
      /// </summary>
      Legacy,
      /// <summary>
      /// An agx.lfx file or encrypted string.
      /// </summary>
      Service
    }

    /// <summary>
    /// AGX Dynamics version and license information.
    /// </summary>
    public struct LicenseInfo
    {
      /// <summary>
      /// Create and parse native license info.
      /// </summary>
      /// <returns>Native license info.</returns>
      public static LicenseInfo Create()
      {
        var info = new LicenseInfo();

        try {
          info.Version = agx.agxSWIG.agxGetVersion( false );
          if ( info.Version.ToLower().StartsWith( "agx-" ) )
            info.Version = info.Version.Remove( 0, 4 );

          var endDateString = agx.Runtime.instance().readValue( "EndDate" );
          try {
            info.EndDate = System.DateTime.Parse( endDateString );
          }
          catch ( System.FormatException ) {
            if ( endDateString.ToLower() == "none" )
              info.EndDate = System.DateTime.MaxValue;
            else
              info.EndDate = System.DateTime.MinValue;
          }

          info.LicenseValid = agx.Runtime.instance().isValid();
          info.LicenseStatus = agx.Runtime.instance().getStatus();

          info.User = agx.Runtime.instance().readValue( "User" );
          info.Contact = agx.Runtime.instance().readValue( "Contact" );

          info.EnabledModules = agx.Runtime.instance().getEnabledModules().ToArray();
        }
        catch ( System.Exception ) {
          info = new LicenseInfo();
        }

        return info;
      }

      /// <summary>
      /// AGX Dynamics version.
      /// </summary>
      public string Version;

      /// <summary>
      /// License end data.
      /// </summary>
      public System.DateTime EndDate;

      /// <summary>
      /// User name of the license.
      /// </summary>
      public string User;

      /// <summary>
      /// Contact information.
      /// </summary>
      public string Contact;

      /// <summary>
      /// Activated AGX Dynamics modules.
      /// </summary>
      public string[] EnabledModules;

      /// <summary>
      /// True if the license is valid - otherwise false.
      /// </summary>
      public bool LicenseValid;

      /// <summary>
      /// License status if something is wrong - otherwise an empty string.
      /// </summary>
      public string LicenseStatus;

      /// <summary>
      /// True if there's a parsed, valid end date - otherwise false.
      /// </summary>
      public bool ValidEndDate { get { return !System.DateTime.Equals( EndDate, System.DateTime.MinValue ); } }

      /// <summary>
      /// True if the license has expired.
      /// </summary>
      public bool LicenseExpired { get { return !ValidEndDate || EndDate < System.DateTime.Now; } }

      /// <summary>
      /// Info string regarding how long it is until the license expires or
      /// the time since the license expired.
      /// </summary>
      public string DiffString
      {
        get
        {
          var diff = EndDate - System.DateTime.Now;
          var str = diff.Days != 0 ?
                      $"{System.Math.Abs( diff.Days )} day" + ( System.Math.Abs( diff.Days ) != 1 ? "s" : string.Empty ) :
                      string.Empty;
          str += diff.Days == 0 && diff.Hours != 0 ?
                      $"{System.Math.Abs( diff.Hours )} hour" + ( System.Math.Abs( diff.Hours ) != 1 ? "s" : string.Empty ) :
                      string.Empty;
          str += string.IsNullOrEmpty( str ) ?
                      $"{System.Math.Abs( diff.Minutes )} minute" + ( System.Math.Abs( diff.Minutes ) != 1 ? "s" : string.Empty ) :
                      string.Empty;
          return str;
        }
      }

      /// <summary>
      /// True if the license expires within given <paramref name="days"/>.
      /// </summary>
      /// <param name="days">Number of days.</param>
      /// <returns>True if the license expires within given <paramref name="days"/> - otherwise false.</returns>
      public bool IsLicenseAboutToBeExpired( int days )
      {
        var diff = EndDate - System.DateTime.Now;
        return System.Convert.ToInt32( diff.TotalDays + 0.5 ) < days;
      }

      public override string ToString()
      {
        return $"Valid: {LicenseValid}, Valid End Date: {ValidEndDate}, End Date: {EndDate}, " +
               $"Modules: [{string.Join( ",", EnabledModules )}], User: {User}, Contact: {Contact}, " +
               $"Status: {LicenseStatus}";
      }
    }

    /// <summary>
    /// License file search directories independent of license type.
    /// </summary>
    public static string[] LicenseFileDirectories
    {
      get
      {
        if ( s_licenseFileDirectories == null ) {
          s_licenseFileDirectories = ( from LicenseType type in System.Enum.GetValues( typeof( LicenseType ) )
                                       let licenseDir = FindLicenseFileDirectory( type )
                                       where !string.IsNullOrEmpty( licenseDir )
                                       select licenseDir ).ToList();
        }

        return s_licenseFileDirectories.ToArray();
      }
    }

    public static bool HasLicenseFile( LicenseType type )
    {
      return !string.IsNullOrEmpty( FindLicenseFile( type ) );
    }

    public static string FindLicenseFile( LicenseType type )
    {
      return Directory.GetFiles( ".",
                                 GetLicenseFilename( type ),
                                 SearchOption.AllDirectories ).FirstOrDefault();
    }

    public static string FindLicenseFileDirectory( LicenseType type )
    {
      var licenseFile = FindLicenseFile( type );
      if ( string.IsNullOrEmpty( licenseFile ) )
        return string.Empty;
      return Path.GetDirectoryName( licenseFile );
    }

    public static string GetLicenseFilename( LicenseType type )
    {
      return s_liceneseNames[ (int)type ];
    }

    private static List<string> s_licenseFileDirectories = null;
    private static string[] s_liceneseNames = new string[] { "agx.lic", "agx.lfx" };
  }
}
