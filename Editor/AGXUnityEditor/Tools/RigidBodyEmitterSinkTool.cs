using UnityEngine;
using UnityEditor;
using AGXUnity;

using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( RigidBodyEmitterSink ) )]
  public class RigidBodyEmitterSinkTool : CustomTargetTool
  {
    public RigidBodyEmitterSink Sink { get { return Targets[ 0 ] as RigidBodyEmitterSink; } }

    public RigidBodyEmitterSinkTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnAdd()
    {
      Sink.RemoveInvalidTemplates();
      m_availableTemplates = null;
      if ( !EditorApplication.isPlayingOrWillChangePlaymode )
        m_availableTemplates = RigidBodyEmitterTool.FindAvailableTemplates();
    }

    public override void OnPostTargetMembersGUI()
    {
      if ( NumTargets != 1 )
        return;

      using ( new GUI.EnabledBlock( !EditorApplication.isPlayingOrWillChangePlaymode ) ) {
        Undo.RecordObject( Sink, "Sink template" );

        var sinkAll = InspectorGUI.Toggle( GUI.MakeLabel( "Sink All" ), Sink.SinkAll );
        if ( sinkAll != Sink.SinkAll )
          Sink.SinkAll = sinkAll;

        if ( !Sink.SinkAll ) {
          InspectorGUI.ToolListGUI( this,
                                    Sink.Templates,
                                    "Sink Templates",
                                    m_availableTemplates,
                                    OnTemplateAdd,
                                    OnTemplateRemove );
        }
      }
    }

    private void OnTemplateAdd( RigidBody template )
    {
      if ( template == null )
        return;

      var assetPath = AssetDatabase.GetAssetPath( template.gameObject );
      if ( string.IsNullOrEmpty( assetPath ) ) {
        Debug.LogWarning( $"Sink template: {template.name} isn't a prefab." );
        return;
      }

      Sink.AddTemplate( template );
    }

    private void OnTemplateRemove( RigidBody template )
    {
      Sink.RemoveTemplate( template );
    }

    private RigidBody[] m_availableTemplates = null;
  }
}
