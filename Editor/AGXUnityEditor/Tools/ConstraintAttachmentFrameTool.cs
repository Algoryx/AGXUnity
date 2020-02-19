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
      : base( isSingleInstanceTool: false )
    {
      AttachmentPairs = attachmentPairs;
      OnChangeDirtyTarget = onChangeDirtyTarget;
    }

    public override void OnAdd()
    {
      HideDefaultHandlesEnableWhenRemoved();

      ReferenceFrameTool = new FrameTool( AttachmentPairs[ 0 ].ReferenceFrame )
      {
        OnChangeDirtyTarget = OnChangeDirtyTarget,
        UndoRedoRecordObject = AttachmentPairs[ 0 ],
        IsSingleInstanceTool = false
      };
      ConnectedFrameTool = new FrameTool( AttachmentPairs[ 0 ].ConnectedFrame )
      {
        OnChangeDirtyTarget = OnChangeDirtyTarget,
        UndoRedoRecordObject = AttachmentPairs[ 0 ],
        TransformHandleActive = !AttachmentPairs[ 0 ].Synchronized,
        IsSingleInstanceTool = false
      };

      AddChild( ReferenceFrameTool );
      AddChild( ConnectedFrameTool );

      var isMultiSelect = AttachmentPairs.Length > 1;
      ReferenceFrameTool.ForceDisableTransformHandle = isMultiSelect;
      ConnectedFrameTool.ForceDisableTransformHandle = isMultiSelect;
    }

    public override void OnRemove()
    {
      RemoveAllChildren();

      ReferenceFrameTool = ConnectedFrameTool = null;
      OnChangeDirtyTarget = null;
    }

    public override void OnPreTargetMembersGUI()
    {
      var isMultiSelect = AttachmentPairs.Length > 1;

      Undo.RecordObjects( AttachmentPairs, "Constraint Attachment" );

      var skin = InspectorEditor.Skin;
      var guiWasEnabled = UnityEngine.GUI.enabled;

      var connectedFrameSynchronized = AttachmentPairs.All( ap => ap.Synchronized );

      EditorGUILayout.LabelField( GUI.MakeLabel( "Reference frame", true ), skin.Label );
      InspectorGUI.HandleFrames( AttachmentPairs.Select( ap => ap.ReferenceFrame ).ToArray(), 1 );

      var rect = EditorGUI.IndentedRect( EditorGUILayout.GetControlRect( false, 20.0f ) );
      var connectedFrameContent = GUI.MakeLabel( $"<b>Connected frame:</b> {(connectedFrameSynchronized ? "Synchronized" : "Free")}" );
      //rect.x += rect.width + 4.0f;
      rect.width = InspectorGUI.GetWidth( connectedFrameContent, skin.Label );
      UnityEngine.GUI.Label( rect, connectedFrameContent, skin.LabelMiddleLeft );

      // TODO GUI: What is this extra 13?
      rect.x = Mathf.Max( rect.width + InspectorGUI.IndentScope.PixelLevel + 4.0f,
                          EditorGUIUtility.labelWidth ) + 14;
      rect.width = 20.0f;
      var toggleSynchronized = InspectorGUI.Button( rect,
                                                    connectedFrameSynchronized ?
                                                      MiscIcon.SynchEnabled :
                                                      MiscIcon.SynchDisabled,
                                                    true,
                                                    "Toggle synchronized with reference frame.",
                                                    0.85f );

      if ( toggleSynchronized ) {
        foreach ( var ap in AttachmentPairs )
          ap.Synchronized = !connectedFrameSynchronized;

        if ( !isMultiSelect && AttachmentPairs[ 0 ].Synchronized )
          ConnectedFrameTool.TransformHandleActive = false;
      }

      UnityEngine.GUI.enabled = !connectedFrameSynchronized && !isMultiSelect;
      InspectorGUI.HandleFrames( AttachmentPairs.Select( ap => ap.ConnectedFrame ).ToArray(), 1 );
      UnityEngine.GUI.enabled = guiWasEnabled;
    }
  }
}
