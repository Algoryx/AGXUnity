using System.Linq;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using GUI = AGXUnityEditor.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  public class ConstraintAttachmentFrameTool : Tool
  {
    public AttachmentPair[] AttachmentPairs { get; private set; }

    public Object OnChangeDirtyTarget { get; private set; }

    public FrameTool ReferenceFrameTool { get; private set; }

    public FrameTool ConnectedFrameTool { get; private set; }

    public ConstraintAttachmentFrameTool( AttachmentPair[] attachmentPairs,
                                          Object onChangeDirtyTarget = null )
    {
      AttachmentPairs = attachmentPairs;
      OnChangeDirtyTarget = onChangeDirtyTarget;
    }

    public override void OnAdd()
    {
      HideDefaultHandlesEnableWhenRemoved();

      if ( AttachmentPairs.Length > 1 )
        return;

      ReferenceFrameTool = new FrameTool( AttachmentPairs[ 0 ].ReferenceFrame )
      {
        OnChangeDirtyTarget = OnChangeDirtyTarget,
        UndoRedoRecordObject = AttachmentPairs[ 0 ]
      };
      ConnectedFrameTool = new FrameTool( AttachmentPairs[ 0 ].ConnectedFrame )
      {
        OnChangeDirtyTarget = OnChangeDirtyTarget,
        UndoRedoRecordObject = AttachmentPairs[ 0 ],
        TransformHandleActive = !AttachmentPairs[ 0 ].Synchronized
      };

      AddChild( ReferenceFrameTool );
      AddChild( ConnectedFrameTool );
    }

    public override void OnRemove()
    {
      if ( AttachmentPairs.Length > 1 )
        return;

      RemoveChild( ReferenceFrameTool );
      RemoveChild( ConnectedFrameTool );

      ReferenceFrameTool = ConnectedFrameTool = null;
      OnChangeDirtyTarget = null;
    }

    public override void OnPreTargetMembersGUI()
    {
      OnPreTargetMembersGUI( AttachmentPairs );
    }

    public void OnPreTargetMembersGUI( AttachmentPair[] attachmentPairs )
    {
      var isMultiSelect = attachmentPairs.Length > 1;
      ReferenceFrameTool.ForceDisableTransformHandle = isMultiSelect;
      ConnectedFrameTool.ForceDisableTransformHandle = isMultiSelect;

      Undo.RecordObjects( attachmentPairs, "Constraint Attachment" );

      var skin = InspectorEditor.Skin;
      var guiWasEnabled = UnityEngine.GUI.enabled;

      using ( new GUI.Indent( 12 ) ) {
        var connectedFrameSynchronized = attachmentPairs.All( ap => ap.Synchronized );

        GUILayout.Label( GUI.MakeLabel( "Reference frame", true ), skin.label );
        GUI.HandleFrames( attachmentPairs.Select( ap => ap.ReferenceFrame ).ToArray(), 4 + 12 );

        using ( new GUILayout.HorizontalScope() ) {
          GUILayout.Space( 12 );
          if ( GUILayout.Button( GUI.MakeLabel( GUI.Symbols.Synchronized.ToString(),
                                                false,
                                                "Synchronized with reference frame" ),
                                 GUI.ConditionalCreateSelectedStyle( connectedFrameSynchronized, skin.button ),
                                 new GUILayoutOption[] { GUILayout.Width( 24 ), GUILayout.Height( 14 ) } ) ) {
            foreach ( var ap in attachmentPairs )
              ap.Synchronized = !connectedFrameSynchronized;

            if ( !isMultiSelect && AttachmentPairs[ 0 ].Synchronized )
              ConnectedFrameTool.TransformHandleActive = false;
          }
          GUILayout.Label( GUI.MakeLabel( "Connected frame", true ), skin.label );
        }

        UnityEngine.GUI.enabled = !connectedFrameSynchronized && !isMultiSelect;
        GUI.HandleFrames( attachmentPairs.Select( ap => ap.ConnectedFrame ).ToArray(), 4 + 12 );
        UnityEngine.GUI.enabled = guiWasEnabled;
      }
    }
  }
}
