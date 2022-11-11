using System;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.IO;
using GUI = AGXUnity.Utils.GUI;
using Object = UnityEngine.Object;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( RestoredAGXFile ) )]
  public class RestoredAGXFileTool : CustomTargetTool
  {
    public RestoredAGXFile RestoredAGXFile
    {
      get
      {
        return Targets[ 0 ] as RestoredAGXFile;
      }
    }

    public RestoredAGXFileTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnPreTargetMembersGUI()
    {
      var skin           = InspectorEditor.Skin;
      var directory      = AssetDatabase.GUIDToAssetPath( RestoredAGXFile.DataDirectoryId );
      var directoryValid = directory.Length > 0 && AssetDatabase.IsValidFolder( directory );

      using ( new GUILayout.HorizontalScope() ) {
        EditorGUILayout.PrefixLabel( GUI.MakeLabel( "Data directory" ),
                                     skin.Label );

        var statusColor = directoryValid ?
                            Color.Lerp( InspectorGUI.BackgroundColor, Color.green, EditorGUIUtility.isProSkin ? 0.8f : 0.2f ) :
                            Color.Lerp( Color.white, Color.red, EditorGUIUtility.isProSkin ? 0.8f : 0.2f );
        using ( new GUI.BackgroundColorBlock( statusColor ) )
          EditorGUILayout.SelectableLabel( directory,
                                           skin.TextField,
                                           GUILayout.Height( EditorGUIUtility.singleLineHeight ) );

        if ( GUILayout.Button( GUI.MakeLabel( "...", false, "Open file panel" ),
                                skin.Button,
                                GUILayout.Width( 28 ) ) ) {
          var newDirectory = EditorUtility.OpenFolderPanel( "Prefab data directory", "Assets", "" );
          if ( newDirectory.Length > 0 ) {
            var relPath = IO.Utils.MakeRelative( newDirectory, Application.dataPath ).Replace( '\\', '/' );
            if ( AssetDatabase.IsValidFolder( relPath ) ) {
              RestoredAGXFile.DataDirectoryId = AssetDatabase.AssetPathToGUID( relPath );
              EditorUtility.SetDirty( RestoredAGXFile );
            }
          }
        }
      }

      AssemblyTool.OnObjectListsGUI( this );
    }
  }
}
