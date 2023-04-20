using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Windows
{
  public class ExamplesWindow : EditorWindow
  {
    public static readonly string DemoPackageName = "AGXDynamicsForUnityDemo";
    public static readonly string StandalonePackageName = "AGXUnity_ExamplesWindow";

    [MenuItem( "AGXUnity/Examples", priority = 5 )]
    public static ExamplesWindow Open()
    {
      return GetWindow<ExamplesWindow>( false,
                                        "AGX Dynamics for Unity Examples",
                                        true );
    }

    private void OnEnable()
    {
      var thumbnailDirectory = FindThumbnailDirectory();
      ExamplesManager.Initialize();
      EditorApplication.update += OnUpdate;
    }

    private void OnDisable()
    {
      EditorApplication.update -= OnUpdate;
      ExamplesManager.Uninitialize();
    }

    private void OnGUI()
    {
      minSize = new Vector2(400, 0);
      if ( m_exampleNameStyle == null ) {
        m_exampleNameStyle = new GUIStyle( InspectorEditor.Skin.Label );
        m_exampleNameStyle.alignment = TextAnchor.MiddleLeft;
      }

      using ( GUI.AlignBlock.Center )
        GUILayout.Box( IconManager.GetAGXUnityLogo(),
                       GUI.Skin.customStyles[ 3 ],
                       GUILayout.Width( 400 ),
                       GUILayout.Height( 100 ) );

      EditorGUILayout.LabelField( "© " + System.DateTime.Now.Year + " Algoryx Simulation AB",
                                  InspectorEditor.Skin.LabelMiddleCenter );

      InspectorGUI.BrandSeparator( 1, 6 );

      var textStyle = new GUIStyle(InspectorEditor.Skin.LabelMiddleCenter) { wordWrap = true };

      EditorGUILayout.LabelField( "Check out the documentation for the examples by clicking the example name in the list.",
                                  textStyle );

      if ( EditorApplication.isPlayingOrWillChangePlaymode )
        ShowNotification( GUI.MakeLabel( "Playing..." ) );
      else if ( ExamplesManager.IsInitializing )
        ShowNotification( GUI.MakeLabel( "Initializing..." ), 0.1 );
      else if ( EditorApplication.isCompiling )
        ShowNotification( GUI.MakeLabel( "Compiling..." ), 0.1 );
      else if ( ExamplesManager.IsInstallingDependencies )
        ShowNotification( GUI.MakeLabel( "Installing..." ) );

      m_scroll = EditorGUILayout.BeginScrollView( m_scroll );

      bool hasDownloads = false;
      foreach ( var data in ExamplesManager.Examples ) {

        using ( new EditorGUILayout.HorizontalScope() ) {
          GUILayout.Box( data.Thumbnail,
                         GUILayout.Width( 64 ),
                         GUILayout.Height( 64 ) );
          var exampleNameLabel = GUI.MakeLabel( $"{data.Name}", true );
          if ( data != null ) {
            if ( Link( exampleNameLabel, GUILayout.Height( 64 ) ) )
              Application.OpenURL( data.DocumentationUrl );
          }
          else
            GUILayout.Label( exampleNameLabel,
                             m_exampleNameStyle,
                             GUILayout.Height( 64 ) );
          
          GUILayout.FlexibleSpace();

          var hasUnresolvedIssues = ExamplesManager.HasUnresolvedIssues( data );

          var buttonText = ExamplesManager.IsInitializing ?
                             "Initializing..." :
                           data.Status == ExamplesManager.ExampleData.State.Installed ?
                             "Load" :
                           data.Status == ExamplesManager.ExampleData.State.Downloading ?
                             "Cancel" :
                           data.Status == ExamplesManager.ExampleData.State.ReadyToInstall ||
                           data.Status == ExamplesManager.ExampleData.State.Installing ?
                             "Importing..." :
                             $"Download ({data.SizeString})";
          var buttonEnabled = !ExamplesManager.IsInitializing &&
                              !EditorApplication.isPlayingOrWillChangePlaymode &&
                              !EditorApplication.isCompiling &&
                              data != null &&
                              !hasUnresolvedIssues &&
                              !ExamplesManager.IsInstallingDependencies &&
                              (
                                // "Install"
                                data.Status == ExamplesManager.ExampleData.State.NotInstalled ||
                                // "Cancel"
                                data.Status == ExamplesManager.ExampleData.State.Downloading ||
                                // "Load"
                                data.Status == ExamplesManager.ExampleData.State.Installed
                              );

          using ( new EditorGUILayout.VerticalScope() ) {
            GUILayout.Space( 0.5f * ( 64 - 18 ) );
            using ( new EditorGUILayout.HorizontalScope() ) {
              if ( hasUnresolvedIssues ) {
                var dependencyContextButtonWidth = 18.0f;

                var hasUnresolvedDependencies  = ExamplesManager.HasUnresolvedDependencies( data );
                var hasUnresolvedInputSettings = ( data.RequiresLegacyInputManager &&
                                                 !ExamplesManager.LegacyInputManagerEnabled ) ||
                                               ( data.Dependencies.Contains( "com.unity.inputsystem" ) &&
                                                 !ExamplesManager.InputSystemEnabled );

                var contextButton = InspectorGUI.Button(MiscIcon.ContextDropdown,
                                                       !ExamplesManager.IsInstallingDependencies &&
                                                       !EditorApplication.isPlayingOrWillChangePlaymode,
                                                       ( hasUnresolvedDependencies ?
                                                           "Required dependencies." :
                                                           "Input settings has to be resolved." ),
                                                       1.1f,
                                                       GUILayout.Width(dependencyContextButtonWidth));
                if ( contextButton ) {
                  var dependenciesMenu = new GenericMenu();
                  if ( hasUnresolvedDependencies ) {
                    dependenciesMenu.AddDisabledItem( GUI.MakeLabel( "Install dependency..." ) );
                    dependenciesMenu.AddSeparator( string.Empty );
                    foreach ( var dependency in data.Dependencies )
                      dependenciesMenu.AddItem( GUI.MakeLabel( dependency.ToString() ),
                                                false,
                                                () => ExamplesManager.InstallDependency( dependency ) );
                  }
                  else {
                    dependenciesMenu.AddDisabledItem( GUI.MakeLabel( "Resolve input settings..." ) );
                    dependenciesMenu.AddSeparator( string.Empty );
                    dependenciesMenu.AddItem( GUI.MakeLabel( "Enable both (legacy and new) Input Systems" ),
                                              false,
                                              () => ExamplesManager.ResolveInputSystemSettings() );
                  }
                  dependenciesMenu.ShowAsContext();
                }
              }
              using ( new GUI.EnabledBlock( buttonEnabled ) ) {

                if ( GUILayout.Button( GUI.MakeLabel( buttonText ),
                                     InspectorEditor.Skin.Button,
                                     GUILayout.MinWidth( 130 ),
                                     GUILayout.Height( 18 ) ) ) {
                  if ( data.Status == ExamplesManager.ExampleData.State.NotInstalled )
                    ExamplesManager.Download( data );
                  else if ( data.Status == ExamplesManager.ExampleData.State.Downloading )
                    ExamplesManager.CancelDownload( data );
                  else if ( data.Status == ExamplesManager.ExampleData.State.Installed ) {
                    if ( !string.IsNullOrEmpty( data.Scene ) )
                      EditorSceneManager.OpenScene( data.Scene, OpenSceneMode.Single );
                    else
                      Debug.LogWarning( $"Unable to find .unity scene file for example: {data.Name}" );
                  }
                }

                if ( data != null ) {
                  if ( data.Status == ExamplesManager.ExampleData.State.Downloading ) {
                    hasDownloads = true;
                    var progressRect = GUILayoutUtility.GetLastRect();
                    progressRect.y += 4.0f;
                    progressRect.height = 18.0f;
                    EditorGUI.ProgressBar( progressRect,
                                           data.DownloadProgress,
                                           $"Downloading: { (int)( 100.0f * data.DownloadProgress + 0.5f ) }%" );
                  }
                }
              }
            }
          }
        }
        InspectorGUI.Separator();
      }

      EditorGUILayout.EndScrollView();

      if ( ExamplesManager.IsInitializing || hasDownloads )
        Repaint();
    }

    private void OnUpdate()
    {
      ExamplesManager.HandleImportQueue();
    }

    private static bool Link( GUIContent content, params GUILayoutOption[] options )
    {
      content.text = GUI.AddColorTag( content.text, EditorGUIUtility.isProSkin ?
                                                      InspectorGUISkin.BrandColorBlue :
                                                      Color.Lerp( InspectorGUISkin.BrandColorBlue,
                                                                  Color.black,
                                                                  0.20f ) );
      var clicked = GUILayout.Button( content, InspectorEditor.Skin.Label, options );
      EditorGUIUtility.AddCursorRect( GUILayoutUtility.GetLastRect(), MouseCursor.Link );
      return clicked;
    }

    private string FindThumbnailDirectory()
    {
      var script = MonoScript.FromScriptableObject( this );
      if ( script == null )
        return string.Empty;

      var scriptPath = AssetDatabase.GetAssetPath( script );
      if ( string.IsNullOrEmpty( scriptPath ) )
        return string.Empty;

      var path = scriptPath.Substring( 0, scriptPath.LastIndexOf( '/' ) + 1 ) + "Images";
      if ( System.IO.Directory.Exists( path ) )
        return path;
      return string.Empty;
    }

    [System.NonSerialized]
    private GUIStyle m_exampleNameStyle = null;
    [SerializeField]
    private Vector2 m_scroll;
  }
}
