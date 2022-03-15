using System;
using System.Linq;

using AGXUnity.Utils;

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
      AGXDriveTrain    = 1 << 4,
      AGXGranular      = 1 << 5,
      AGXHydraulics    = 1 << 6,
      AGXHydrodynamics = 1 << 7,
      AGXSimulink      = 1 << 8,
      AGXTerrain       = 1 << 9,
      AGXTires         = 1 << 10,
      AGXTracks        = 1 << 11,
      AGXWireLink      = 1 << 12,
      AGXWires         = 1 << 13,
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

        var enabledModules = agx.Runtime.instance().getEnabledModules().ToArray();
        info.EnabledModules = Module.None;
        foreach ( var module in enabledModules ) {
          if ( module == "AgX" ) {
            info.EnabledModules |= Module.AGX;
            continue;
          }
          else if ( !module.StartsWith( "AgX-" ) )
            continue;
          var enabledModule = module.Replace( "AgX-", "AGX" );
          if ( Enum.TryParse<Module>( enabledModule, out var enumModule ) )
            info.EnabledModules |= enumModule;
        }
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
