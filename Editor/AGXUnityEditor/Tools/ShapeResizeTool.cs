using System.Linq;
using UnityEngine;
using UnityEditor;
using AGXUnity.Collide;
using AGXUnity.Utils;

namespace AGXUnityEditor.Tools
{
  /// <summary>
  /// Tool to resize supported primitive shape types. Supported types
  /// (normally Box, Capsule, Cylinder and Sphere) has ShapeUtils set.
  /// 
  /// This tool is active when ActiveKey is down and a supported shape
  /// is selected. For symmetric resize, both SymmetricScaleKey and
  /// ActiveKey has to be down.
  /// </summary>
  public class ShapeResizeTool : Tool
  {
    /// <summary>
    /// Checks if the given shape supports resize.
    /// </summary>
    /// <param name="shape">Shape to check if it supports this tool.</param>
    /// <returns>True if the given shape support this tool.</returns>
    public static bool SupportsShape( Shape shape )
    {
      return shape != null && shape.GetUtils() != null;
    }

    /// <summary>
    /// Key code for symmetric scale/resize.
    /// </summary>
    public Utils.KeyHandler SymmetricScaleKey { get { return GetKeyHandler( "Symmetric" ); } }

    /// <summary>
    /// True for this tool to remove itself when key "Esc" is pressed.
    /// </summary>
    public bool RemoveOnKeyEscape = false;

    /// <summary>
    /// Shape this tool handles.
    /// </summary>
    public Shape Shape { get; private set; }

    public ShapeResizeTool( Shape shape )
      : base( isSingleInstanceTool: true )
    {
      AddKeyHandler( "Symmetric", new Utils.KeyHandler( KeyCode.LeftShift ) );

      Shape = shape;
    }

    public override void OnAdd()
    {
      HideDefaultHandlesEnableWhenRemoved();
      SizeUpdated = false;
    }

    public override void OnRemove()
    {
      if ( SizeUpdated )
        OnSizeUpdatedUpdateMassProperties();
    }

    public override void OnSceneViewGUI( SceneView sceneView )
    {
      if ( RemoveOnKeyEscape && Manager.KeyEscapeDown ) {
        // Avoiding delay of this tool being active in the Inspector.
        // It's not enough to dirty our Shape because this tool could
        // be activated in an recursive editor.
        if ( Selection.activeGameObject != null )
          EditorUtility.SetDirty( Selection.activeGameObject );

        PerformRemoveFromParent();

        return;
      }

      Update( SymmetricScaleKey.IsDown );
    }

    private void Update( bool symmetricScale )
    {
      if ( Shape == null )
        return;

      var utils = Shape.GetUtils();
      if ( utils == null )
        return;

      if ( SizeUpdated && EditorApplication.timeSinceStartup - LastChangeTime > 0.333 )
        OnSizeUpdatedUpdateMassProperties();

      Undo.RecordObject( Shape, "ShapeResizeTool" );
      Undo.RecordObject( Shape.transform, "ShapeResizeToolTransform" );

      var color = Color.gray;
      var scale = 0.35f;
      foreach ( ShapeUtils.Direction dir in System.Enum.GetValues( typeof( ShapeUtils.Direction ) ) ) {
        var delta = DeltaSliderTool( utils.GetWorldFace( dir ),
                                     utils.GetWorldFaceDirection( dir ),
                                     color,
                                     scale );
        if ( delta.magnitude > 1.0E-5f ) {
          var localSizeChange = Shape.transform.InverseTransformDirection( delta );
          var isHalfSizeDirection = utils.IsHalfSize( dir );
          if ( !symmetricScale && isHalfSizeDirection )
            localSizeChange *= 0.5f;

          utils.UpdateSize( ref localSizeChange, dir );

          if ( !symmetricScale && localSizeChange.magnitude > 1.0E-5f ) {
            var localPositionDelta = isHalfSizeDirection ?
                                       localSizeChange :
                                       0.5f * localSizeChange;
            Shape.transform.position += Shape.transform.TransformDirection( localPositionDelta );
          }

          SizeUpdated = true;
          LastChangeTime = EditorApplication.timeSinceStartup;
        }
      }
    }

    private void OnSizeUpdatedUpdateMassProperties()
    {
      var rb = Shape.RigidBody;
      if ( rb != null ) {
        rb.UpdateMassProperties();
        EditorUtility.SetDirty( rb );
      }
      SizeUpdated = false;
    }

    private bool SizeUpdated { get; set; } = false;
    private double LastChangeTime { get; set; } = 0.0;
  }
}
