using System;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.IO;
using GUI = AGXUnityEditor.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( RestoredAGXFile ) )]
  public class RestoredAGXFileTool : Tool
  {
    public RestoredAGXFile RestoredAGXFile { get; private set; }

    public RestoredAGXFileTool( RestoredAGXFile restoredAGXFile )
    {
      RestoredAGXFile = restoredAGXFile;
    }

    public override void OnPreTargetMembersGUI( GUISkin skin )
    {
      var directory      = AssetDatabase.GUIDToAssetPath( RestoredAGXFile.DataDirectoryId );
      var directoryValid = directory.Length > 0 && AssetDatabase.IsValidFolder( directory );

      using ( new GUILayout.HorizontalScope() ) {
        GUILayout.Label( GUI.MakeLabel( "Data directory" ), skin.label, GUILayout.Width( 160 ) );

        var statusColor = directoryValid ?
                            Color.Lerp( Color.white, Color.green, 0.2f ) :
                            Color.Lerp( Color.white, Color.red, 0.2f );
        var prevColor   = UnityEngine.GUI.backgroundColor;

        UnityEngine.GUI.backgroundColor = statusColor;
        GUILayout.TextField( directory, skin.textField );
        UnityEngine.GUI.backgroundColor = prevColor;
        if ( GUILayout.Button( GUI.MakeLabel( "...", false, "Open file panel" ),
                                skin.button,
                                GUILayout.Width( 28 ) ) ) {
          var newDirectory = EditorUtility.OpenFolderPanel( "Prefab data directory", "Assets", "" );
          if ( newDirectory.Length > 0 ) {
            var relPath = IO.AGXFileInfo.MakeRelative( newDirectory, Application.dataPath ).Replace( '\\', '/' );
            if ( AssetDatabase.IsValidFolder( relPath ) ) {
              RestoredAGXFile.DataDirectoryId = AssetDatabase.AssetPathToGUID( relPath );
              EditorUtility.SetDirty( RestoredAGXFile );
            }
          }
        }
      }
    }
  }
}
