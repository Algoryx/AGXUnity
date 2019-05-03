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
    public Shape Shape { get; private set; }

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

    public ShapeTool( Shape shape )
      : base( shape )
    {
      Shape = shape;
    }

    public override void OnAdd()
    {
    }

    public override void OnRemove()
    {
    }

    public override void OnPreTargetMembersGUI( InspectorEditor editor )
    {
      if ( editor.IsMultiSelect ) {
        GUI.Separator();
        return;
      }

      var skin                     = InspectorEditor.Skin;
      bool toggleShapeResizeTool   = false;
      bool toggleShapeCreate       = false;
      bool toggleDisableCollisions = false;
      bool toggleShapeVisualCreate = true;

      GUILayout.BeginHorizontal();
      {
        GUI.ToolsLabel( skin );

        using ( GUI.ToolButtonData.ColorBlock ) {
          using ( new EditorGUI.DisabledGroupScope( !Tools.ShapeResizeTool.SupportsShape( Shape ) ) )
            toggleShapeResizeTool = GUI.ToolButton( GUI.Symbols.ShapeResizeTool,
                                                    ShapeResizeTool,
                                                    "Shape resize tool",
                                                    skin,
                                                    24 );

          toggleShapeCreate       = GUI.ToolButton( GUI.Symbols.ShapeCreateTool,
                                                    ShapeCreateTool,
                                                    "Create shape from visual objects",
                                                    skin );
          toggleDisableCollisions = GUI.ToolButton( GUI.Symbols.DisableCollisionsTool,
                                                    DisableCollisionsTool,
                                                    "Disable collisions against other objects",
                                                    skin );

          using ( new EditorGUI.DisabledGroupScope( !Tools.ShapeVisualCreateTool.CanCreateVisual( Shape ) ) )
            toggleShapeVisualCreate = GUI.ToolButton( GUI.Symbols.ShapeVisualCreateTool,
                                                      ShapeVisualCreateTool,
                                                      "Create visual representation of the physical shape",
                                                      skin,
                                                      14 );
        }
      }
      GUILayout.EndHorizontal();

      GUI.Separator();

      if ( ShapeCreateTool ) {
        GetChild<ShapeCreateTool>().OnInspectorGUI( editor );

        GUI.Separator();
      }
      if ( DisableCollisionsTool ) {
        GetChild<DisableCollisionsTool>().OnInspectorGUI( editor );

        GUI.Separator();
      }
      if ( ShapeVisualCreateTool ) {
        GetChild<ShapeVisualCreateTool>().OnInspectorGUI( editor );

        GUI.Separator();
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

    public override void OnPostTargetMembersGUI( InspectorEditor editor )
    {
      var shapeVisual = ShapeVisual.Find( Shape );
      if ( shapeVisual == null )
        return;

      var skin = InspectorEditor.Skin;

      GUI.Separator();
      if ( !GUI.FoldoutEx( EditorData.Instance.GetData( Shape,
                                                        "Visual",
                                                        entry => entry.Bool = true ),
                                                        skin.button,
                                                        GUI.MakeLabel( "Shape Visual", 12, true ),
                                                        new GUIStyle( skin.label ) { alignment = TextAnchor.UpperCenter } ) )
        return;

      GUI.Separator();

      GUILayout.Space( 6 );

      Undo.RecordObjects( shapeVisual.GetComponentsInChildren<MeshRenderer>(), "Shape visual material" );

      var materials = shapeVisual.GetMaterials();
      if ( materials.Length > 1 ) {
        var distinctMaterials = materials.Distinct().ToArray();
        using ( GUI.AlignBlock.Center ) {
          GUILayout.Label( GUI.MakeLabel( "Displays material if all materials are the same <b>(otherwise None)</b> and/or assign new material to all objects in this shape." ),
                                          new GUIStyle( skin.textArea ) { alignment = TextAnchor.MiddleCenter }, GUILayout.Width( Screen.width - 60 ) );
        }
        GUI.MaterialEditor( GUI.MakeLabel( "Common material:", true ),
                            128,
                            distinctMaterials.Length == 1 ? distinctMaterials.First() : null,
                            skin,
                            newMaterial => shapeVisual.SetMaterial( newMaterial ) );

        GUILayout.Space( 6 );

        GUI.Separator();

        using ( GUI.AlignBlock.Center )
          GUILayout.Label( GUI.MakeLabel( "Material list", true ), skin.label );

        GUI.Separator();
      }

      for ( int i = 0; i < materials.Length; ++i ) {
        var material = materials[ i ];
        var showMaterialEditor = materials.Length == 1 ||
                                 GUI.Foldout( EditorData.Instance.GetData( Shape,
                                                                           "VisualMaterial" + i ),
                                              GUI.MakeLabel( material.name ), skin );
        if ( showMaterialEditor )
          GUI.MaterialEditor( GUI.MakeLabel( "Material:", true ),
                              64,
                              material,
                              skin,
                              newMaterial => shapeVisual.ReplaceMaterial( i, newMaterial ) );
        GUI.Separator();
      }
    }
  }
}
