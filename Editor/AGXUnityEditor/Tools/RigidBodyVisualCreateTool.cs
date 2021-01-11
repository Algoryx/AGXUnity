using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Collide;
using AGXUnity.Rendering;
using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  public class RigidBodyVisualCreateTool : Tool
  {
    public static bool ValidForNewShapeVisuals( RigidBody rb )
    {
      if ( rb == null )
        return false;

      int numSupportedNewShapeVisual = 0;
      foreach ( var shape in rb.Shapes )
        numSupportedNewShapeVisual += System.Convert.ToInt32( ShapeVisualCreateTool.CanCreateVisual( shape ) );

      return numSupportedNewShapeVisual > 0;
    }

    public RigidBody RigidBody { get; private set; }

    public RigidBodyVisualCreateTool( RigidBody rb )
      : base( isSingleInstanceTool: true )
    {
      RigidBody = rb;
    }

    public override void OnAdd()
    {
      foreach ( var shape in RigidBody.Shapes )
        AddChild( new ShapeVisualCreateTool( shape, false ) );
    }

    public override void OnRemove()
    {
    }

    public void OnInspectorGUI()
    {
      if ( RigidBody == null || GetChildren().Length == 0 ) {
        PerformRemoveFromParent();
        return;
      }

      var skin = InspectorEditor.Skin;

      InspectorGUI.OnDropdownToolBegin( "Create visual representation of this rigid body given all supported shapes." );

      foreach ( var tool in GetChildren<ShapeVisualCreateTool>() ) {
        if ( ShapeVisual.HasShapeVisual( tool.Shape ) )
          continue;

        EditorGUILayout.PrefixLabel( GUI.MakeLabel( tool.Shape.name,
                                                    true ),
                                     skin.Label );
        using ( InspectorGUI.IndentScope.Single )
          tool.OnInspectorGUI( true );
      }

      var createCancelState = InspectorGUI.PositiveNegativeButtons( true,
                                                                    "Create",
                                                                    "Create shape visual for shapes that hasn't already got one.",
                                                                    "Cancel" );
      if ( createCancelState == InspectorGUI.PositiveNegativeResult.Positive ) {
        foreach ( var tool in GetChildren<ShapeVisualCreateTool>() )
          if ( !ShapeVisual.HasShapeVisual( tool.Shape ) )
            tool.CreateShapeVisual();
      }

      InspectorGUI.OnDropdownToolEnd();

      if ( createCancelState != InspectorGUI.PositiveNegativeResult.Neutral )
        PerformRemoveFromParent();
    }
  }
}
