using AGXUnity.Utils;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Windows
{
  public class LicenseManagerWindow : EditorWindow
  {
    public static LicenseManagerWindow Open()
    {
      var window = GetWindow<LicenseManagerWindow>( false,
                                                    "License Manager - AGX Dynamics for Unity",
                                                    true );
      window.minSize = new Vector2( 300, 250 );

      return window;
    }

    /// <summary>
    /// License file directory with Assets as root and
    /// / as directory separator.
    /// </summary>
    public string LicenseDirectory
    {
      get
      {
        return GetLicenseDirectoryData().String;
      }
      private set
      {
        if ( Path.IsPathRooted( value ) )
          value = value.MakeRelative( Directory.GetCurrentDirectory(), false ).Replace( '\\', '/' );
        GetLicenseDirectoryData().String = value;
      }
    }

    public bool IsUpdatingLicenseInformation => m_updateLicenseInformationRequested || m_updateLicenseInfoTask != null;

    private bool IsBusy => AGXUnity.LicenseManager.IsBusy || m_licenseOperationTask != null;

    private void OnEnable()
    {
      m_activeLicenseStyle = null;
      m_checkLicenseValidity = true;
      EditorApplication.update += OnEditorUpdate;

      ValidateLicenseDirectory();

      StartUpdateLicenseInformation();
    }

    private void OnDisable()
    {
      EditorApplication.update -= OnEditorUpdate;
      // Native operations may finish after the window closes. Their callbacks
      // only complete a task and never retain or access this window.
      m_licenseOperationTask = null;
    }

    private void OnFocus()
    {
      m_checkLicenseValidity = true;
    }

    private void OnGUI()
    {
      ValidateLicenseDirectory();
      if ( m_clearFocus ) {
        UnityEngine.GUI.FocusControl( "" );
        m_clearFocus = false;
      }

      using ( GUI.AlignBlock.Center )
        GUILayout.Box( IconManager.GetAGXUnityLogo(),
                       GUI.Skin.customStyles[ 3 ],
                       GUILayout.Width( System.Math.Min( 400.0f, position.width - 20.0f ) ),
                       GUILayout.Height( 100 ) );

      EditorGUILayout.LabelField( "© " + System.DateTime.Now.Year + " Algoryx Simulation AB",
                                  InspectorEditor.Skin.LabelMiddleCenter );

      InspectorGUI.BrandSeparator( 1, 6 );

      m_scroll = EditorGUILayout.BeginScrollView( m_scroll );

      if ( IsUpdatingLicenseInformation )
        ShowNotification( GUI.MakeLabel( "Reading..." ) );
      else if ( IsBusy )
        ShowNotification( GUI.MakeLabel( m_operation == LicenseOperation.Connect || AGXUnity.LicenseManager.IsConnecting ? "Connecting..." :
                                        m_operation == LicenseOperation.Return || AGXUnity.LicenseManager.IsReturning ? "Returning seat..." :
                                        m_operation == LicenseOperation.Activate || AGXUnity.LicenseManager.IsActivating ? "Activating..." : "Refreshing..." ) );

      if ( !IsUpdatingLicenseInformation && !IsBusy &&
           AGXUnity.NativeHandler.HasInstance && AGXUnity.NativeHandler.Instance.Initialized &&
           !LicenseWarnings.CurrentLicense.IsValid && AGXUnity.LicenseManager.AutomaticFloatingCheckoutEnabled ) {
        var hasFloatingLicense = m_licenseData.Any( data => data.LicenseInfo.IsFloating );
        EditorGUILayout.HelpBox( LicenseWarnings.GetWarningMessage( LicenseWarnings.CurrentLicense, hasFloatingLicense ),
                                 MessageType.Warning, true );
        if ( !hasFloatingLicense && !LicenseWarnings.IsFloating( LicenseWarnings.CurrentLicense ) )
          EditorGUILayout.HelpBox( "If you have activated your license previously on this machine, consider importing the .lfx file instead of reactivating the license.", MessageType.Info, true );
        GUILayout.Space( 6 );
      }

      if ( !string.IsNullOrEmpty( m_licenseReadError ) )
        EditorGUILayout.HelpBox( m_licenseReadError, MessageType.Error, true );
      if ( !string.IsNullOrEmpty( m_operationError ) )
        EditorGUILayout.HelpBox( m_operationError, MessageType.Error, true );
      if ( !string.IsNullOrEmpty( m_importMessage ) )
        EditorGUILayout.HelpBox( m_importMessage, MessageType.Info, true );

      using ( new GUI.EnabledBlock( !IsUpdatingLicenseInformation && !IsBusy ) ) {
        if ( LicenseWarnings.CurrentLicense.IsFloating ) {
          EditorGUILayout.HelpBox( "A floating license seat is held. Return it before connecting, activating, refreshing, or loading another license.", MessageType.Info, true );
          using ( new GUI.EnabledBlock( UnityEngine.GUI.enabled && !EditorApplication.isPlayingOrWillChangePlaymode ) ) {
            if ( GUILayout.Button( GUI.MakeLabel( "Return Seat", false, "Return the current floating seat to the server." ), InspectorEditor.Skin.Button ) )
              ReturnSeat();
          }
          GUILayout.Space( 6 );
        }
        if ( EditorApplication.isPlayingOrWillChangePlaymode )
          EditorGUILayout.HelpBox( "Exit play mode to change license connections.", MessageType.Info, true );
        for ( int i = 0; i < m_licenseData.Count; ++i ) {
          var data = m_licenseData[ i ];
          LicenseDataGUI( data );
          if ( i + 1 < m_licenseData.Count )
            InspectorGUI.Separator( 2, 6, InspectorGUISkin.BrandColorBlue );
        }

        if ( m_licenseData.Count > 0 )
          InspectorGUI.Separator( 2, 6, InspectorGUISkin.BrandColorBlue );

        ActivateLicenseGUI();
      }

      InspectorGUI.BrandSeparator( 1, 6 );

      GUILayout.Label( GUI.MakeLabel( "Online Documentation", true ), InspectorEditor.Skin.Label );

      if ( InspectorGUI.Link( GUI.MakeLabel( "License Manager",
                                              false,
                                              s_licenseManagerUrl ) ) )
        Application.OpenURL( s_licenseManagerUrl );
      if ( InspectorGUI.Link( GUI.MakeLabel( "Licensing AGX Dynamics for Unity",
                                              false,
                                              s_licensingUrl ) ) )
        Application.OpenURL( s_licensingUrl );
      if ( InspectorGUI.Link( GUI.MakeLabel( "Free Trial",
                                              false,
                                              s_freeTrialUrl ) ) )
        Application.OpenURL( s_freeTrialUrl );

      EditorGUILayout.EndScrollView();

      if ( IsBusy || IsUpdatingLicenseInformation )
        Repaint();
    }

    private void ActivateLicenseGUI()
    {
      GUILayout.Label( GUI.MakeLabel( "Activate license", true ), InspectorEditor.Skin.Label );
      var selectLicenseRect    = GUILayoutUtility.GetLastRect();
      selectLicenseRect.x     += selectLicenseRect.width;
      selectLicenseRect.width  = 28;
      selectLicenseRect.x     -= selectLicenseRect.width;
      selectLicenseRect.y     -= EditorGUIUtility.standardVerticalSpacing;
      var selectLicensePressed = InspectorGUI.Button( selectLicenseRect,
                                                      MiscIcon.Locate,
                                                      UnityEngine.GUI.enabled && !EditorApplication.isPlayingOrWillChangePlaymode,
                                                      "Copy and inspect a license file. Floating files are connected using Connect.");
      if ( selectLicensePressed ) {
        var sourceLicense = EditorUtility.OpenFilePanel( "Copy AGX Dynamics license file",
                                                         ".",
                                                         $"{AGXUnity.LicenseManager.GetLicenseExtension( AGXUnity.LicenseInfo.LicenseType.Service ).Remove( 0, 1 )}," +
                                                         $"{AGXUnity.LicenseManager.GetLicenseExtension( AGXUnity.LicenseInfo.LicenseType.Legacy ).Remove( 0, 1 )}" );
        if ( !string.IsNullOrEmpty( sourceLicense ) ) {
          var targetLicense = AGXUnity.IO.Environment.FindUniqueFilename( $"{LicenseDirectory}/{Path.GetFileName( sourceLicense )}" ).PrettyPath();
          if ( EditorUtility.DisplayDialog( "Copy AGX Dynamics license",
                                          $"Copy \"{sourceLicense}\" to \"{targetLicense}\"?",
                                          "Yes",
                                          "Cancel" ) ) {
            try {
              File.Copy( sourceLicense, targetLicense, false );
              var info = AGXUnity.LicenseManager.QueryInfo( targetLicense );
              m_importMessage = info.IsFloating ? "Floating license imported. Select Connect to request a seat." :
                                !info.IsParsed ? "License copied, but its type could not be identified. Check the file and the Console for details." : null;
              if ( info.IsParsed && !info.IsFloating && !AGXUnity.LicenseManager.HasFloatingSession && AGXUnity.LicenseManager.LoadFile( targetLicense ) )
                m_activationError = null;
              else if ( !info.IsFloating && AGXUnity.LicenseManager.HasFloatingSession )
                m_importMessage = "License copied. Return the current floating seat before loading this license.";
              LicenseWarnings.Capture( AGXUnity.LicenseInfo.Create() );
              StartUpdateLicenseInformation();
              GUIUtility.ExitGUI();
            }
            catch ( ExitGUIException ) {
              throw;
            }
            catch ( System.Exception e ) {
              Debug.LogException( e );
            }
          }
        }
      }

      using ( InspectorGUI.IndentScope.Single ) {
        m_licenseActivateData.Id = EditorGUILayout.TextField( GUI.MakeLabel( "License Id" ),
                                                              m_licenseActivateData.Id,
                                                              InspectorEditor.Skin.TextField );
        if ( m_licenseActivateData.Id.Any( c => !char.IsDigit( c ) ) )
          m_licenseActivateData.Id = new string( m_licenseActivateData.Id.Where( c => char.IsDigit( c ) ).ToArray() );
        m_licenseActivateData.Password = EditorGUILayout.PasswordField( GUI.MakeLabel( "Activation Password" ),
                                                                        m_licenseActivateData.Password );

        InspectorGUI.SelectFolder( GUI.MakeLabel( "License File Directory" ),
                                   LicenseDirectory,
                                   "License file directory",
                                   newDirectory => {
                                     newDirectory = newDirectory.PrettyPath();

                                     if ( string.IsNullOrEmpty( newDirectory ) )
                                       newDirectory = "Assets";

                                     if ( !Directory.Exists( newDirectory ) ) {
                                       Debug.LogWarning( $"Invalid license directory: {newDirectory} - directory doesn't exist." );
                                       return;
                                     }
                                     else if ( !IO.Utils.IsValidProjectFolder( newDirectory ) ) {
                                       Debug.LogWarning( $"Invalid license directory: {newDirectory} - directory has to be in the project." );
                                       return;
                                     }
                                     LicenseDirectory = newDirectory;
                                   } );

        var validId = int.TryParse( m_licenseActivateData.Id, out var licenseId ) && licenseId > 0;
        if ( m_licenseActivateData.Id.Length > 0 && !validId )
          EditorGUILayout.HelpBox( "Enter a license ID between 1 and 2147483647.", MessageType.Error, true );

        if ( !string.IsNullOrEmpty( m_activationError ) )
          EditorGUILayout.HelpBox( m_activationError, MessageType.Error, true );

        using ( new GUI.EnabledBlock( UnityEngine.GUI.enabled && validId &&
                                      !LicenseWarnings.CurrentLicense.IsFloating && !EditorApplication.isPlayingOrWillChangePlaymode &&
                                      m_licenseActivateData.Password.Length > 0 ) ) {
          // It isn't possible to press this button during activation.
          if ( UnityEngine.GUI.Button( EditorGUI.IndentedRect( EditorGUILayout.GetControlRect() ),
                                       GUI.MakeLabel( IsBusy ?
                                                        "Activating..." :
                                                        "Activate" ),
                                                      InspectorEditor.Skin.Button ) ) {
            ActivateLicense( licenseId );
          }
        }
      }
    }

    private void ActivateLicense( int licenseId )
    {
      m_activationError = null;
      m_operationError = null;
      m_operation = LicenseOperation.Activate;
      var completion = new TaskCompletionSource<LicenseOperationResult>();
      m_licenseOperationTask = completion.Task;
      try {
        AGXUnity.LicenseManager.ActivateAsync( licenseId,
                                             m_licenseActivateData.Password,
                                             LicenseDirectory,
                                             success => {
                                               var info = AGXUnity.LicenseInfo.Create();
                                               if ( !success )
                                                 Debug.LogError( "License Error: ".Color( Color.red ) + LicenseWarnings.GetActivationError( info ) );
                                               completion.TrySetResult( new LicenseOperationResult { Success = success, LicenseInfo = info } );
                                             } );
      }
      catch ( System.Exception e ) {
        Debug.LogException( e );
        completion.TrySetResult( new LicenseOperationResult {
          LicenseInfo = AGXUnity.LicenseInfo.Create(),
          Error = LicenseWarnings.GetActivationError( new AGXUnity.LicenseInfo() )
        } );
      }
    }

    private void LicenseDataGUI( LicenseData data )
    {
      var highlight = m_licenseData.Count > 1 &&
                      !IsUpdatingLicenseInformation &&
                      !IsBusy &&
                      ( data.LicenseInfo.IsFloating ? IsActiveFloatingFile( data.Filename ) :
                        data.Filename == FindActiveNonFloatingFile() );
      if ( highlight && m_activeLicenseStyle == null )
        m_activeLicenseStyle = new GUIStyle( InspectorEditor.Skin.Label );
      // The texture is deleted when hitting stop in the editor while
      // m_activeLicenseStyle != null.
      if ( m_activeLicenseStyle != null && m_activeLicenseStyle.normal.background == null )
        m_activeLicenseStyle.normal.background = GUI.CreateColoredTexture( 1,
                                                                           1,
                                                                           Color.Lerp( InspectorGUI.BackgroundColor,
                                                                                       Color.green,
                                                                                       0.025f ) );

      var canChange = UnityEngine.GUI.enabled && !EditorApplication.isPlayingOrWillChangePlaymode;
      var floating = data.LicenseInfo.IsFloating;
      var identifiedService = data.LicenseInfo.Type == AGXUnity.LicenseInfo.LicenseType.Service && !floating;
      var unknownFloatingSource = floating && LicenseWarnings.CurrentLicense.IsFloating &&
                                  string.IsNullOrEmpty( AGXUnity.LicenseManager.ActiveFloatingLicenseFilename );
      var licenseFileButtons = new List<InspectorGUI.MiscButtonData>();
      if ( !floating )
        licenseFileButtons.Add( InspectorGUI.MiscButtonData.Create( MiscIcon.Update,
                                  () => RefreshLicense( data ),
                                  canChange && !LicenseWarnings.CurrentLicense.IsFloating &&
                                  AGXUnity.LicenseManager.GetLicenseType( data.Filename ) == AGXUnity.LicenseInfo.LicenseType.Service,
                                  LicenseWarnings.CurrentLicense.IsFloating ? "Return the floating seat before refreshing a license." : "Refresh license from server." ) );
      licenseFileButtons.Add( InspectorGUI.MiscButtonData.Create( MiscIcon.EntryRemove,
                                () => DeleteLicense( data ),
                                canChange && !unknownFloatingSource,
                                unknownFloatingSource ? "Return the current seat before deleting floating files: its source is unknown." :
                                floating ? "Delete floating license file. A seat held from this file must be returned first." :
                                identifiedService && !LicenseWarnings.CurrentLicense.IsFloating ? "Delete or deactivate and delete license file." : "Delete license file." ) );

      var highlightScope = highlight ? new EditorGUILayout.VerticalScope( m_activeLicenseStyle ) : null;
      InspectorGUI.SelectableTextField( GUI.MakeLabel( "License file" ),
                                        data.Filename,
                                        licenseFileButtons.ToArray() );
      InspectorGUI.SelectableTextField( GUI.MakeLabel( "License type" ),
                                        floating ? "Floating" : data.LicenseInfo.TypeDescription );

      if ( floating ) {
        using ( new GUI.EnabledBlock( canChange && !LicenseWarnings.CurrentLicense.IsFloating ) ) {
          if ( GUILayout.Button( GUI.MakeLabel( "Connect", false,
                                 LicenseWarnings.CurrentLicense.IsFloating ? "Return the current seat before connecting." : "Request a floating license seat from this file's server." ),
                                 InspectorEditor.Skin.Button ) )
            ConnectLicense( data.Filename );
        }
      }

      InspectorGUI.Separator( 1, 6 );

      InspectorGUI.LicenseEndDateField( data.LicenseInfo );

      EditorGUILayout.EnumFlagsField( GUI.MakeLabel( "Enabled modules",
                                                     false,
                                                     data.LicenseInfo.EnabledModules.ToString() ),
                                      data.LicenseInfo.AllModulesEnabled ?
                                        AGXUnity.LicenseInfo.Module.All :
                                        data.LicenseInfo.EnabledModules,
                                      false,
                                      InspectorEditor.Skin.Popup );

      InspectorGUI.SelectableTextField( GUI.MakeLabel( "User" ), data.LicenseInfo.User );

      InspectorGUI.SelectableTextField( GUI.MakeLabel( "Contact" ), data.LicenseInfo.Contact );
      highlightScope?.Dispose();
    }

    private static bool IsActiveFloatingFile( string filename )
    {
      var active = AGXUnity.LicenseManager.ActiveFloatingLicenseFilename;
      return !string.IsNullOrEmpty( active ) &&
             string.Equals( Path.GetFullPath( filename ).Replace( '\\', '/' ), active,
                            Path.DirectorySeparatorChar == '\\' ? System.StringComparison.OrdinalIgnoreCase : System.StringComparison.Ordinal );
    }

    private string FindActiveNonFloatingFile()
    {
      var active = LicenseWarnings.CurrentLicense;
      if ( active.IsFloating || string.IsNullOrEmpty( active.UniqueId ) )
        return null;
      var matches = m_licenseData.Where( data => !data.LicenseInfo.IsFloating && data.LicenseInfo.IsParsed &&
                                                 data.LicenseInfo.UniqueId == active.UniqueId ).ToArray();
      return matches.Length == 1 ? matches[ 0 ].Filename : null;
    }

    private void DeleteLicense( LicenseData data )
    {
      var filename = Path.GetFileName( data.Filename );
      m_operationError = null;
      // Query again at the point of action in case the file changed after scanning.
      var info = AGXUnity.LicenseManager.QueryInfo( data.Filename );
      if ( info.IsFloating && AGXUnity.LicenseManager.HasFloatingSession ) {
        if ( string.IsNullOrEmpty( AGXUnity.LicenseManager.ActiveFloatingLicenseFilename ) ) {
          m_operationError = "Return the current floating seat before deleting floating files. Its source file is unknown.";
          return;
        }
        if ( IsActiveFloatingFile( data.Filename ) ) {
          if ( EditorUtility.DisplayDialog( $"Delete \"{filename}\"?",
                                            "Return the floating seat to the server, then delete this license file? The file will be preserved if the return fails.",
                                            "Return Seat and Delete", "Cancel" ) )
            ReturnSeat( data.Filename );
          return;
        }
      }

      if ( info.Type == AGXUnity.LicenseInfo.LicenseType.Service && !info.IsFloating && !AGXUnity.LicenseManager.HasFloatingSession ) {
        var choice = EditorUtility.DisplayDialogComplex( $"Delete or deactivate and delete \"{filename}\"?",
                                                         "Deactivating contacts the license server and removes this installation, allowing the license to be activated again using its License ID and Password.",
                                                         "Deactivate and Delete", "Cancel", "Delete" );
        if ( choice == 1 )
          return;
        if ( !( choice == 0 ? AGXUnity.LicenseManager.DeactivateAndDelete( data.Filename ) : AGXUnity.LicenseManager.DeleteFile( data.Filename ) ) )
          m_operationError = $"Unable to remove \"{filename}\". Check the Console for details.";
      }
      else {
        if ( !EditorUtility.DisplayDialog( $"Delete \"{filename}\"?", "Delete this license file from the project?", "Delete", "Cancel" ) )
          return;
        if ( !AGXUnity.LicenseManager.DeleteFile( data.Filename ) )
          m_operationError = $"Unable to delete \"{filename}\". Check the Console for details.";
      }
      LicenseWarnings.Capture( AGXUnity.LicenseManager.UpdateLicenseInformation() );
      StartUpdateLicenseInformation();
    }

    private void ConnectLicense( string filename )
    {
      BeginFloatingOperation( LicenseOperation.Connect, done => AGXUnity.LicenseManager.ConnectFloatingAsync( filename, done ) );
    }

    private void ReturnSeat( string deleteAfterReturn = null )
    {
      m_deleteAfterReturn = deleteAfterReturn;
      BeginFloatingOperation( LicenseOperation.Return, AGXUnity.LicenseManager.ReturnFloatingAsync );
    }

    private void BeginFloatingOperation( LicenseOperation operation, System.Action<System.Action<bool>> start )
    {
      m_operation = operation;
      m_operationError = null;
      var completion = new TaskCompletionSource<LicenseOperationResult>();
      m_licenseOperationTask = completion.Task;
      try {
        start( success => completion.TrySetResult( new LicenseOperationResult {
          Success = success,
          Error = success ? null : AGXUnity.LicenseManager.LastOperationError ?? $"{operation} failed. Check the Console for details.",
          LicenseInfo = AGXUnity.LicenseInfo.Create()
        } ) );
      }
      catch ( System.Exception e ) {
        completion.TrySetResult( new LicenseOperationResult { Error = $"{operation} failed. {e.Message}", LicenseInfo = AGXUnity.LicenseInfo.Create() } );
      }
    }

    private static EditorDataEntry GetLicenseDirectoryData()
    {
      return EditorData.Instance.GetStaticData( "LicenseManagerWindow_LicenseFilename",
                                                entry => entry.String = "Assets" );
    }

    private void ValidateLicenseDirectory()
    {
      if ( !Directory.Exists( LicenseDirectory ) )
        LicenseDirectory = "Assets";
    }

    private void StartUpdateLicenseInformation()
    {
      m_updateLicenseInformationRequested = true;
      m_licenseReadError = null;
    }

    private void OnEditorUpdate()
    {
      // License operations invoke callbacks from worker threads.
      // Wait until the native task has finished before reading files or UI state.
      if ( AGXUnity.LicenseManager.IsBusy )
        return;

      if ( m_licenseOperationTask != null && m_licenseOperationTask.IsCompleted ) {
        var result = m_licenseOperationTask.Result;
        m_licenseOperationTask = null;
        LicenseWarnings.Capture( result.LicenseInfo );
        if ( m_operation == LicenseOperation.Activate ) {
          if ( result.Success ) {
            m_licenseActivateData = IdPassword.Empty();
            m_activationError = null;
          }
          else
            m_activationError = result.Error ?? LicenseWarnings.GetActivationError( result.LicenseInfo );
          m_clearFocus = true;
        }
        else if ( m_operation == LicenseOperation.Connect || m_operation == LicenseOperation.Return ) {
          m_operationError = result.Success ? null : result.Error;
          if ( result.Success ) {
            m_activationError = null;
            m_importMessage = null;
            if ( m_operation == LicenseOperation.Return && !string.IsNullOrEmpty( m_deleteAfterReturn ) &&
                 !AGXUnity.LicenseManager.DeleteFile( m_deleteAfterReturn ) )
              m_operationError = "The seat was returned, but the license file could not be deleted. Check the Console for details.";
          }
          m_deleteAfterReturn = null;
        }
        else if ( result.Success && result.LicenseInfo.IsValid )
          m_activationError = null;
        else if ( !result.Success )
          m_operationError = result.Error;
        if ( m_operation == LicenseOperation.Refresh && !string.IsNullOrEmpty( m_restoreAfterRefresh ) ) {
          AGXUnity.LicenseManager.LoadFile( m_restoreAfterRefresh );
          LicenseWarnings.Capture( AGXUnity.LicenseManager.UpdateLicenseInformation() );
          m_restoreAfterRefresh = null;
        }
        m_operation = LicenseOperation.None;
        RemoveNotification();
        StartUpdateLicenseInformation();
        Repaint();
      }

      if ( IsBusy )
        return;

      if ( m_updateLicenseInfoTask != null && m_updateLicenseInfoTask.IsCompleted ) {
        var licenseData = m_updateLicenseInfoTask.Result;
        m_updateLicenseInfoTask = null;
        if ( licenseData != null )
          m_licenseData = licenseData;
        else
          m_licenseReadError = "Unable to read license information. Check the Console for details.";
        RemoveNotification();
        Repaint();
      }

      if ( m_updateLicenseInfoTask != null )
        return;

      if ( AGXUnity.NativeHandler.HasInstance && AGXUnity.NativeHandler.Instance.Initialized &&
           ( m_checkLicenseValidity ||
             LicenseWarnings.CurrentLicense.IsFloating != AGXUnity.LicenseManager.HasFloatingSession ||
             LicenseWarnings.CurrentLicense.IsValid != agx.Runtime.instance().isValid() ) ) {
        m_checkLicenseValidity = false;
        var info = AGXUnity.LicenseManager.UpdateLicenseInformation();
        LicenseWarnings.Capture( info );
        if ( info.IsValid )
          m_activationError = null;
        Repaint();
      }

      if ( m_updateLicenseInformationRequested ) {
        m_updateLicenseInformationRequested = false;
        m_updateLicenseInfoTask = Task.Run( () => {
          try {
            var licenseData = new List<LicenseData>();
            foreach ( var licenseFile in AGXUnity.LicenseManager.FindLicenseFiles() ) {
              var info = AGXUnity.LicenseManager.QueryInfo( licenseFile );
              licenseData.Add( new LicenseData { Filename = licenseFile, LicenseInfo = info } );
            }
            return licenseData;
          }
          catch ( System.Exception e ) {
            Debug.LogException( e );
            return null;
          }
        } );
      }
    }

    private void RefreshLicense( LicenseData licenseData )
    {
      var previous = FindActiveNonFloatingFile();
      m_restoreAfterRefresh = previous != licenseData.Filename ? previous : null;
      m_operation = LicenseOperation.Refresh;
      m_operationError = null;
      var completion = new TaskCompletionSource<LicenseOperationResult>();
      m_licenseOperationTask = completion.Task;
      try {
        AGXUnity.LicenseManager.RefreshAsync( licenseData.Filename,
                                             success => {
                                               completion.TrySetResult( new LicenseOperationResult {
                                                 Success = success,
                                                 Error = success ? null : AGXUnity.LicenseManager.LastOperationError,
                                                 LicenseInfo = AGXUnity.LicenseInfo.Create()
                                               } );
                                             } );
      }
      catch ( System.Exception e ) {
        Debug.LogException( e );
        completion.TrySetResult( new LicenseOperationResult { LicenseInfo = AGXUnity.LicenseInfo.Create(), Error = e.Message } );
      }
    }

    internal struct IdPassword
    {
      public static IdPassword Empty() { return new IdPassword() { Id = string.Empty, Password = string.Empty }; }
      public string Id;
      public string Password;
    }

    private struct LicenseData
    {
      public string Filename;
      public AGXUnity.LicenseInfo LicenseInfo;
    }

    private struct LicenseOperationResult
    {
      public bool Success;
      public AGXUnity.LicenseInfo LicenseInfo;
      public string Error;
    }

    private enum LicenseOperation { None, Activate, Refresh, Connect, Return }

    private IdPassword m_licenseActivateData = IdPassword.Empty();
    private Vector2 m_scroll = Vector2.zero;
    [System.NonSerialized]
    private List<LicenseData> m_licenseData = new List<LicenseData>();
    private Task<List<LicenseData>> m_updateLicenseInfoTask = null;
    private Task<LicenseOperationResult> m_licenseOperationTask = null;
    private bool m_updateLicenseInformationRequested = false;
    private LicenseOperation m_operation = LicenseOperation.None;
    private string m_deleteAfterReturn;
    private string m_restoreAfterRefresh;
    private string m_operationError;
    private string m_importMessage;
    private bool m_checkLicenseValidity = false;
    private bool m_clearFocus = false;
    private string m_activationError = null;
    private string m_licenseReadError = null;
    [System.NonSerialized]
    private GUIStyle m_activeLicenseStyle = null;

    private static readonly string s_licenseManagerUrl = @"https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#license-manager";
    private static readonly string s_licensingUrl = @"https://us.download.algoryx.se/AGXUnity/documentation/current/getting_started.html#licensing";
    private static readonly string s_freeTrialUrl = @"https://www.algoryx.se/agx-unity/";
  }
}
