using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AGXUnity;

using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( RigidBodyEmitter ) )]
  public class RigidBodyEmitterTool : CustomTargetTool
  {
    public static RigidBody[] FindAvailableTemplates( IEnumerable<string> directories = null )
    {
      directories = directories != null ?
                      directories :
                      Directory.GetDirectories( "Assets",
                                                "Resources",
                                                SearchOption.AllDirectories ).Select( dir => dir.Replace( '\\', '/' ) );
      return ( from dir in directories
               from guid in AssetDatabase.FindAssets( "t:GameObject", new string[] { dir } ).Distinct()
               let assetPath = AssetDatabase.GUIDToAssetPath( guid )
               let rb = AssetDatabase.LoadAssetAtPath<GameObject>( assetPath ).GetComponent<RigidBody>()
               where rb != null
               select rb ).ToArray();
    }

    public RigidBodyEmitter Emitter { get { return Targets[ 0 ] as RigidBodyEmitter; } }

    public RigidBodyEmitterTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnAdd()
    {
      Emitter.RemoveInvalidTemplates();

      m_availableTemplates = null;
      if ( !EditorApplication.isPlayingOrWillChangePlaymode )
        m_availableTemplates = FindAvailableTemplates();
    }

    public override void OnRemove()
    {
      m_availableTemplates = null;
    }

    public override void OnPostTargetMembersGUI()
    {
      if ( NumTargets != 1 )
        return;

      Undo.RecordObject( Emitter, "Emitter template" );
      InspectorGUI.ToolListGUI( this,
                                Emitter.Templates,
                                "Templates",
                                OnAddTemplate,
                                OnRemoveTemplate,
                                OnRenderProbabilityWeight,
                                null,
                                m_availableTemplates );
    }

    private void OnRenderProbabilityWeight( RigidBody template, int index )
    {
      using ( InspectorGUI.IndentScope.Single ) {
        GUILayout.Space( 2 );
        var entry = Emitter.TemplateEntries[ index ];
        entry.ProbabilityWeight = Mathf.Clamp01( EditorGUILayout.FloatField( GUI.MakeLabel( "Probability weight" ),
                                                                             entry.ProbabilityWeight ) );
        GUILayout.Space( 6 );
      }
    }

    private void OnAddTemplate( RigidBody template )
    {
      if ( template == null )
        return;

      var assetPath = AssetDatabase.GetAssetPath( template.gameObject );
      if ( string.IsNullOrEmpty( assetPath ) ) {
        Debug.LogWarning( $"Emitter template: {template.name} isn't a prefab." );
        return;
      }

      Emitter.AddTemplate( template, 0.5f );
    }

    private void OnRemoveTemplate( RigidBody template )
    {
      Emitter.RemoveTemplate( template );
    }

    private RigidBody[] m_availableTemplates = null;
  }
}
