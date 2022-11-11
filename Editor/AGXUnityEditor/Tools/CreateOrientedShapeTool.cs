using AGXUnity;
using AGXUnity.Collide;
using AGXUnity.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Mesh = AGXUnity.Collide.Mesh;

namespace AGXUnityEditor.Tools
{
  public class CreateOrientedShapeTool : Tool
  {
    private struct PrimitiveData
    {
      public bool boxReady;
      public agx.AffineMatrix4x4 boxTransform;
      public agx.Vec3 boxExtents;

      public bool capsuleReady;
      public agx.AffineMatrix4x4 capsuleRotation;
      public agx.Vec2 capsuleRadiusHeight;

      public bool cylinderReady;
      public agx.AffineMatrix4x4 cylinderRotation;
      public agx.Vec2 cylinderRadiusHeight;
    }

    private struct SelectionData
    {
      public MeshFilter Filter { get; private set; }
      public Vector3 LocalExtents { get; private set; }
      public Vector3 WorldCenter { get; private set; }
      public Quaternion Rotation { get; private set; }

      public float Radius { get => new Vector2( LocalExtents.MiddleValue(), LocalExtents.MinValue() ).magnitude; }

      public void SetGameObject( GameObject go )
      {
        Filter = go.GetComponent<MeshFilter>();
        Bounds localBounds = Filter.sharedMesh.bounds;
        LocalExtents = Filter.transform.InverseTransformDirection( Filter.transform.TransformVector( localBounds.extents ) );
        WorldCenter = Filter.transform.TransformPoint( localBounds.center );
        Rotation = Filter.transform.rotation;
      }

    }

    public enum ShapeType
    {
      Box,
      Cylinder,
      Capsule,
      Sphere,
      Mesh,
    }

    public static GameObject CreateShape<T>( Transform transform, Action<T> initializeAction ) where T : Shape
    {
      if ( initializeAction == null ) {
        Debug.LogError( "Unable to create shape without an initializeAction." );
        return null;
      }

      if ( transform == null )
        return null;

      GameObject shapeGameObject = Factory.Create<T>();

      Undo.RegisterCreatedObjectUndo( shapeGameObject, "New game object with shape component" );
      if ( AGXUnity.Rendering.DebugRenderManager.HasInstance )
        Undo.AddComponent<AGXUnity.Rendering.ShapeDebugRenderData>( shapeGameObject );

      initializeAction( shapeGameObject.GetComponent<T>() );

      Undo.SetTransformParent( shapeGameObject.transform, transform, "Shape as child to visual" );

      // SetTransformParent assigns some scale given the parent. We're in general not
      // interested in this scale since it will "un-scale" meshes (and the rest of the
      // shapes doesn't support scale so...).

      // If mesh and the mesh should be parent to the filter we have to move the
      // localScale to the shape game object.
      shapeGameObject.transform.localScale = Vector3.one;

      return shapeGameObject;
    }

    private Utils.OrientedShapeCreateButtons m_buttons = new Utils.OrientedShapeCreateButtons();
    private List<GameObject> m_selection       = new List<GameObject>();
    private const string m_visualPrimitiveName = "createShapeVisualPrimitive";
    private PrimitiveData m_primitiveData;
    private SelectionData m_selectionData;
    private Color m_preSelectionColor;

    private Thread m_boxCreateThread;
    private Thread m_cylinderCreateThread;
    private Thread m_capsuleCreateThread;

    public GameObject Parent { get; private set; }
    public Color VisualPrimitiveColor { get; set; }
    public string VisualPrimitiveShader { get; set; }

    public CreateOrientedShapeTool( GameObject parent )
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
        m_preSelectionColor = selected.GetComponent<MeshRenderer>().sharedMaterial.color;
        selected.GetComponent<MeshRenderer>().sharedMaterial.color = Color.green;
        m_selectionData.SetGameObject( selected );
        // TODO HIGHLIGHT: Add multiple.
        //SetVisualizedSelection( selected );
      }
      UpdatePrimitiveData();

      // TODO GUI: Why? Force inspector update instead?
      EditorUtility.SetDirty( Parent );
    }

    public void OnInspectorGUI()
    {
      if ( HandleKeyEscape( false ) )
        return;

      var skin = InspectorEditor.Skin;

      InspectorGUI.OnDropdownToolBegin( GetCurrentStateInfo() );

      ShapeType? previewShape = null;

      UnityEngine.GUI.enabled = m_selection.Count > 0;
      m_buttons.Update( Event.current, ( type ) =>
        {
          if ( type == ShapeType.Box ) {
            CreateShape<Box>( m_selectionData.Filter.transform, box =>
            {
              m_boxCreateThread.Join();
              box.HalfExtents = m_primitiveData.boxExtents.ToVector3();

              box.transform.position = m_selectionData.WorldCenter;
              box.transform.rotation = m_selectionData.Rotation;
              box.transform.rotation *= ( new agx.Quat( m_primitiveData.boxTransform ) ).ToHandedQuaternion();
            } );
          }
          else if ( type == ShapeType.Cylinder ) {
            CreateShape<Cylinder>( m_selectionData.Filter.transform, cylinder =>
            {
              m_cylinderCreateThread.Join();
              cylinder.Radius = (float)m_primitiveData.cylinderRadiusHeight.x;
              cylinder.Height = (float)m_primitiveData.cylinderRadiusHeight.y;

              cylinder.transform.position = m_selectionData.WorldCenter;
              cylinder.transform.rotation = m_selectionData.Rotation;
              cylinder.transform.rotation *= ( new agx.Quat( m_primitiveData.cylinderRotation ) ).ToHandedQuaternion();
            } );
          }
          else if ( type == ShapeType.Capsule ) {
            CreateShape<Capsule>( m_selectionData.Filter.transform, capsule =>
            {
              m_capsuleCreateThread.Join();
              capsule.Radius = (float)m_primitiveData.capsuleRadiusHeight.x;
              capsule.Height = (float)m_primitiveData.capsuleRadiusHeight.y;

              capsule.transform.position = m_selectionData.WorldCenter;
              capsule.transform.rotation = m_selectionData.Rotation;
              capsule.transform.rotation *= ( new agx.Quat( m_primitiveData.capsuleRotation ) ).ToHandedQuaternion();
            } );
          }
          else if ( type == ShapeType.Sphere ) {
            CreateShape<Sphere>( m_selectionData.Filter.transform, sphere =>
            {
              sphere.Radius = m_selectionData.Radius;

              sphere.transform.position = m_selectionData.WorldCenter;
              sphere.transform.rotation = m_selectionData.Rotation;
            } );
          }
          else if ( type == ShapeType.Mesh ) {
            CreateShape<Mesh>( m_selectionData.Filter.transform, mesh =>
            {
              mesh.SetSourceObject( m_selectionData.Filter.sharedMesh );
              // We don't want to set the position given the center of the bounds
              // since we're one-to-one with the mesh filter.
              mesh.transform.position = m_selectionData.Filter.transform.position;
              mesh.transform.rotation = m_selectionData.Filter.transform.rotation;
            } );
          }

          Reset();
        }, ( type ) => previewShape = type );

      if ( Event.current.type == EventType.Repaint )
        UpdateVisualPrimitive( previewShape );

      UnityEngine.GUI.enabled = true;

      InspectorGUI.OnDropdownToolEnd();

      EditorUtility.SetDirty( Parent );
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
      ClearSelection();
    }

    private void ClearSelection()
    {
      if ( m_selection.Count > 0 )
        m_selection[ 0 ].GetComponent<MeshRenderer>().sharedMaterial.color = m_preSelectionColor;
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

      if ( m_selection.Count > 0 )
        ClearSelection();
      else {
        PerformRemoveFromParent();
        return true;
      }

      return false;
    }

    private void UpdatePrimitiveData()
    {
      var vertices = m_selectionData.Filter.sharedMesh.vertices;
      agx.Vec3Vector agxVerts = new agx.Vec3Vector(vertices.Length);
      foreach ( var v in vertices )
        agxVerts.Add( v.ToHandedVec3() );

      m_primitiveData.boxReady = false;
      m_primitiveData.cylinderReady = false;
      m_primitiveData.capsuleReady = false;

      m_boxCreateThread = new Thread( () =>
      {
        agxUtil.agxUtilSWIG.computeOrientedBox( agxVerts, ref m_primitiveData.boxExtents, ref m_primitiveData.boxTransform );
        m_primitiveData.boxReady = true;
      } );
      m_boxCreateThread.Start();

      m_cylinderCreateThread = new Thread( () =>
      {
        agxUtil.agxUtilSWIG.computeOrientedCylinder( agxVerts, ref m_primitiveData.cylinderRadiusHeight, ref m_primitiveData.cylinderRotation );
        m_primitiveData.cylinderReady = true;
      } );
      m_cylinderCreateThread.Start();

      m_capsuleCreateThread= new Thread( () =>
      {
        agxUtil.agxUtilSWIG.computeOrientedCapsule( agxVerts, ref m_primitiveData.capsuleRadiusHeight, ref m_primitiveData.capsuleRotation );
        m_primitiveData.capsuleReady = true;
      } );
      m_capsuleCreateThread.Start();

    }

    private void UpdateVisualPrimitive( ShapeType? type )
    {
      Utils.VisualPrimitive vp = GetVisualPrimitive( m_visualPrimitiveName );

      if ( type == null ) {
        RemoveVisualPrimitive( m_visualPrimitiveName );
        return;
      }

      var desiredType = Type.GetType( "AGXUnityEditor.Utils.VisualPrimitive" + type.ToString() + ", AGXUnityEditor" );

      // Desired type doesn't exist - remove current visual primitive if it exists.
      if ( desiredType == null ) {
        RemoveVisualPrimitive( m_visualPrimitiveName );
        return;
      }

      // New visual primitive type. Remove old one.
      if ( vp != null && vp.GetType() != desiredType ) {
        RemoveVisualPrimitive( m_visualPrimitiveName );
        vp = null;
      }

      // Same type as selected button shape type.
      if ( vp == null ) {
        MethodInfo genericMethod = GetType().GetMethod( "GetOrCreateVisualPrimitive", BindingFlags.NonPublic | BindingFlags.Instance ).MakeGenericMethod( desiredType );
        vp = (Utils.VisualPrimitive)genericMethod.Invoke( this, new object[] { m_visualPrimitiveName, VisualPrimitiveShader } );
      }

      if ( vp == null )
        return;

      vp.Pickable = false;
      vp.Color = VisualPrimitiveColor;

      vp.Visible = type != null;
      if ( !vp.Visible )
        return;

      if ( vp is Utils.VisualPrimitiveMesh ) {
        vp.Node.transform.localScale = m_selectionData.Filter.transform.lossyScale;
        vp.Node.transform.position = m_selectionData.Filter.transform.position;
        vp.Node.transform.rotation = m_selectionData.Filter.transform.rotation;
      }
      else {
        vp.Node.transform.localScale = Vector3.one;
        vp.Node.transform.position = m_selectionData.WorldCenter;
        vp.Node.transform.rotation = m_selectionData.Rotation;
      }

      if ( vp is Utils.VisualPrimitiveBox ) {
        if ( !m_primitiveData.boxReady ) {
          RemoveVisualPrimitive( m_visualPrimitiveName );
          return;
        }
        ( vp as Utils.VisualPrimitiveBox ).SetSize( m_primitiveData.boxExtents.ToVector3() );
        vp.Node.transform.rotation *= new agx.Quat( m_primitiveData.boxTransform ).ToHandedQuaternion();
      }
      else if ( vp is Utils.VisualPrimitiveCylinder ) {
        if ( !m_primitiveData.cylinderReady ) {
          RemoveVisualPrimitive( m_visualPrimitiveName );
          return;
        }
        ( vp as Utils.VisualPrimitiveCylinder ).SetSize( (float)m_primitiveData.cylinderRadiusHeight.x, (float)m_primitiveData.cylinderRadiusHeight.y );
        vp.Node.transform.rotation *= new agx.Quat( m_primitiveData.cylinderRotation ).ToHandedQuaternion();
      }
      else if ( vp is Utils.VisualPrimitiveCapsule ) {
        if ( !m_primitiveData.capsuleReady ) {
          RemoveVisualPrimitive( m_visualPrimitiveName );
          return;
        }
        ( vp as Utils.VisualPrimitiveCapsule ).SetSize( (float)m_primitiveData.capsuleRadiusHeight.x, (float)m_primitiveData.capsuleRadiusHeight.y );
        vp.Node.transform.rotation *= new agx.Quat( m_primitiveData.capsuleRotation ).ToHandedQuaternion();
      }
      else if ( vp is Utils.VisualPrimitiveSphere )
        ( vp as Utils.VisualPrimitiveSphere ).SetSize( m_selectionData.Radius );
      else if ( vp is Utils.VisualPrimitiveMesh )
        ( vp as Utils.VisualPrimitiveMesh ).SetSourceObject( m_selectionData.Filter.sharedMesh );
    }
  }
}
