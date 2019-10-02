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
    /// Key code to activate this tool.
    /// </summary>
    public Utils.KeyHandler ActivateKey { get { return GetKeyHandler( "Activate" ); } }

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
      AddKeyHandler( "Activate", new Utils.KeyHandler( KeyCode.LeftControl ) );
      AddKeyHandler( "Symmetric", new Utils.KeyHandler( KeyCode.LeftControl, KeyCode.LeftShift ) );

      Shape = shape;
    }

    public override void OnSceneViewGUI( SceneView sceneView )
    {
      if ( RemoveOnKeyEscape && Manager.KeyEscapeDown ) {
        EditorUtility.SetDirty( Shape );

        PerformRemoveFromParent();

        return;
      }

      if ( ActivateKey.IsDown || SymmetricScaleKey.IsDown )
        Update( SymmetricScaleKey.IsDown );
    }

    private void Update( bool symmetricScale )
    {
      if ( Shape == null )
        return;

      ShapeUtils utils = Shape.GetUtils();
      if ( utils == null )
        return;

      Undo.RecordObject( Shape, "ShapeResizeTool" );
      Undo.RecordObject( Shape.transform, "ShapeResizeToolTransform" );

      Color color = Color.gray;
      float scale = 0.35f;
      foreach ( ShapeUtils.Direction dir in System.Enum.GetValues( typeof( ShapeUtils.Direction ) ) ) {
        Vector3 delta = DeltaSliderTool( utils.GetWorldFace( dir ), utils.GetWorldFaceDirection( dir ), color, scale );
        if ( delta.magnitude > 1.0E-5f ) {
          Vector3 localSizeChange = Shape.transform.InverseTransformDirection( delta );
          Vector3 localPositionDelta = 0.5f * localSizeChange;
          if ( !symmetricScale && utils.IsHalfSize( dir ) )
            localSizeChange *= 0.5f;

          utils.UpdateSize( localSizeChange, dir );

          if ( !symmetricScale )
            Shape.transform.position += Shape.transform.TransformDirection( localPositionDelta );
        }
      }
    }
  }
}
