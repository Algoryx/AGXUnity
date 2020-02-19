using System;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  public class FrameTool : Tool
  {
    /// <summary>
    /// Search from root tool, through all children (depth first),
    /// for a frame tool operating on <paramref name="frame"/>.
    /// </summary>
    /// <param name="frame">Frame to search frame tool for.</param>
    /// <returns>First frame tool that matches <paramref name="frame"/>.</returns>
    public static FrameTool FindActive( IFrame frame )
    {
      return ToolManager.FindActive<FrameTool>( frameTool => frameTool.Frame == frame );
    }

    /// <summary>
    /// Frame this tool controls.
    /// </summary>
    public IFrame Frame { get; set; }

    /// <summary>
    /// Size/Scale of this tool, if 1.0f it'll be the size of the default
    /// Unity position/rotation handle.
    /// </summary>
    public float Size { get; set; }

    /// <summary>
    /// Transparency value, default 1.0f.
    /// </summary>
    public float Alpha { get; set; }

    /// <summary>
    /// Enable/disable SelectGameObjectTool to set frame parent from picking
    /// in scene view.
    /// </summary>
    public bool SelectParent
    {
      get { return GetChild<SelectGameObjectTool>() != null; }
      set
      {
        if ( value && GetChild<SelectGameObjectTool>() == null ) {
          RemoveAllChildren();
          var selectGameObjectTool = new SelectGameObjectTool();
          selectGameObjectTool.OnSelect += parent =>
          {
            Frame.SetParent( parent );

            OnLocalToolDone( selectGameObjectTool );
          };
          AddChild( selectGameObjectTool );
        }
        else if ( !value )
          RemoveChild( GetChild<SelectGameObjectTool>() );
      }
    }

    /// <summary>
    /// Enable/disable FindPointTool to find transform given surface/triangle.
    /// </summary>
    public bool FindTransformGivenPointOnSurface
    {
      get { return GetChild<FindPointTool>() != null; }
      set
      {
        if ( value && GetChild<FindPointTool>() == null ) {
          RemoveAllChildren();
          FindPointTool pointTool = new FindPointTool();
          pointTool.OnPointFound = data =>
          {
            Frame.SetParent( data.Target );
            Frame.Position = data.RaycastResult.Point;
            Frame.Rotation = data.Rotation;

            OnLocalToolDone( pointTool );
          };
          AddChild( pointTool );
        }
        else if ( !value )
          RemoveChild( GetChild<FindPointTool>() );
      }
    }

    /// <summary>
    /// Enable/disable EdgeDetectionTool to find transform given edge.
    /// </summary>
    public bool FindTransformGivenEdge
    {
      get { return GetChild<EdgeDetectionTool>() != null; }
      set
      {
        if ( value && GetChild<EdgeDetectionTool>() == null ) {
          RemoveAllChildren();
          EdgeDetectionTool edgeTool = new EdgeDetectionTool();
          edgeTool.OnEdgeFound += data =>
          {
            Frame.SetParent( data.Target );
            Frame.Position = data.Position;
            Frame.Rotation = data.Rotation;

            OnLocalToolDone( edgeTool );
          };

          AddChild( edgeTool );
        }
        else if ( !value )
          RemoveChild( GetChild<EdgeDetectionTool>() );
      }
    }

    /// <summary>
    /// Enable/disable transform handle tool. Default enable.
    /// </summary>
    public bool TransformHandleActive { get; set; }

    /// <summary>
    /// Regardless of TransformHandleActive, force this tool to
    /// not render transform handle, e.g., during multi-selection.
    /// </summary>
    public bool ForceDisableTransformHandle { get; set; }

    /// <summary>
    /// Callback when a local frame tool successfully exits.
    /// </summary>
    public Action<Tool> OnToolDoneCallback = delegate { };

    /// <summary>
    /// When the position/rotation has been changed and this property is
    /// set the update method will call EditorUtility.SetDirty( OnChangeDirtyTarget )
    /// to force update of GUI related to this object.
    /// </summary>
    public UnityEngine.Object OnChangeDirtyTarget { get; set; }

    public UnityEngine.Object UndoRedoRecordObject { get; set; }

    /// <summary>
    /// Construct given frame, size in scene view and transparency alpha.
    /// </summary>
    /// <param name="frame">Target frame to manipulate.</param>
    /// <param name="size">Size of position/rotation handle in scene view.</param>
    /// <param name="alpha">Transparency alpha.</param>
    public FrameTool( IFrame frame, float size = 0.6f, float alpha = 1.0f )
      : base( isSingleInstanceTool: true )
    {
      Frame = frame;
      Size  = size;
      Alpha = alpha;
      TransformHandleActive = true;
      ForceDisableTransformHandle = false;
    }

    /// <summary>
    /// Removes any active children which has been activated
    /// by user interaction.
    /// </summary>
    public void InactivateTemporaryChildren()
    {
      foreach ( var child in GetChildren() )
        child.PerformRemoveFromParent();
    }

    public override void OnAdd()
    {
      DirtyTarget();
    }

    public override void OnRemove()
    {
      DirtyTarget();
    }

    public override void OnSceneViewGUI( SceneView sceneView )
    {
      if ( Frame == null )
        return;

      if ( GetParent() == null && Manager.KeyEscapeDown ) {
        PerformRemoveFromParent();
        return;
      }

      if ( !TransformHandleActive || ForceDisableTransformHandle )
        return;

      if ( UndoRedoRecordObject != null )
        Undo.RecordObject( UndoRedoRecordObject, "Frame Tool" );

      // Shows position handle if, e.g., scale or some other strange setting is used in the editor.
      bool isRotation = UnityEditor.Tools.current == UnityEditor.Tool.Rotate;

      // NOTE: Checking GUI changes before updating position/rotation to avoid
      //       drift in the values.
      bool changesMade = false;
      if ( !isRotation ) {
        var newPosition = PositionTool( Frame.Position, Frame.Rotation, Size, Alpha );
        changesMade = Vector3.SqrMagnitude( Frame.Position - newPosition ) > 1.0E-6f;
        if ( changesMade )
          Frame.Position = newPosition;
      }
      else {
        var newRotation = RotationTool( Frame.Position, Frame.Rotation, Size, Alpha );
        changesMade = ( Quaternion.Inverse( Frame.Rotation ) * newRotation ).eulerAngles.sqrMagnitude > 1.0E-6f;
        if ( changesMade )
          Frame.Rotation = newRotation;
      }

      if ( changesMade )
        DirtyTarget();
    }

    public override void OnPreTargetMembersGUI()
    {
      ToolsGUI( false );
    }

    public void ToolsGUI( bool isMultiSelect )
    {
      var skin = InspectorEditor.Skin;
      bool guiWasEnabled = UnityEngine.GUI.enabled;

      bool toggleSelectParent = false;
      bool toggleFindGivenPoint = false;
      bool toggleSelectEdge = false;
      bool togglePositionHandle = false;

      UnityEngine.GUI.enabled = !isMultiSelect;
      InspectorGUI.ToolButtons( InspectorGUI.ToolButtonData.Create( ToolIcon.SelectParent,
                                                                    SelectParent,
                                                                    "Select parent object by selecting object in scene view",
                                                                    () => toggleSelectParent = true,
                                                                    !isMultiSelect,
                                                                    () => UnityEngine.GUI.enabled = !isMultiSelect && guiWasEnabled ),
                                InspectorGUI.ToolButtonData.Create( ToolIcon.FindTransformGivenPoint,
                                                                    FindTransformGivenPointOnSurface,
                                                                    "Find position and rotation given point and direction on an objects surface",
                                                                    () => toggleFindGivenPoint = true,
                                                                    !isMultiSelect && guiWasEnabled ),
                                InspectorGUI.ToolButtonData.Create( ToolIcon.FindTransformGivenEdge,
                                                                    FindTransformGivenEdge,
                                                                    "Find position and rotation given a triangle or principal edge",
                                                                    () => toggleSelectEdge = true,
                                                                    !isMultiSelect && guiWasEnabled ),
                                InspectorGUI.ToolButtonData.Create( ToolIcon.TransformHandle,
                                                                    TransformHandleActive,
                                                                    "Position/rotation handle",
                                                                    () => togglePositionHandle = true,
                                                                    !isMultiSelect && guiWasEnabled ) );

      if ( toggleSelectParent )
        SelectParent = !SelectParent;
      if ( toggleFindGivenPoint )
        FindTransformGivenPointOnSurface = !FindTransformGivenPointOnSurface;
      if ( toggleSelectEdge )
        FindTransformGivenEdge = !FindTransformGivenEdge;
      if ( togglePositionHandle )
        TransformHandleActive = !TransformHandleActive;

      UnityEngine.GUI.enabled = guiWasEnabled;
    }

    /// <summary>
    /// Call this method when a tool spawned by this tool exits, successfully.
    /// </summary>
    /// <param name="localTool">Tool that is about to be removed with exit success.</param>
    private void OnLocalToolDone( Tool localTool )
    {
      OnToolDoneCallback( localTool );

      DirtyTarget();
    }

    /// <summary>
    /// If OnChangeDirtyTarget is set and changes are made from outside of
    /// the Inspector tab (e.g., scene view operations), this method will
    /// flag the object as dirty which will result in Inspector GUI update.
    /// </summary>
    private void DirtyTarget()
    {
      if ( OnChangeDirtyTarget != null )
        EditorUtility.SetDirty( OnChangeDirtyTarget );
    }
  }
}
