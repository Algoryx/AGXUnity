using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using AGXUnity;

using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( RigidBodyEmitter ) )]
  public class RigidBodyEmitterTool : CustomTargetTool
  {
    public RigidBodyEmitter Emitter { get { return Targets[ 0 ] as RigidBodyEmitter; } }

    public RigidBodyEmitterTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnAdd()
    {
      Emitter.RemoveInvalidTemplates();

      var directories = Directory.GetDirectories( "Assets",
                                                  "Resources",
                                                  SearchOption.AllDirectories ).Select( dir => dir.Replace( '\\', '/' ) );
      m_availableTemplates = ( from dir in directories
                               from guid in AssetDatabase.FindAssets( "t:GameObject", new string[] { dir } ).Distinct()
                               let assetPath = AssetDatabase.GUIDToAssetPath( guid )
                               let rb = AssetDatabase.LoadAssetAtPath<GameObject>( assetPath ).GetComponent<RigidBody>()
                               where rb != null
                               select rb ).ToArray();
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
      // Is this required? Is Unity figuring out we're referencing this
      // component/game object even if it isn't in a Resources folder?
      if ( !assetPath.Contains( "/Resources/" ) ) {
        Debug.LogWarning( $"Emitter template: {template.name} isn't located in a 'Resources' folder." );
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
