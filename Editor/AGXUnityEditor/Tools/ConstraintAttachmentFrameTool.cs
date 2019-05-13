using System.Linq;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using GUI = AGXUnityEditor.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  public class ConstraintAttachmentFrameTool : CustomTargetTool
  {
    public AttachmentPair AttachmentPair { get; private set; }

    public Object OnChangeDirtyTarget { get; private set; }

    public FrameTool ReferenceFrameTool { get; private set; }

    public FrameTool ConnectedFrameTool { get; private set; }

    public ConstraintAttachmentFrameTool( AttachmentPair attachmentPair,
                                          Object onChangeDirtyTarget = null )
      : base( onChangeDirtyTarget )
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

    public override void OnPreTargetMembersGUI( InspectorEditor editor )
    {
      OnPreTargetMembersGUI( editor, new AttachmentPair[] { AttachmentPair } );
    }

    public void OnPreTargetMembersGUI( InspectorEditor editor, AttachmentPair[] attachmentPairs )
    {
      ReferenceFrameTool.ForceDisableTransformHandle = editor.IsMultiSelect;
      ConnectedFrameTool.ForceDisableTransformHandle = editor.IsMultiSelect;

      Undo.RecordObjects( attachmentPairs, "Constraint Attachment" );

      var skin = InspectorEditor.Skin;
      var guiWasEnabled = UnityEngine.GUI.enabled;

      using ( new GUI.Indent( 12 ) ) {
        var connectedFrameSynchronized = attachmentPairs.All( ap => ap.Synchronized );

        GUILayout.Label( GUI.MakeLabel( "Reference frame", true ), skin.label );
        GUI.HandleFrames( attachmentPairs.Select( ap => ap.ReferenceFrame ).ToArray(), editor, 4 + 12 );

        using ( new GUILayout.HorizontalScope() ) {
          GUILayout.Space( 12 );
          if ( GUILayout.Button( GUI.MakeLabel( GUI.Symbols.Synchronized.ToString(),
                                                false,
                                                "Synchronized with reference frame" ),
                                 GUI.ConditionalCreateSelectedStyle( connectedFrameSynchronized, skin.button ),
                                 new GUILayoutOption[] { GUILayout.Width( 24 ), GUILayout.Height( 14 ) } ) ) {
            foreach ( var ap in attachmentPairs )
              ap.Synchronized = !connectedFrameSynchronized;

            if ( !editor.IsMultiSelect && AttachmentPair.Synchronized )
              ConnectedFrameTool.TransformHandleActive = false;
          }
          GUILayout.Label( GUI.MakeLabel( "Connected frame", true ), skin.label );
        }

        UnityEngine.GUI.enabled = !connectedFrameSynchronized && !editor.IsMultiSelect;
        GUI.HandleFrames( attachmentPairs.Select( ap => ap.ConnectedFrame ).ToArray(), editor, 4 + 12 );
        UnityEngine.GUI.enabled = guiWasEnabled;
      }
    }
  }
}
