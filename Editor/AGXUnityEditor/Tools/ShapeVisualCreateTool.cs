using System;
using UnityEngine;
using UnityEditor;
using AGXUnity.Collide;
using AGXUnity.Rendering;
using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  public class ShapeVisualCreateTool : Tool
  {
    public static bool CanCreateVisual( Shape shape )
    {
      return shape != null &&
             ShapeVisual.SupportsShapeVisual( shape ) &&
            !ShapeVisual.HasShapeVisual( shape ) &&
             ( !( shape is AGXUnity.Collide.Mesh ) ||
                ( shape as AGXUnity.Collide.Mesh ).SourceObjects.Length > 0 );
    }

    public Shape Shape { get; private set; }

    public Material Material
    {
      get { return m_material; }
      set
      {
        if ( m_material != null && value == m_material )
          return;

        m_material = value ?? Manager.GetOrCreateShapeVisualDefaultMaterial();

        GetData( DataEntry.Material ).Asset = m_material;

        if ( Preview != null )
          Preview.GetComponent<ShapeVisual>().SetMaterial( m_material );
      }
    }

    public string Name
    {
      get { return m_name; }
      set
      {
        if ( value == m_name )
          return;

        m_name = value;

        GetData( DataEntry.Name ).String = m_name;
      }
    }

    public GameObject Preview { get; private set; }

    public ShapeVisualCreateTool( Shape shape, bool isGloballyUnique = true )
      : base( isSingleInstanceTool: isGloballyUnique )
    {
      Shape = shape;
      Preview = ShapeVisual.Create( shape );
      if ( Preview != null ) {
        Preview.transform.parent   = Manager.VisualsParent.transform;
        Preview.transform.position = Shape.transform.position;
        Preview.transform.rotation = Shape.transform.rotation;
      }

      Material = GetData( DataEntry.Material ).Asset as Material ?? Manager.GetOrCreateShapeVisualDefaultMaterial();
      Name     = GetData( DataEntry.Name ).String;
      if ( Name == "" )
        Name = Shape.name + "_Visual";
    }

    public override void OnRemove()
    {
      if ( Preview != null )
        GameObject.DestroyImmediate( Preview );
    }

    public override void OnSceneViewGUI( SceneView sceneView )
    {
      if ( Shape == null || Preview == null ) {
        PerformRemoveFromParent();
        return;
      }

      Preview.transform.position = Shape.transform.position;
      Preview.transform.rotation = Shape.transform.rotation;
      if ( Shape is AGXUnity.Collide.Mesh )
        Preview.transform.localScale = Shape.transform.lossyScale;
    }

    public void OnInspectorGUI( bool onlyNameAndMaterial = false )
    {
      if ( !onlyNameAndMaterial )
        InspectorGUI.OnDropdownToolBegin( "Create visual representation of this shape." );

      Name = EditorGUILayout.TextField( GUI.MakeLabel( "Name" ),
                                        Name,
                                        InspectorEditor.Skin.TextField );

      InspectorGUI.UnityMaterial( GUI.MakeLabel( "Material" ),
                                  Material,
                                  newMaterial => Material = newMaterial );

      if ( !onlyNameAndMaterial ) {
        var createCancelState = InspectorGUI.PositiveNegativeButtons( Preview != null,
                                                                      "Create",
                                                                      "Create new shape visual.",
                                                                      "Cancel" );

        InspectorGUI.OnDropdownToolEnd();

        if ( createCancelState == InspectorGUI.PositiveNegativeResult.Positive ) {
          CreateShapeVisual();
        }
        if ( createCancelState != InspectorGUI.PositiveNegativeResult.Neutral ) {
          PerformRemoveFromParent();
        }
      }
    }

    public void CreateShapeVisual()
    {
      if ( Preview != null )
        GameObject.DestroyImmediate( Preview );

      var go = ShapeVisual.Create( Shape );
      if ( go == null )
        return;

      var shapeVisual = go.GetComponent<ShapeVisual>();
      shapeVisual.name = Name;
      shapeVisual.SetMaterial( Material );

      Undo.RegisterCreatedObjectUndo( go, "Shape visual for shape: " + Shape.name );
    }

    private enum DataEntry
    {
      Material,
      Name
    }

    private EditorDataEntry GetData( DataEntry entry )
    {
      return EditorData.Instance.GetData( Shape, "ShapeVisualCreateTool_" + entry.ToString() );
    }

    private Material m_material = null;
    private string m_name = "";
  }
}
