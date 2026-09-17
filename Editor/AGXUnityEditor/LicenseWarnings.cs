using AGXUnity;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AGXUnityEditor
{
  // Keep the result of loading separate from file queries, which may change
  // the native status without changing the currently loaded license.
  internal static class LicenseWarnings
  {
    internal static LicenseInfo CurrentLicense { get; private set; }

    internal static void Capture( LicenseInfo info )
    {
      CurrentLicense = info;
    }

    internal static bool IsFloating( LicenseInfo info )
    {
      return info.IsFloating ||
             ( info.Status?.IndexOf( "floating", StringComparison.OrdinalIgnoreCase ) ?? -1 ) >= 0;
    }

    internal static string GetWarningMessage( LicenseInfo info, bool hasFloatingLicense = false )
    {
      if ( info.IsValid || !LicenseManager.AutomaticFloatingCheckoutEnabled )
        return string.Empty;

      var message = "No valid AGX Dynamics license is available.";
      if ( !string.IsNullOrWhiteSpace( info.Status ) )
        message += "\n\n" + info.Status.Trim();

      return message + "\n\n" + ( IsFloating( info ) || hasFloatingLicense ?
                                   "Check the connection to your floating license server and that a license seat is available." :
                                   "Activate a license or import an existing license file in the License Manager." );
    }

    internal static string GetActivationError( LicenseInfo info )
    {
      // A failed request can leave a previously valid license loaded. Its
      // status must not be presented as the reason activation failed.
      return "License activation failed.\n\n" +
             ( !info.IsValid && !string.IsNullOrWhiteSpace( info.Status ) ?
                 info.Status.Trim() :
                 "Verify your license ID, activation password, and connection before retrying." );
    }

    internal static void ScheduleStartupWarning()
    {
      if ( Application.isBatchMode || CurrentLicense.IsValid || !LicenseManager.AutomaticFloatingCheckoutEnabled ||
           SessionState.GetBool( s_startupWarningShownKey, false ) )
        return;

      EditorApplication.update -= ShowStartupWarning;
      EditorApplication.update += ShowStartupWarning;
    }

    private static void ShowStartupWarning()
    {
      if ( Application.isBatchMode || !LicenseManager.AutomaticFloatingCheckoutEnabled ||
           SessionState.GetBool( s_startupWarningShownKey, false ) ) {
        EditorApplication.update -= ShowStartupWarning;
        return;
      }

      if ( EditorApplication.isCompiling || EditorApplication.isUpdating ||
           EditorApplication.isPlayingOrWillChangePlaymode || LicenseManager.IsBusy ||
           Resources.FindObjectsOfTypeAll<Windows.LicenseManagerWindow>()
                    .Any( window => window.IsUpdatingLicenseInformation ) )
        return;

      EditorApplication.update -= ShowStartupWarning;
      if ( !NativeHandler.HasInstance || !NativeHandler.Instance.Initialized )
        return;

      // Observe validity only: checking a warning must never acquire or return
      // a floating seat. Retain the captured failure status if still invalid.
      if ( agx.Runtime.instance().isValid() ) {
        Capture( LicenseInfo.Create() );
        return;
      }

      SessionState.SetBool( s_startupWarningShownKey, true );
      if ( EditorUtility.DisplayDialog( "AGX Dynamics license",
                                        GetWarningMessage( CurrentLicense ),
                                        "Open License Manager",
                                        "Dismiss" ) )
        Windows.LicenseManagerWindow.Open();
    }

    private const string s_startupWarningShownKey = "AGXUnity.LicenseWarningShown";
  }
}
