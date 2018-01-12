using UnityEngine;
using UnityEditor;
using AGXUnity;
using GUI = AGXUnityEditor.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  public class ConstraintAttachmentFrameTool : Tool
  {
    public AttachmentPair AttachmentPair { get; private set; }

    public UnityEngine.Object OnChangeDirtyTarget { get; private set; }

    public FrameTool ReferenceFrameTool { get; private set; }

    public FrameTool ConnectedFrameTool { get; private set; }

    public ConstraintAttachmentFrameTool( AttachmentPair attachmentPair, UnityEngine.Object onChangeDirtyTarget = null )
    {
      AttachmentPair = attachmentPair;
      OnChangeDirtyTarget = onChangeDirtyTarget;
    }

    public override void OnAdd()
    {
      HideDefaultHandlesEnableWhenRemoved();

      ReferenceFrameTool = new FrameTool( AttachmentPair.ReferenceFrame )
      {
        OnChangeDirtyTarget = OnChangeDirtyTarget,
        UndoRedoRecordObject = AttachmentPair
      };
      ConnectedFrameTool = new FrameTool( AttachmentPair.ConnectedFrame )
      {
        OnChangeDirtyTarget = OnChangeDirtyTarget,
        UndoRedoRecordObject = AttachmentPair,
        TransformHandleActive = !AttachmentPair.Synchronized
      };

      AddChild( ReferenceFrameTool );
      AddChild( ConnectedFrameTool );
    }

    public override void OnRemove()
    {
      RemoveChild( ReferenceFrameTool );
      RemoveChild( ConnectedFrameTool );

      ReferenceFrameTool = ConnectedFrameTool = null;
      OnChangeDirtyTarget = null;
    }

    public override void OnPreTargetMembersGUI( GUISkin skin )
    {
      if ( AttachmentPair == null ) {
        PerformRemoveFromParent();
        return;
      }

      bool guiWasEnabled = UnityEngine.GUI.enabled;

      using ( new GUI.Indent( 12 ) ) {
        Undo.RecordObject( AttachmentPair, "Constraint Tool" );

        GUILayout.Label( GUI.MakeLabel( "Reference frame", true ), skin.label );
        GUI.HandleFrame( AttachmentPair.ReferenceFrame, skin, 4 + 12 );
        GUILayout.BeginHorizontal();
        GUILayout.Space( 12 );
        if ( GUILayout.Button( GUI.MakeLabel( GUI.Symbols.Synchronized.ToString(), false, "Synchronized with reference frame" ),
                               GUI.ConditionalCreateSelectedStyle( AttachmentPair.Synchronized, skin.button ),
                               new GUILayoutOption[] { GUILayout.Width( 24 ), GUILayout.Height( 14 ) } ) ) {
          AttachmentPair.Synchronized = !AttachmentPair.Synchronized;
          if ( AttachmentPair.Synchronized )
            ConnectedFrameTool.TransformHandleActive = false;
        }
        GUILayout.Label( GUI.MakeLabel( "Connected frame", true ), skin.label );
        GUILayout.EndHorizontal();
        UnityEngine.GUI.enabled = !AttachmentPair.Synchronized;
        GUI.HandleFrame( AttachmentPair.ConnectedFrame, skin, 4 + 12 );
        UnityEngine.GUI.enabled = guiWasEnabled;
      }
    }
  }
}
