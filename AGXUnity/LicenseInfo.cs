using AGXUnity.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Math = System.Math;

namespace AGXUnity
{
  /// <summary>
  /// AGX Dynamics version and license information.
  /// </summary>
  public struct LicenseInfo
  {
    /// <summary>
    /// AGX Dynamics modules.
    /// </summary>
    [Flags]
    public enum Module
    {
      None             = 0,
      AGX              = 1 << 0,
      AGXParticles     = 1 << 1,
      AGXCable         = 1 << 2,
      AGXCableDamage   = 1 << 3,
      AGXControl       = 1 << 4,
      AGXDriveTrain    = 1 << 5,
      AGXGranular      = 1 << 6,
      AGXHydraulics    = 1 << 7,
      AGXHydrodynamics = 1 << 8,
      AGXSimulink      = 1 << 9,
      AGXSensor        = 1 << 10,
      AGXTerrain       = 1 << 11,
      AGXTires         = 1 << 12,
      AGXTracks        = 1 << 12,
      AGXWireLink      = 1 << 14,
      AGXWires         = 1 << 15,
      All              = ~0
    }

    /// <summary>
    /// Creates a comma separated string with the flagged modules in
    /// the given <paramref name="modules"/>.
    /// </summary>
    /// <param name="modules">Modules to get as string.</param>
    /// <returns>Comma separated string with the given module names.</returns>
    public static string GetModuleNames( Module modules )
    {
      if ( modules.HasFlag( Module.All ) ) {
        modules = Module.None;
        foreach ( Module module in Enum.GetValues( typeof( Module ) ) ) {
          if ( module == Module.None || module == Module.All )
            continue;
          modules |= module;
        }
      }

      return string.Join( ", ", modules.ToString()
                                       .Split( new string[] { ", " }, StringSplitOptions.RemoveEmptyEntries )
                                       .Where( str => str.StartsWith( "AGX" ) )
                                       .Select( str => str.SplitCamelCase() ) );
    }

    /// <summary>
    /// License types that AGX Dynamics supports.
    /// </summary>
    public enum LicenseType
    {
      /// <summary>
      /// Unidentified license type.
      /// </summary>
      Unknown,
      /// <summary>
      /// An agx.lfx file or encrypted string.
      /// </summary>
      Service,
      /// <summary>
      /// An agx.lic file or obfuscated string.
      /// </summary>
      Legacy
    }

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

        info.IsValid = agx.Runtime.instance().isValid();
        info.Status = agx.Runtime.instance().getStatus();
        info.IsFloating = agx.Runtime.instance().isFloatingLicense();

        ParseDate( ref info, agx.Runtime.instance().readValue( "EndDate" ) );

        // Parsing end date from the status string.
        if ( !info.IsValid && !info.ValidEndDate ) {
          foreach ( var endDateTag in s_expiredEndDateTags ) {
            var startIndex = info.Status.IndexOf( endDateTag );
            if ( startIndex < 0 )
              continue;
            startIndex += endDateTag.Length;
            if ( ParseDate( ref info,
                            info.Status.Substring( startIndex,
                                                   FindDateLength( info.Status,
                                                                   startIndex ) ) ) )
              break;
          }
        }

        info.User = agx.Runtime.instance().readValue( "User" );
        info.Contact = agx.Runtime.instance().readValue( "Contact" );

        var subscriptionType = agx.Runtime.instance().readValue( "Product" ).Split( '-' );
        if ( subscriptionType.Length == 2 )
          info.TypeDescription = subscriptionType[ 1 ].Trim();

        if ( string.IsNullOrEmpty( info.TypeDescription ) )
          info.TypeDescription = "Unknown";

        if ( agx.Runtime.instance().hasKey( "InstallationID" ) ) {
          info.Type = LicenseType.Service;
          info.UniqueId = agx.Runtime.instance().readValue( "InstallationID" );
        }
        else if ( agx.Runtime.instance().hasKey( "License" ) ) {
          info.Type = LicenseType.Legacy;
          info.UniqueId = agx.Runtime.instance().readValue( "License" );
        }

        var enabledModules = agx.Runtime.instance().getEnabledModules();
        ParseModules( ref info, enabledModules );
      }
      catch ( System.Exception ) {
        info = new LicenseInfo();
      }

      return info;
    }

    public static LicenseInfo FromNative( agx.LicenseInfo native )
    {
      var info = new LicenseInfo();
      info.Version = agx.agxSWIG.agxGetVersion( false );

      info.Status = "";
      info.IsValid = native.licenseType != -1;
      LicenseInfo.ParseDate( ref info, native.endDate );

      info.Type = LicenseInfo.LicenseType.Service;
      var subscriptionType = native.product.Split("-");
      if ( subscriptionType.Length == 2 )
        info.TypeDescription = subscriptionType[ 1 ].Trim();

      info.User = native.user;
      info.Contact = native.contact;

      info.UniqueId = native.installationID;

      info.IsFloating = native.licenseType == 1;

      ParseModules( ref info, native.modules );
      return info;
    }

    public static LicenseInfo FromLegacy( string legacyLicenseContent )
    {
      var info = new LicenseInfo()
      {
        Type = LicenseInfo.LicenseType.Legacy,
        TypeDescription = "Legacy License",
        Version = agx.agxSWIG.agxGetVersion( false )
      };

      var userMatch = Regex.Match( legacyLicenseContent, @"^\s+User\s+""(.*)""\s*$", RegexOptions.Multiline );
      if ( userMatch.Success )
        info.User = userMatch.Groups[ 1 ].Value;

      var contactMatch = Regex.Match( legacyLicenseContent, @"^\s+Contact\s+""(.*)""\s*$", RegexOptions.Multiline );
      if ( contactMatch.Success )
        info.Contact = contactMatch.Groups[ 1 ].Value;

      var endDateMatch = Regex.Match( legacyLicenseContent, @"^\s+EndDate\s+""(.*)""\s*$", RegexOptions.Multiline );
      if ( endDateMatch.Success )
        LicenseInfo.ParseDate( ref info, endDateMatch.Groups[ 1 ].Value );

      info.IsValid = !info.IsExpired;
      info.IsFloating = false;

      var moduleMatch = Regex.Match( legacyLicenseContent, @"^\s+Modules\s+""(.*)""\s*$", RegexOptions.Multiline );
      if ( moduleMatch.Success )
        ParseModules( ref info, moduleMatch.Groups[ 1 ].Value.Split( ',' ) );

      return info;
    }

    /// <summary>
    /// AGX Dynamics version.
    /// </summary>
    public string Version;

    /// <summary>
    /// License end data.
    /// </summary>
    public DateTime EndDate;

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
    public Module EnabledModules;

    /// <summary>
    /// True if all available modules are enabled.
    /// </summary>
    public bool AllModulesEnabled
    {
      get
      {
        var allSet = true;
        foreach ( Module eVal in Enum.GetValues( typeof( Module ) ) ) {
          if ( eVal == Module.None || eVal == Module.All )
            continue;
          allSet = allSet && EnabledModules.HasFlag( eVal );
        }
        return allSet;
      }
    }

    /// <summary>
    /// True if the license is valid - otherwise false.
    /// </summary>
    public bool IsValid;

    /// <summary>
    /// True if the license is a floating license
    /// </summary>
    public bool IsFloating;

    /// <summary>
    /// License status if something is wrong - otherwise an empty string.
    /// </summary>
    public string Status;

    /// <summary>
    /// AGX Dynamics license type.
    /// </summary>
    public LicenseType Type;

    /// <summary>
    /// License type - "Unknown" if not successfully parsed.
    /// </summary>
    public string TypeDescription;

    /// <summary>
    /// Unique license identifier.
    /// </summary>
    public string UniqueId;

    /// <summary>
    /// True if there's a parsed, valid end date - otherwise false.
    /// </summary>
    public bool ValidEndDate { get { return !DateTime.Equals( EndDate, DateTime.MinValue ); } }

    /// <summary>
    /// True if the license has expired.
    /// </summary>
    public bool IsExpired { get { return !ValidEndDate || EndDate < DateTime.Now; } }

    /// <summary>
    /// Info string regarding how long it is until the license expires or
    /// the time since the license expired.
    /// </summary>
    public string DiffString
    {
      get
      {
        var diff = EndDate - DateTime.Now;
        var str = diff.Days != 0 ?
                    $"{Math.Abs( diff.Days )} day" + ( System.Math.Abs( diff.Days ) != 1 ? "s" : string.Empty ) :
                    string.Empty;
        str += diff.Days == 0 && diff.Hours != 0 ?
                    $"{Math.Abs( diff.Hours )} hour" + ( System.Math.Abs( diff.Hours ) != 1 ? "s" : string.Empty ) :
                    string.Empty;
        str += string.IsNullOrEmpty( str ) ?
                    $"{Math.Abs( diff.Minutes )} minute" + ( System.Math.Abs( diff.Minutes ) != 1 ? "s" : string.Empty ) :
                    string.Empty;
        return str;
      }
    }

    /// <summary>
    /// True when the license information has been parsed from AGX Dynamics.
    /// </summary>
    public bool IsParsed { get { return Type != LicenseType.Unknown; } }

    /// <summary>
    /// True if the license expires within given <paramref name="days"/>.
    /// </summary>
    /// <param name="days">Number of days.</param>
    /// <returns>True if the license expires within given <paramref name="days"/> - otherwise false.</returns>
    public bool IsAboutToBeExpired( int days )
    {
      var diff = EndDate - DateTime.Now;
      return Convert.ToInt32( diff.TotalDays + 0.5 ) < days;
    }

    /// <summary>
    /// Checks if this license is valid and if the given <paramref name="module"/>
    /// is enabled in the license.
    /// </summary>
    /// <param name="module">Module to check.</param>
    /// <returns>True if this license is valid and the given module is enabled.</returns>
    /// <seealso cref="HasModuleLogWarn(Module, object)"/>
    public bool HasModule( Module module )
    {
      return IsValid && EnabledModules.HasFlag( module );
    }

    /// <summary>
    /// Checks if the license is valid and if the given <paramref name="module"/>
    /// is enabled in the license. If the license is valid but the module isn't
    /// in the license, an log error is printed.
    /// </summary>
    /// <param name="module">Module to check.</param>
    /// <param name="context">Caller context.</param>
    /// <returns>True if the license is valid and the module is enabled in the license - otherwise false.</returns>
    public bool HasModuleLogError( Module module, UnityEngine.Object context )
    {
      var isEnabled = HasModule( module );
      // Warn when the license is valid but the module isn't in the license.
      // Invalid/expired licenses results in other errors displayed elsewhere.
      if ( !isEnabled && IsValid ) {
        var contextName = context != null ?
                            context.GetType().FullName :
                            "Unknown Source";
        UnityEngine.Debug.LogError( $"{contextName}: Required license module(s) \"{GetModuleNames( module )}\" aren't enabled in the current license.",
                                    context );
      }

      return isEnabled;
    }

    public override string ToString()
    {
      return $"Valid: {IsValid}, Valid End Date: {ValidEndDate}, End Date: {EndDate}, " +
             $"Modules: [{string.Join( ",", EnabledModules )}], User: {User}, Contact: {Contact}, " +
             $"Status: {Status}";
    }

    private static void ParseModules( ref LicenseInfo info, IEnumerable<String> modules )
    {
      foreach ( var module in modules ) {
        if ( module == "AgX" ) {
          info.EnabledModules |= LicenseInfo.Module.AGX;
          continue;
        }
        else if ( !module.StartsWith( "AgX-" ) )
          continue;
        var enabledModule = module.Replace( "AgX-", "AGX" );
        if ( Enum.TryParse<LicenseInfo.Module>( enabledModule, out var enumModule ) )
          info.EnabledModules |= enumModule;
      }
    }

    private static bool ParseDate( ref LicenseInfo info, string dateString )
    {
      try {
        // The license is valid during the "End Date". Add one day
        // and remove a tick.
        info.EndDate = DateTime.Parse( dateString ).AddDays( 1.0 ).AddTicks( -1 );
      }
      catch ( FormatException ) {
        if ( dateString.ToLower() == "none" )
          info.EndDate = DateTime.MaxValue;
        else
          info.EndDate = DateTime.MinValue;

        return false;
      }

      return true;
    }

    private static int FindDateLength( string dateStr, int endDateStartIndex )
    {
      char[] delims = new char[] { ' ', ',' };
      int lastIndex = dateStr.Length;
      foreach ( var delim in delims ) {
        var index = dateStr.IndexOf( delim, endDateStartIndex );
        if ( index >= 0 ) {
          lastIndex = index;
          break;
        }
      }
      return lastIndex - endDateStartIndex;
    }

    private static string[] s_expiredEndDateTags = new string[] { "EndDate: ", "End date: " };
  }
}
