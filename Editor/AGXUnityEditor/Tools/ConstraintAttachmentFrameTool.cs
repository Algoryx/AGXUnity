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

      var position = EditorGUI.IndentedRect( EditorGUILayout.GetControlRect( false, 24.0f ) );
      position.width = 24.0f;
      var toggleSynchronized = UnityEngine.GUI.Button( position,
                                                       GUI.MakeLabel( "",
                                                                      false,
                                                                      "Synchronized with reference frame." ),
                                                       skin.GetButton( connectedFrameSynchronized ) );
      var iconName = connectedFrameSynchronized ? "agx_unity_hinge 7" : "agx_unity_hinge 6";
      using ( new GUI.ColorBlock( InspectorGUISkin.BrandColor ) )
        UnityEngine.GUI.DrawTexture( IconManager.GetIconRect( position ), IconManager.GetIcon( iconName ) );
      if ( toggleSynchronized ) {
        foreach ( var ap in AttachmentPairs )
          ap.Synchronized = !connectedFrameSynchronized;

        if ( !isMultiSelect && AttachmentPairs[ 0 ].Synchronized )
          ConnectedFrameTool.TransformHandleActive = false;
      }

      var connectedFrameContent = GUI.MakeLabel( $"<b>Connected frame:</b> {(connectedFrameSynchronized ? "Synchronized" : "Free")}" );
      position.x += 26.0f;
      position.width = InspectorGUI.GetWidth( connectedFrameContent, skin.Label );
      UnityEngine.GUI.Label( position, connectedFrameContent, skin.LabelMiddleLeft );

      UnityEngine.GUI.enabled = !connectedFrameSynchronized && !isMultiSelect;
      InspectorGUI.HandleFrames( AttachmentPairs.Select( ap => ap.ConnectedFrame ).ToArray(), 1 );
      UnityEngine.GUI.enabled = guiWasEnabled;
    }
  }
}
