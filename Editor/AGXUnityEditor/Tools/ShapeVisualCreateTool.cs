using System;
using UnityEngine;
using UnityEditor;
using AGXUnity.Collide;
using AGXUnity.Rendering;
using GUI = AGXUnityEditor.Utils.GUI;

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
      : base( isSingleInstanceTool: true )
    {
      Shape = shape;
      Preview = ShapeVisual.Create( shape );
      if ( Preview != null ) {
        Preview.transform.parent   = Manager.VisualsParent.transform;
        Preview.transform.position = Shape.transform.position;
        Preview.transform.rotation = Shape.transform.rotation;
      }

      Material = GetData( DataEntry.Material ).Asset as Material;
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
    }

    public void OnInspectorGUI( bool onlyNameAndMaterial = false )
    {
      var skin = InspectorEditor.Skin;

      if ( !onlyNameAndMaterial ) {
        GUILayout.Space( 4 );
        using ( GUI.AlignBlock.Center )
          GUILayout.Label( GUI.MakeLabel( "Create visual tool", 16, true ), skin.Label );

        GUILayout.Space( 2 );
        GUI.Separator();
        GUILayout.Space( 4 );
      }

      GUILayout.BeginHorizontal();
      {
        GUILayout.Label( GUI.MakeLabel( "Name:", true ), skin.Label, GUILayout.Width( 64 ) );
        Name = GUILayout.TextField( Name, skin.TextField, GUILayout.ExpandWidth( true ) );
      }
      GUILayout.EndHorizontal();

      InspectorGUI.UnityMaterial( GUI.MakeLabel( "Material:", true ),
                                  Material,
                                  newMaterial => Material = newMaterial );

      GUI.Separator();

      if ( !onlyNameAndMaterial ) {
        var createCancelState = GUI.CreateCancelButtons( Preview != null, "Create new shape visual" );
        if ( createCancelState == GUI.CreateCancelState.Create ) {
          CreateShapeVisual();
        }
        if ( createCancelState != GUI.CreateCancelState.Nothing ) {
          PerformRemoveFromParent();
          return;
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
