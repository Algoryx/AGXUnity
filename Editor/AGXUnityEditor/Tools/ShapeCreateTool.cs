using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Collide;
using AGXUnity.Utils;
using Mesh = AGXUnity.Collide.Mesh;

namespace AGXUnityEditor.Tools
{
  public class ShapeCreateTool : Tool
  {
    public enum ShapeType
    {
      Box,
      Cylinder,
      Capsule,
      Sphere,
      Mesh,
      HollowCylinder,
      Cone,
      HollowCone
    }

    public static GameObject CreateShape<T>( ShapeInitializationData data, bool shapeAsParent, Action<T> initializeAction ) where T : Shape
    {
      if ( initializeAction == null ) {
        Debug.LogError( "Unable to create shape without an initializeAction." );
        return null;
      }

      if ( data == null )
        return null;

      GameObject shapeGameObject = Factory.Create<T>();

      Undo.RegisterCreatedObjectUndo( shapeGameObject, "New game object with shape component" );
      if ( AGXUnity.Rendering.DebugRenderManager.HasInstance )
        Undo.AddComponent<AGXUnity.Rendering.ShapeDebugRenderData>( shapeGameObject );

      initializeAction( shapeGameObject.GetComponent<T>() );

      if ( shapeAsParent ) {
        // New parent to shape is filter current parent.
        Undo.SetTransformParent( shapeGameObject.transform, data.Filter.transform.parent, "Visual parent as parent to shape" );
        Undo.SetTransformParent( data.Filter.transform, shapeGameObject.transform, "Shape as parent to visual" );
      }
      else
        Undo.SetTransformParent( shapeGameObject.transform, data.Filter.transform, "Shape as child to visual" );

      // SetTransformParent assigns some scale given the parent. We're in general not
      // interested in this scale since it will "un-scale" meshes (and the rest of the
      // shapes doesn't support scale so...).

      // If mesh and the mesh should be parent to the filter we have to move the
      // localScale to the shape game object.
      if ( shapeAsParent && typeof( T ) == typeof( Mesh ) ) {
        shapeGameObject.transform.localScale = data.Filter.transform.localScale;
        data.Filter.transform.localScale = Vector3.one;
      }
      else
        shapeGameObject.transform.localScale = Vector3.one;

      return shapeGameObject;
    }

    private Utils.ShapeCreateButtons m_buttons = new Utils.ShapeCreateButtons();
    private List<GameObject> m_selection       = new List<GameObject>();
    private const string m_visualPrimitiveName = "createShapeVisualPrimitive";

    public GameObject Parent { get; private set; }
    public Color VisualPrimitiveColor { get; set; }
    public string VisualPrimitiveShader { get; set; }

    public ShapeCreateTool( GameObject parent )
      : base( isSingleInstanceTool: true )
    {
      Parent = parent;
      VisualPrimitiveColor = Color.red;
      VisualPrimitiveShader = "Standard";
    }

    public override void OnAdd()
    {
    }

    public override void OnRemove()
    {
      Reset();
    }

    public override void OnSceneViewGUI( SceneView sceneView )
    {
      if ( Parent == null ) {
        PerformRemoveFromParent();
        return;
      }

      if ( HandleKeyEscape( true ) )
        return;

      // NOTE: Waiting for mouse click!
      if ( !Manager.HijackLeftMouseClick() )
        return;

      var hitResults = Utils.Raycast.IntersectChildren( HandleUtility.GUIPointToWorldRay( Event.current.mousePosition ),
                                                        Parent,
                                                        null,
                                                        true );
      // Find target. Ignoring shapes.
      GameObject selected = null;
      for ( int i = 0; selected == null && i < hitResults.Length; ++i ) {
        if ( hitResults[ i ].Target.GetComponent<Shape>() == null )
          selected = hitResults[ i ].Target;
      }

      // Single selection mode.
      ClearSelection();
      if ( selected != null ) {
        m_selection.Add( selected );
        // TODO HIGHLIGHT: Add multiple.
        //SetVisualizedSelection( selected );
      }
      else
        m_buttons.Reset();

      // TODO GUI: Why? Force inspector update instead?
      EditorUtility.SetDirty( Parent );
    }

    public void OnInspectorGUI()
    {
      if ( HandleKeyEscape( false ) )
        return;

      var skin = InspectorEditor.Skin;

      InspectorGUI.OnDropdownToolBegin( GetCurrentStateInfo() );

      UnityEngine.GUI.enabled = m_selection.Count > 0;
      m_buttons.OnGUI( Event.current );
      UnityEngine.GUI.enabled = true;

      InspectorGUI.OnDropdownToolEnd();

      EditorUtility.SetDirty( Parent );

      // Call this before we exit since it'll remove the visual primitive
      // if no shape button currently is selected.
      var vp = GetSelectedButtonVisualPrimitive();
      if ( m_buttons.Selected == null )
        return;

      if ( m_selection.Count == 0 || m_buttons.Selected.State.Axis == ShapeInitializationData.Axes.None )
        return;

      var shapesInitData = ShapeInitializationData.Create( m_selection.ToArray() );
      if ( shapesInitData.Length == 0 )
        return;

      var shapeInitData = shapesInitData[ 0 ];
      var axisData = shapeInitData.FindAxisData( m_buttons.Selected.State.Axis, m_buttons.Selected.State.ExpandRadius );

      UpdateVisualPrimitive( vp, shapeInitData, axisData );

      if ( m_buttons.Selected.State.CreatePressed ) {
        if ( m_buttons.Selected.State.ShapeType == ShapeType.Box ) {
          CreateShape<Box>( shapeInitData, m_buttons.Selected.State.ShapeAsParent, box =>
          {
            box.HalfExtents = shapeInitData.LocalExtents;
            shapeInitData.SetDefaultPositionRotation( box.gameObject );
          } );
        }
        else if ( m_buttons.Selected.State.ShapeType == ShapeType.Cylinder ) {
          CreateShape<Cylinder>( shapeInitData, m_buttons.Selected.State.ShapeAsParent, cylinder =>
          {
            cylinder.Radius = axisData.Radius;
            cylinder.Height = axisData.Height;

            shapeInitData.SetPositionRotation( cylinder.gameObject, axisData.Direction );
          } );
        }
        else if ( m_buttons.Selected.State.ShapeType == ShapeType.HollowCylinder ) {
          CreateShape<HollowCylinder>( shapeInitData, m_buttons.Selected.State.ShapeAsParent, hollowCylinder =>
          {
            hollowCylinder.Thickness = axisData.Radius / 10f; // Arbitrary base thickness
            hollowCylinder.Radius = axisData.Radius;
            hollowCylinder.Height = axisData.Height;

            shapeInitData.SetPositionRotation( hollowCylinder.gameObject, axisData.Direction );
          } );
        }
        else if (m_buttons.Selected.State.ShapeType == ShapeType.Cone)
        {
          CreateShape<Cone>(shapeInitData, m_buttons.Selected.State.ShapeAsParent, cone =>
          {
            cone.TopRadius = axisData.Radius;
            cone.BottomRadius = axisData.Radius;
            cone.Height = axisData.Height;

            shapeInitData.SetPositionRotation(cone.gameObject, axisData.Direction);
          });
        }
        else if (m_buttons.Selected.State.ShapeType == ShapeType.HollowCone)
        {
          CreateShape<HollowCone>(shapeInitData, m_buttons.Selected.State.ShapeAsParent, hollowCone =>
          {
            hollowCone.Thickness = axisData.Radius / 10f; // Arbitrary base thickness
            hollowCone.TopRadius = axisData.Radius / 2f; // Arbitrary base top radius
            hollowCone.BottomRadius = axisData.Radius;
            hollowCone.Height = axisData.Height;

            shapeInitData.SetPositionRotation(hollowCone.gameObject, axisData.Direction);
          });
        }
        else if ( m_buttons.Selected.State.ShapeType == ShapeType.Capsule ) {
          CreateShape<Capsule>( shapeInitData, m_buttons.Selected.State.ShapeAsParent, capsule =>
          {
            capsule.Radius = axisData.Radius;
            capsule.Height = axisData.Height;

            shapeInitData.SetPositionRotation( capsule.gameObject, axisData.Direction );
          } );
        }
        else if ( m_buttons.Selected.State.ShapeType == ShapeType.Sphere ) {
          CreateShape<Sphere>( shapeInitData, m_buttons.Selected.State.ShapeAsParent, sphere =>
          {
            sphere.Radius = axisData.Radius;

            shapeInitData.SetPositionRotation( sphere.gameObject, axisData.Direction );
          } );
        }
        else if ( m_buttons.Selected.State.ShapeType == ShapeType.Mesh ) {
          CreateShape<Mesh>( shapeInitData, m_buttons.Selected.State.ShapeAsParent, mesh =>
          {
            mesh.SetSourceObject( shapeInitData.Filter.sharedMesh );
            // We don't want to set the position given the center of the bounds
            // since we're one-to-one with the mesh filter.
            mesh.transform.position = shapeInitData.Filter.transform.position;
            mesh.transform.rotation = shapeInitData.Filter.transform.rotation;
          } );
        }

        Reset();
      }
    }

    private string GetCurrentStateInfo()
    {
      var info = "Create shapes by selecting visual objects in Scene View.\n\n";
      if ( m_selection.Count == 0 )
        info += "Select highlighted visual object in Scene View" + AwaitingUserActionDots();
      else
        info += "Choose shape properties or more objects in Scene View" + AwaitingUserActionDots();
      return info;
    }

    private void Reset()
    {
      m_buttons.Reset();
      ClearSelection();
    }

    private void ClearSelection()
    {
      m_selection.Clear();
      // TODO HIGHLIGHT: Fix.
      //ClearVisualizedSelection();
    }

    private bool HandleKeyEscape( bool isSceneViewUpdate )
    {
      bool keyEscDown = isSceneViewUpdate ? Manager.KeyEscapeDown : Manager.IsKeyEscapeDown( Event.current );
      if ( !keyEscDown )
        return false;

      if ( isSceneViewUpdate )
        Manager.UseKeyEscapeDown();
      else
        Event.current.Use();

      if ( m_buttons.Selected != null )
        m_buttons.Selected = null;
      else if ( m_selection.Count > 0 )
        ClearSelection();
      else {
        PerformRemoveFromParent();
        return true;
      }

      return false;
    }

    private Utils.VisualPrimitive GetSelectedButtonVisualPrimitive()
    {
      Utils.VisualPrimitive vp = GetVisualPrimitive( m_visualPrimitiveName );

      if ( m_buttons.Selected == null || m_buttons.Selected.State.Axis == ShapeInitializationData.Axes.None ) {
        RemoveVisualPrimitive( m_visualPrimitiveName );
        return null;
      }

      var desiredType = Type.GetType( "AGXUnityEditor.Utils.VisualPrimitive" + m_buttons.Selected.State.ShapeType.ToString() + ", AGXUnityEditor" );

      // Desired type doesn't exist - remove current visual primitive if it exists.
      if ( desiredType == null ) {
        RemoveVisualPrimitive( m_visualPrimitiveName );
        return null;
      }

      // New visual primitive type. Remove old one.
      if ( vp != null && vp.GetType() != desiredType ) {
        RemoveVisualPrimitive( m_visualPrimitiveName );
        vp = null;
      }

      // Same type as selected button shape type.
      if ( vp != null )
        return vp;

      MethodInfo genericMethod = GetType().GetMethod( "GetOrCreateVisualPrimitive", BindingFlags.NonPublic | BindingFlags.Instance ).MakeGenericMethod( desiredType );
      vp = (Utils.VisualPrimitive)genericMethod.Invoke( this, new object[] { m_visualPrimitiveName, VisualPrimitiveShader } );
      if ( vp == null )
        return null;

      vp.Pickable = false;
      vp.Color = VisualPrimitiveColor;

      return vp;
    }

    private void UpdateVisualPrimitive( Utils.VisualPrimitive vp, ShapeInitializationData shapeInitData, ShapeInitializationData.AxisData axisData )
    {
      if ( vp == null )
        return;

      vp.Visible = shapeInitData != null && axisData != null;
      if ( !vp.Visible )
        return;

      if ( vp is Utils.VisualPrimitiveMesh ) {
        vp.Node.transform.localScale = shapeInitData.Filter.transform.lossyScale;
        vp.Node.transform.position   = shapeInitData.Filter.transform.position;
        vp.Node.transform.rotation   = shapeInitData.Filter.transform.rotation;
      }
      else {
        vp.Node.transform.localScale = Vector3.one;
        vp.Node.transform.position   = shapeInitData.WorldCenter;
#if UNITY_2018_1_OR_NEWER
        vp.Node.transform.rotation   = shapeInitData.Rotation * Quaternion.FromToRotation( Vector3.up, axisData.Direction ).normalized;
#else
        vp.Node.transform.rotation   = shapeInitData.Rotation * Quaternion.FromToRotation( Vector3.up, axisData.Direction ).Normalize();
#endif
      }

      if ( vp is Utils.VisualPrimitiveBox )
        ( vp as Utils.VisualPrimitiveBox ).SetSize( shapeInitData.LocalExtents );
      else if ( vp is Utils.VisualPrimitiveCylinder )
        ( vp as Utils.VisualPrimitiveCylinder ).SetSize( axisData.Radius, axisData.Height );
      else if ( vp is Utils.VisualPrimitiveCapsule )
        ( vp as Utils.VisualPrimitiveCapsule ).SetSize( axisData.Radius, axisData.Height );
      else if ( vp is Utils.VisualPrimitiveSphere )
        ( vp as Utils.VisualPrimitiveSphere ).SetSize( axisData.Radius );
      else if ( vp is Utils.VisualPrimitiveMesh )
        ( vp as Utils.VisualPrimitiveMesh ).SetSourceObject( shapeInitData.Filter.sharedMesh );
    }
  }
}
