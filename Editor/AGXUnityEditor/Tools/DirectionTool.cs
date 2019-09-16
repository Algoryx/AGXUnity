using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AGXUnity.Utils;

namespace AGXUnityEditor.Tools
{
  public class DirectionTool : Tool
  {
    public Vector3 Position { get; set; }

    public Vector3 MainDirection { get; set; }

    public Vector3 UpVector { get; set; }

    public Action<Vector3, Quaternion> OnSelect = delegate { };

    public DirectionTool( Vector3 position, Vector3 mainDirection, Vector3 upVector )
      : base( isSingleInstanceTool: true )
    {
      Position      = position;
      MainDirection = mainDirection;
      UpVector      = upVector;

      foreach ( var visual in Visuals ) {
        visual.Color          = new Color( 0.9f, 0.9f, 0.9f, 0.15f );
        visual.MouseOverColor = new Color( 0.9f, 0.9f, 0.9f, 0.65f );
        visual.OnMouseClick  += OnPrimitiveClick;
      }
    }

    public Utils.VisualPrimitiveArrow GetVisual( ShapeUtils.Direction direction )
    {
      return GetOrCreateVisualPrimitive<Utils.VisualPrimitiveArrow>( direction.ToString(), "GUI/Text Shader" );
    }

    public IEnumerable<Utils.VisualPrimitiveArrow> Visuals
    {
      get
      {
        foreach ( var e in Enum.GetValues( typeof( ShapeUtils.Direction ) ) )
          yield return GetVisual( (ShapeUtils.Direction)e );
      }
    }

    public ShapeUtils.Direction GetDirection( Utils.VisualPrimitive primitive )
    {
      foreach ( ShapeUtils.Direction direction in Enum.GetValues( typeof( ShapeUtils.Direction ) ) )
        if ( GetVisual( direction ) == primitive )
          return direction;

      throw new AGXUnity.Exception( "Direction not found." );
    }

    public override void OnSceneViewGUI( SceneView sceneView )
    {
      foreach ( ShapeUtils.Direction direction in Enum.GetValues( typeof( ShapeUtils.Direction ) ) ) {
        var visual          = ConfigureVisual( direction );
        Quaternion rotation = GetRotation( Vector3.up, direction );
        visual.SetTransform( Position, rotation, 0.5f * Vector3.one );
      }
    }

    private void OnPrimitiveClick( Utils.Raycast.Result result, Utils.VisualPrimitive primitive )
    {
      OnSelect( Position, GetRotation( Vector3.forward, GetDirection( primitive ) ) );

      PerformRemoveFromParent();
    }

    private Quaternion GetRotation( Vector3 axis, ShapeUtils.Direction direction )
    {
      return Quaternion.LookRotation( MainDirection, UpVector ) * Quaternion.FromToRotation( axis, ShapeUtils.GetLocalFaceDirection( direction ) );
    }

    private Utils.VisualPrimitiveArrow ConfigureVisual( ShapeUtils.Direction direction )
    {
      const float edgeDirectionAlpha    = 0.25f;
      const float nonEdgeDirectionAlpha = 0.025f;
      const float mouseOverAlpha        = 0.65f;

      var visual            = GetVisual( direction );
      visual.Visible        = true;
      visual.Color          = direction == ShapeUtils.Direction.Negative_Z || direction == ShapeUtils.Direction.Positive_Z ?
                                new Color( 0.96f, 0.8f, 0.8f, edgeDirectionAlpha ) :
                                new Color( 0.8f, 0.8f, 0.96f, nonEdgeDirectionAlpha );
      visual.MouseOverColor = new Color( 0.9f, 0.9f, 0.9f, mouseOverAlpha );

      return visual;
    }
  }
}
