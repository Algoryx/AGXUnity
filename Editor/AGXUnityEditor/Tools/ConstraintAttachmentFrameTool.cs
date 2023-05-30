using System.Linq;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using GUI = AGXUnity.Utils.GUI;

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

      GUILayout.Space( 4 );

      var rect = EditorGUILayout.GetControlRect( false, EditorGUIUtility.singleLineHeight );
      var orgWidth = rect.xMax;
      UnityEngine.GUI.Label( EditorGUI.IndentedRect( rect ),
                             GUI.MakeLabel( "Connected frame", true ),
                             InspectorEditor.Skin.Label );

      var buttonWidth        = 1.1f * EditorGUIUtility.singleLineHeight;
      var buttonHeightOffset = 1.0f;
      rect.x                += EditorGUIUtility.labelWidth;
      rect.y                -= buttonHeightOffset;
      rect.width             = buttonWidth;
      var toggleSynchronized = InspectorGUI.Button( rect,
                                                    connectedFrameSynchronized ?
                                                      MiscIcon.SynchEnabled :
                                                      MiscIcon.SynchDisabled,
                                                    true,
                                                    "Toggle synchronized with reference frame.",
                                                    0.9f );
      rect.x    += rect.width + 2.0f;
      rect.width = orgWidth - rect.x;
      rect.y    += buttonHeightOffset;

      UnityEngine.GUI.Label( rect,
                             GUI.MakeLabel( $"{( connectedFrameSynchronized ? "Synchronized" : "Free" )}" ),
                             InspectorEditor.Skin.Label );

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
