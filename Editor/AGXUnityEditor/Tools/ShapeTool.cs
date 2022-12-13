using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using AGXUnity.Collide;
using AGXUnity.Rendering;

using GUI = AGXUnity.Utils.GUI;
using Object = UnityEngine.Object;

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

          var shapeResizeTool               = new ShapeResizeTool( Shape );
          shapeResizeTool.RemoveOnKeyEscape = true;

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

    public bool CreateOrientedShapeTool
    {
      get { return GetChild<CreateOrientedShapeTool>() != null; }
      set
      {
        if ( value && !CreateOrientedShapeTool ) {
          RemoveAllChildren();

          var shapeCreateTool = new CreateOrientedShapeTool( Shape.gameObject );
          AddChild( shapeCreateTool );
        }
        else if ( !value )
          RemoveChild( GetChild<CreateOrientedShapeTool>() );
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
      if ( IsMultiSelect )
        return;

      var skin                     = InspectorEditor.Skin;
      bool toggleShapeResizeTool   = false;
      bool toggleShapeCreate       = false;
      bool toggleDisableCollisions = false;
      bool toggleShapeVisualCreate = false;

      InspectorGUI.ToolButtons( InspectorGUI.ToolButtonData.Create( ToolIcon.ShapeResize,
                                                                    ShapeResizeTool,
                                                                    "Shape resize tool",
                                                                    () => toggleShapeResizeTool = true,
                                                                    Tools.ShapeResizeTool.SupportsShape( Shape ) ),
                                InspectorGUI.ToolButtonData.Create( ToolIcon.CreateShapeGivenVisual,
                                                                    CreateOrientedShapeTool,
                                                                    "Create oriented shape from visual objects",
                                                                    () => toggleShapeCreate = true ),
                                InspectorGUI.ToolButtonData.Create( ToolIcon.DisableCollisions,
                                                                    DisableCollisionsTool,
                                                                    "Disable collisions against other objects",
                                                                    () => toggleDisableCollisions = true ),
                                InspectorGUI.ToolButtonData.Create( ToolIcon.CreateVisual,
                                                                    ShapeVisualCreateTool,
                                                                    "Create visual representation of the physical shape",
                                                                    () => toggleShapeVisualCreate = true,
                                                                    Tools.ShapeVisualCreateTool.CanCreateVisual( Shape ) ) );

      if ( CreateOrientedShapeTool ) {
        GetChild<CreateOrientedShapeTool>().OnInspectorGUI();
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
        CreateOrientedShapeTool = !CreateOrientedShapeTool;
      if ( toggleDisableCollisions )
        DisableCollisionsTool = !DisableCollisionsTool;
      if ( toggleShapeVisualCreate )
        ShapeVisualCreateTool = !ShapeVisualCreateTool;
    }

    public override void OnPostTargetMembersGUI()
    {
      if ( IsMultiSelect )
        return;

      var shapeVisual = ShapeVisual.Find( Shape );
      if ( shapeVisual == null )
        return;

      var materials = shapeVisual.GetMaterials();
      if ( materials.Length > 1 ) {
        var names = ( from renderer in shapeVisual.GetComponentsInChildren<MeshRenderer>()
                      from material in renderer.sharedMaterials
                      select renderer.name ).ToArray();

        var distinctMaterials = materials.Distinct().ToArray();
        var isExtended = false;
        if ( distinctMaterials.Length == 1 )
          isExtended = ShapeVisualMaterialGUI( "Common Render Material",
                                               distinctMaterials[ 0 ],
                                               newMaterial => shapeVisual.SetMaterial( newMaterial ) );
        else
          isExtended = InspectorGUI.Foldout( EditorData.Instance.GetData( Shape, "Render Materials" ),
                                             GUI.MakeLabel( "Render Materials" ) );

        if ( isExtended )
          using ( InspectorGUI.IndentScope.Single )
            for ( int i = 0; i < materials.Length; ++i ) {
              ShapeVisualMaterialGUI( names[ i ],
                                      materials[ i ],
                                      newMaterial => shapeVisual.ReplaceMaterial( i, newMaterial ) );
            }
      }
      else if ( materials.Length == 1 ) {
        ShapeVisualMaterialGUI( "Render Material",
                                materials[ 0 ],
                                newMaterial => shapeVisual.ReplaceMaterial( 0, newMaterial ) );
      }
    }

    private bool ShapeVisualMaterialGUI( string name, Material material, Action<Material> onNewMaterial )
    {
      var editorData = EditorData.Instance.GetData( Shape, "Visual_" + name );
      var result = InspectorGUI.FoldoutObjectField( GUI.MakeLabel( name ),
                                                    material,
                                                    typeof( Material ),
                                                    editorData,
                                                    false ) as Material;
      if ( result != material )
        onNewMaterial?.Invoke( result );

      return editorData.Bool;
    }
  }
}
