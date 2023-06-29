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
      // These variables are set by the background threads generating the primitives when they are done
      public bool boxReady;
      public bool capsuleReady;
      public bool cylinderReady;

      public agx.AffineMatrix4x4 boxTransform;
      public agx.Vec3 boxExtents;

      public agx.AffineMatrix4x4 capsuleRotation;
      public agx.Vec2 capsuleRadiusHeight;

      public agx.AffineMatrix4x4 cylinderRotation;
      public agx.Vec2 cylinderRadiusHeight;
    }

    private class SelectionData
    {
      public GameObject GameObject { get; private set; }
      public MeshFilter Filter { get; private set; }
      public Vector3 LocalExtents { get; private set; }
      public Vector3 WorldCenter { get; private set; }
      public Quaternion Rotation { get; private set; }

      public float Radius { get => new Vector2( LocalExtents.MiddleValue(), LocalExtents.MinValue() ).magnitude; }

      public PrimitiveData PrimitiveData;

      // Generating oriented primitives might take some time, to avoid freezing the UI during this time
      // The work is offloaded on background threads
      public Thread BoxCreateThread { get; private set; }
      public Thread CylinderCreateThread { get; private set; }
      public Thread CapsuleCreateThread { get; private set; }

      public string VisualPrimitiveName;
      public Color VisualPrimitiveColor { get; set; } = Color.red;
      public string VisualPrimitiveShader { get; set; } = "Diffuse";

      public SelectionData( GameObject go )
      {
        GameObject = go;
        Filter = go.GetComponent<MeshFilter>();
        Bounds localBounds = Filter.sharedMesh.bounds;
        LocalExtents = Filter.transform.InverseTransformDirection( Filter.transform.TransformVector( localBounds.extents ) );
        WorldCenter = Filter.transform.TransformPoint( localBounds.center );
        Rotation = Filter.transform.rotation;
        PrimitiveData = new PrimitiveData();

        VisualPrimitiveName = "createShapeVisualPrimitive" + go.name;

        agx.Vec3 scale = go.transform.localScale.ToVec3();
        var vertices = Filter.sharedMesh.vertices;
        agx.Vec3Vector agxVerts = new agx.Vec3Vector(vertices.Length);
        foreach ( var v in vertices )
          agxVerts.Add( agx.Vec3.mul(v.ToHandedVec3(), scale));

        PrimitiveData.boxReady = false;
        PrimitiveData.cylinderReady = false;
        PrimitiveData.capsuleReady = false;

        SceneViewHighlight.Add( go );

        BoxCreateThread = new Thread( () =>
        {
          agxUtil.agxUtilSWIG.computeOrientedBox( agxVerts, ref PrimitiveData.boxExtents, ref PrimitiveData.boxTransform );
          PrimitiveData.boxReady = true;
        } );
        BoxCreateThread.Start();

        CylinderCreateThread = new Thread( () =>
        {
          agxUtil.agxUtilSWIG.computeOrientedCylinder( agxVerts, ref PrimitiveData.cylinderRadiusHeight, ref PrimitiveData.cylinderRotation );
          PrimitiveData.cylinderReady = true;
        } );
        CylinderCreateThread.Start();

        CapsuleCreateThread = new Thread( () =>
        {
          agxUtil.agxUtilSWIG.computeOrientedCapsule( agxVerts, ref PrimitiveData.capsuleRadiusHeight, ref PrimitiveData.capsuleRotation );
          PrimitiveData.capsuleReady = true;
        } );
        CapsuleCreateThread.Start();
      }

      public void Reset()
      {
        SceneViewHighlight.Remove( GameObject );
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
    private List<SelectionData> m_selection            = new List<SelectionData>();

    public GameObject Parent { get; private set; }

    public CreateOrientedShapeTool( GameObject parent )
      : base( isSingleInstanceTool: true )
    {
      Parent = parent;
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
      if ( !(Event.current.shift || Event.current.control) )
        ClearSelection();

      if ( selected != null ) {
        if ( !m_selection.Exists( s => s.GameObject == selected ) )
          m_selection.Add( new SelectionData( selected ) );
        else if ( Event.current.control )
          m_selection.RemoveAll( s =>
           {
             if ( s.GameObject == selected ) {
               s.Reset();
               return true;
             }
             return false;
           } );
      }

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
          foreach ( var s in m_selection ) {
            if ( type == ShapeType.Box ) {
              CreateShape<Box>( s.Filter.transform, box =>
              {
                s.BoxCreateThread.Join();
                box.HalfExtents = s.PrimitiveData.boxExtents.ToVector3();

                box.transform.position = s.WorldCenter;
                box.transform.rotation = s.Rotation;
                box.transform.rotation *= ( new agx.Quat( s.PrimitiveData.boxTransform ) ).ToHandedQuaternion();
              } );
            }
            else if ( type == ShapeType.Cylinder ) {
              CreateShape<Cylinder>( s.Filter.transform, cylinder =>
              {
                s.CylinderCreateThread.Join();
                cylinder.Radius = (float)s.PrimitiveData.cylinderRadiusHeight.x;
                cylinder.Height = (float)s.PrimitiveData.cylinderRadiusHeight.y;

                cylinder.transform.position = s.WorldCenter;
                cylinder.transform.rotation = s.Rotation;
                cylinder.transform.rotation *= ( new agx.Quat( s.PrimitiveData.cylinderRotation ) ).ToHandedQuaternion();
              } );
            }
            else if ( type == ShapeType.Capsule ) {
              CreateShape<Capsule>( s.Filter.transform, capsule =>
              {
                s.CapsuleCreateThread.Join();
                capsule.Radius = (float)s.PrimitiveData.capsuleRadiusHeight.x;
                capsule.Height = (float)s.PrimitiveData.capsuleRadiusHeight.y;

                capsule.transform.position = s.WorldCenter;
                capsule.transform.rotation = s.Rotation;
                capsule.transform.rotation *= ( new agx.Quat( s.PrimitiveData.capsuleRotation ) ).ToHandedQuaternion();
              } );
            }
            else if ( type == ShapeType.Sphere ) {
              CreateShape<Sphere>( s.Filter.transform, sphere =>
              {
                sphere.Radius = s.Radius;
                sphere.transform.position = s.WorldCenter;
                sphere.transform.rotation = s.Rotation;
              } );
            }
            else if ( type == ShapeType.Mesh ) {
              CreateShape<Mesh>( s.Filter.transform, mesh =>
              {
                mesh.SetSourceObject( s.Filter.sharedMesh );
                // We don't want to set the position given the center of the bounds
                // since we're one-to-one with the mesh filter.
                mesh.transform.position = s.Filter.transform.position;
                mesh.transform.rotation = s.Filter.transform.rotation;
              } );
            }
          }

          Reset();
          PerformRemoveFromParent();
          EditorUtility.SetDirty( Parent );
        }, ( type ) => previewShape = type );

      if ( Event.current.type == EventType.Repaint )
        foreach ( var s in m_selection )
          UpdateVisualPrimitive( previewShape, s );

      UnityEngine.GUI.enabled = true;

      InspectorGUI.OnDropdownToolEnd();
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
      m_selection.ForEach( s =>
      {
        RemoveVisualPrimitive( s.VisualPrimitiveName );
        s.Reset(); 
      } );
      m_selection.Clear();
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

    private void UpdateVisualPrimitive( ShapeType? type, SelectionData sel )
    {
      Utils.VisualPrimitive vp = GetVisualPrimitive( (string)sel.VisualPrimitiveName );

      if ( type == null ) {
        RemoveVisualPrimitive( (string)sel.VisualPrimitiveName );
        return;
      }

      var desiredType = Type.GetType( "AGXUnityEditor.Utils.VisualPrimitive" + type.ToString() + ", AGXUnityEditor" );

      // Desired type doesn't exist - remove current visual primitive if it exists.
      if ( desiredType == null ) {
        RemoveVisualPrimitive( (string)sel.VisualPrimitiveName );
        return;
      }

      // New visual primitive type. Remove old one.
      if ( vp != null && vp.GetType() != desiredType ) {
        RemoveVisualPrimitive( (string)sel.VisualPrimitiveName );
        vp = null;
      }

      // Same type as selected button shape type.
      if ( vp == null ) {
        MethodInfo genericMethod = GetType().GetMethod( "GetOrCreateVisualPrimitive", BindingFlags.NonPublic | BindingFlags.Instance ).MakeGenericMethod( desiredType );
        vp = (Utils.VisualPrimitive)genericMethod.Invoke( this, new object[] { sel.VisualPrimitiveName, sel.VisualPrimitiveShader } );
      }

      if ( vp == null )
        return;

      vp.Pickable = false;
      vp.Color = sel.VisualPrimitiveColor;

      vp.Visible = type != null;
      if ( !vp.Visible )
        return;

      if ( vp is Utils.VisualPrimitiveMesh ) {
        vp.Node.transform.localScale = sel.Filter.transform.lossyScale;
        vp.Node.transform.position = sel.Filter.transform.position;
        vp.Node.transform.rotation = sel.Filter.transform.rotation;
      }
      else {
        vp.Node.transform.localScale = Vector3.one;
        vp.Node.transform.position = sel.WorldCenter;
        vp.Node.transform.rotation = sel.Rotation;
      }

      if ( vp is Utils.VisualPrimitiveBox ) {
        if ( !m_selection[ 0 ].PrimitiveData.boxReady ) {
          RemoveVisualPrimitive( sel.VisualPrimitiveName );
          return;
        }
        ( vp as Utils.VisualPrimitiveBox ).SetSize( Extensions.ToVector3( sel.PrimitiveData.boxExtents ) );
        vp.Node.transform.rotation *= new agx.Quat( sel.PrimitiveData.boxTransform ).ToHandedQuaternion();
      }
      else if ( vp is Utils.VisualPrimitiveCylinder ) {
        if ( !sel.PrimitiveData.cylinderReady ) {
          RemoveVisualPrimitive( sel.VisualPrimitiveName );
          return;
        }
        ( vp as Utils.VisualPrimitiveCylinder ).SetSize( (float)sel.PrimitiveData.cylinderRadiusHeight.x, (float)sel.PrimitiveData.cylinderRadiusHeight.y );
        vp.Node.transform.rotation *= new agx.Quat( sel.PrimitiveData.cylinderRotation ).ToHandedQuaternion();
      }
      else if ( vp is Utils.VisualPrimitiveCapsule ) {
        if ( !sel.PrimitiveData.capsuleReady ) {
          RemoveVisualPrimitive( sel.VisualPrimitiveName );
          return;
        }
        ( vp as Utils.VisualPrimitiveCapsule ).SetSize( (float)sel.PrimitiveData.capsuleRadiusHeight.x, (float)sel.PrimitiveData.capsuleRadiusHeight.y );
        vp.Node.transform.rotation *= new agx.Quat( sel.PrimitiveData.capsuleRotation ).ToHandedQuaternion();
      }
      else if ( vp is Utils.VisualPrimitiveSphere )
        ( vp as Utils.VisualPrimitiveSphere ).SetSize( sel.Radius );
      else if ( vp is Utils.VisualPrimitiveMesh )
        ( vp as Utils.VisualPrimitiveMesh ).SetSourceObject( (UnityEngine.Mesh)sel.Filter.sharedMesh );
    }
  }
}
