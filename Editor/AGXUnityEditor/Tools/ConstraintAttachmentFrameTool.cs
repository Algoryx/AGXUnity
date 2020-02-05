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

      using ( InspectorGUI.IndentScope.Single ) {
        var connectedFrameSynchronized = AttachmentPairs.All( ap => ap.Synchronized );

        EditorGUILayout.LabelField( GUI.MakeLabel( "Reference frame", true ), skin.Label );
        InspectorGUI.HandleFrames( AttachmentPairs.Select( ap => ap.ReferenceFrame ).ToArray(), 1 );

        using ( new GUILayout.HorizontalScope() ) {
          // TODO GUI: Fix this special button. Why 6 pixels extra?
          GUILayout.Space( InspectorGUI.IndentScope.PixelLevel + 6 );
          if ( GUILayout.Button( GUI.MakeLabel( GUI.Symbols.Synchronized.ToString(),
                                                false,
                                                "Synchronized with reference frame" ),
                                 skin.GetButton( connectedFrameSynchronized ),
                                 new GUILayoutOption[] { GUILayout.Width( 24 ), GUILayout.Height( 14 ) } ) ) {
            foreach ( var ap in AttachmentPairs )
              ap.Synchronized = !connectedFrameSynchronized;

            if ( !isMultiSelect && AttachmentPairs[ 0 ].Synchronized )
              ConnectedFrameTool.TransformHandleActive = false;
          }
          GUILayout.Label( GUI.MakeLabel( "Connected frame", true ), skin.Label );
        }

        UnityEngine.GUI.enabled = !connectedFrameSynchronized && !isMultiSelect;
        InspectorGUI.HandleFrames( AttachmentPairs.Select( ap => ap.ConnectedFrame ).ToArray(), 1 );
        UnityEngine.GUI.enabled = guiWasEnabled;
      }
    }
  }
}
