using System;
using System.Linq;

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
      AGXWires         = 1 << 13
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

        var endDateString = agx.Runtime.instance().readValue( "EndDate" );
        try {
          info.EndDate = DateTime.Parse( endDateString );
        }
        catch ( FormatException ) {
          if ( endDateString.ToLower() == "none" )
            info.EndDate = DateTime.MaxValue;
          else
            info.EndDate = DateTime.MinValue;
        }

        info.LicenseValid = agx.Runtime.instance().isValid();
        info.LicenseStatus = agx.Runtime.instance().getStatus();

        info.User = agx.Runtime.instance().readValue( "User" );
        info.Contact = agx.Runtime.instance().readValue( "Contact" );

        var subscriptionType = agx.Runtime.instance().readValue( "Product" ).Split( '-' );
        if ( subscriptionType.Length == 2 )
          info.Type = subscriptionType[ 1 ].Trim();

        if ( string.IsNullOrEmpty( info.Type ) )
          info.Type = "Unknown";

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

        info.IsParsed = true;
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
    /// License type - "Unknown" if not successfully parsed.
    /// </summary>
    public string Type;

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
    public bool ValidEndDate { get { return !DateTime.Equals( EndDate, DateTime.MinValue ); } }

    /// <summary>
    /// True if the license has expired.
    /// </summary>
    public bool LicenseExpired { get { return !ValidEndDate || EndDate < DateTime.Now; } }

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
    public bool IsParsed { get; private set; }

    /// <summary>
    /// True if the license expires within given <paramref name="days"/>.
    /// </summary>
    /// <param name="days">Number of days.</param>
    /// <returns>True if the license expires within given <paramref name="days"/> - otherwise false.</returns>
    public bool IsLicenseAboutToBeExpired( int days )
    {
      var diff = EndDate - DateTime.Now;
      return Convert.ToInt32( diff.TotalDays + 0.5 ) < days;
    }

    public override string ToString()
    {
      return $"Valid: {LicenseValid}, Valid End Date: {ValidEndDate}, End Date: {EndDate}, " +
             $"Modules: [{string.Join( ",", EnabledModules )}], User: {User}, Contact: {Contact}, " +
             $"Status: {LicenseStatus}";
    }
  }
}
