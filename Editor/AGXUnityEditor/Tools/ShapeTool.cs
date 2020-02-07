using System.Linq;
using UnityEngine;
using UnityEditor;
using AGXUnity.Collide;
using AGXUnity.Rendering;
using GUI = AGXUnityEditor.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( Shape ) )]
  public class ShapeTool : CustomTargetTool
  {
    public Shape Shape
    {
      get
      {
        return Targets[ 0 ] as Shape;
      }
    }

    public bool ShapeResizeTool
    {
      get { return GetChild<ShapeResizeTool>() != null; }
      set
      {
        if ( value && !ShapeResizeTool ) {
          RemoveAllChildren();

          var shapeResizeTool                                            = new ShapeResizeTool( Shape );
          shapeResizeTool.ActivateKey.HideDefaultHandlesWhenIsDown       = true;
          shapeResizeTool.SymmetricScaleKey.HideDefaultHandlesWhenIsDown = true;
          shapeResizeTool.RemoveOnKeyEscape                              = true;

          AddChild( shapeResizeTool );

          Manager.RequestSceneViewFocus();
        }
        else if ( !value )
          RemoveChild( GetChild<ShapeResizeTool>() );
      }
    }

    public bool DisableCollisionsTool
    {
      get { return GetChild<DisableCollisionsTool>() != null; }
      set
      {
        if ( value && !DisableCollisionsTool ) {
          RemoveAllChildren();

          var disableCollisionsTool = new DisableCollisionsTool( Shape.gameObject );
          AddChild( disableCollisionsTool );
        }
        else if ( !value )
          RemoveChild( GetChild<DisableCollisionsTool>() );
      }
    }

    public bool ShapeCreateTool
    {
      get { return GetChild<ShapeCreateTool>() != null; }
      set
      {
        if ( value && !ShapeCreateTool ) {
          RemoveAllChildren();

          var shapeCreateTool = new ShapeCreateTool( Shape.gameObject );
          AddChild( shapeCreateTool );
        }
        else if ( !value )
          RemoveChild( GetChild<ShapeCreateTool>() );
      }
    }

    public bool ShapeVisualCreateTool
    {
      get { return GetChild<ShapeVisualCreateTool>() != null; }
      set
      {
        if ( value && !ShapeVisualCreateTool ) {
          RemoveAllChildren();

          var createShapeVisualTool = new ShapeVisualCreateTool( Shape );
          AddChild( createShapeVisualTool );
        }
        else if ( !value )
          RemoveChild( GetChild<ShapeVisualCreateTool>() );
      }
    }

    public ShapeTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnAdd()
    {
    }

    public override void OnRemove()
    {
    }

    public override void OnPreTargetMembersGUI()
    {
      if ( IsMultiSelect ) {
        return;
      }

      var skin                     = InspectorEditor.Skin;
      bool toggleShapeResizeTool   = false;
      bool toggleShapeCreate       = false;
      bool toggleDisableCollisions = false;
      bool toggleShapeVisualCreate = false;

      InspectorGUI.ToolButtons( InspectorGUI.ToolButtonData.Create( "agx_unity_shape resize 1",
                                                                    ShapeResizeTool,
                                                                    "Shape resize tool",
                                                                    () => toggleShapeResizeTool = true,
                                                                    Tools.ShapeResizeTool.SupportsShape( Shape ) ),
                                InspectorGUI.ToolButtonData.Create( "agx_unity_represent shape 1",
                                                                    ShapeCreateTool,
                                                                    "Create shape from visual objects",
                                                                    () => toggleShapeCreate = true ),
                                InspectorGUI.ToolButtonData.Create( "agx_unity_disable collision 1",
                                                                    DisableCollisionsTool,
                                                                    "Disable collisions against other objects",
                                                                    () => toggleDisableCollisions = true ),
                                InspectorGUI.ToolButtonData.Create( "agx_unity_represent shape 3",
                                                                    ShapeVisualCreateTool,
                                                                    "Create visual representation of the physical shape",
                                                                    () => toggleShapeVisualCreate = true,
                                                                    Tools.ShapeVisualCreateTool.CanCreateVisual( Shape ) ) );

      if ( ShapeCreateTool ) {
        GetChild<ShapeCreateTool>().OnInspectorGUI();
      }
      if ( DisableCollisionsTool ) {
        GetChild<DisableCollisionsTool>().OnInspectorGUI();
      }
      if ( ShapeVisualCreateTool ) {
        GetChild<ShapeVisualCreateTool>().OnInspectorGUI();
      }

      if ( toggleShapeResizeTool )
        ShapeResizeTool = !ShapeResizeTool;
      if ( toggleShapeCreate )
        ShapeCreateTool = !ShapeCreateTool;
      if ( toggleDisableCollisions )
        DisableCollisionsTool = !DisableCollisionsTool;
      if ( toggleShapeVisualCreate )
        ShapeVisualCreateTool = !ShapeVisualCreateTool;
    }

    public override void OnPostTargetMembersGUI()
    {
      var shapeVisual = ShapeVisual.Find( Shape );
      if ( shapeVisual == null )
        return;

      var skin = InspectorEditor.Skin;

      if ( !InspectorGUI.Foldout( EditorData.Instance.GetData( Shape,
                                                               "Visual",
                                                               entry => entry.Bool = true ),
                                                               GUI.MakeLabel( "Shape Visual" ) ) )
        return;

      using ( InspectorGUI.IndentScope.Single ) {
        GUILayout.Space( 6 );

        Undo.RecordObjects( shapeVisual.GetComponentsInChildren<MeshRenderer>(), "Shape visual material" );

        var materials = shapeVisual.GetMaterials();
        if ( materials.Length > 1 ) {
          var distinctMaterials = materials.Distinct().ToArray();
          using ( GUI.AlignBlock.Center ) {
            GUILayout.Label( GUI.MakeLabel( "Displays material if all materials are the same <b>(otherwise None)</b> and/or assign new material to all objects in this shape." ),
                             skin.TextAreaMiddleCenter,
                             GUILayout.Width( Screen.width - 60 ) );
          }
          InspectorGUI.UnityMaterial( GUI.MakeLabel( "Common material:", true ),
                                      distinctMaterials.Length == 1 ? distinctMaterials.First() : null,
                                      newMaterial => shapeVisual.SetMaterial( newMaterial ) );

          GUILayout.Space( 6 );

          using ( GUI.AlignBlock.Center )
            GUILayout.Label( GUI.MakeLabel( "Material list", true ), skin.Label );
        }

        for ( int i = 0; i < materials.Length; ++i ) {
          var material = materials[ i ];
          var showMaterialEditor = materials.Length == 1 ||
                                   InspectorGUI.Foldout( EditorData.Instance.GetData( Shape,
                                                                                      "VisualMaterial" + i ),
                                                         GUI.MakeLabel( material.name ) );
          if ( showMaterialEditor )
            InspectorGUI.UnityMaterial( GUI.MakeLabel( "Material:" ),
                                        material,
                                        newMaterial => shapeVisual.ReplaceMaterial( i, newMaterial ) );
        }
      }
    }
  }
}
