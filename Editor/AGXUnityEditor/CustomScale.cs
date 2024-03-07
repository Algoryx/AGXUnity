using AGXUnity.Collide;
using AGXUnityEditor;
using AGXUnityEditor.Utils;
using System;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

[CustomContext( typeof( AGXUnity.Collide.Shape ) )]
public class ScaleContext : EditorToolContext
{
  protected override Type GetEditorToolType( Tool tool )
  {
    switch ( tool ) {
      case Tool.Scale:
        return typeof( ShapeScaleTool );
      default:
        return base.GetEditorToolType( tool );
    }
  }
}

class ShapeScaleTool : EditorTool
{
  private Transform transform;
  private Vector3 pos;
  private Quaternion rot;

  private void HandleBox( Box box )
  {
    box.HalfExtents = Handles.ScaleHandle( box.HalfExtents, pos, rot );
  }

  private void HandleCapsule( Capsule cap )
  {
    Vector3 scale = new Vector3(cap.Radius, cap.Height,cap.Radius);
    var newScale = Handles.ScaleHandle( scale, pos, rot );

    cap.Height = newScale.y;
    if ( newScale.x != scale.x )
      cap.Radius = newScale.x;
    else if ( newScale.z != scale.z )
      cap.Radius = newScale.z;
  }

  private void HandleCylinder( Cylinder cyl )
  {
    Vector3 scale = new Vector3(cyl.Radius, cyl.Height,cyl.Radius);
    var newScale = Handles.ScaleHandle( scale, pos, rot );

    cyl.Height = newScale.y;
    if ( newScale.x != scale.x )
      cyl.Radius = newScale.x;
    else if ( newScale.z != scale.z )
      cyl.Radius = newScale.z;
  }

  private void HandleSphere( Sphere sphere )
  {
    Vector3 scale = new Vector3(sphere.Radius, sphere.Radius, sphere.Radius);
    var newScale = Handles.ScaleHandle( scale, pos, rot );

    if ( newScale.y != scale.y )
      sphere.Radius = newScale.y;
    if ( newScale.x != scale.x )
      sphere.Radius = newScale.x;
    else if ( newScale.z != scale.z )
      sphere.Radius = newScale.z;
  }

  float m_initialBR = 0.0f;
  float m_initialTR = 0.0f;
  float m_initialHeight = 0.0f;

  static Color s_XAxisColor = new Color(73f / 85f, 62f / 255f, 29f / 255f, 0.93f);
  static Color s_YAxisColor = new Color(154f / 255f, 81f / 85f, 24f / 85f, 0.93f);
  static Color s_ZAxisColor = new Color(58f / 255f, 122f / 255f, 248f / 255f, 0.93f);

  private void HandleCone( Cone cone )
  {
    var size = HandleUtility.GetHandleSize(pos);

    var c = Handles.color;

    var center = pos + transform.up * cone.Height * 0.5f;

    Handles.color = s_ZAxisColor;
    cone.BottomRadius = Handles.ScaleSlider( cone.BottomRadius, pos, transform.forward, rot, size, 0.1f );
    Handles.color = s_XAxisColor;
    cone.TopRadius = Handles.ScaleSlider( cone.TopRadius, pos + transform.up * cone.Height, transform.forward, rot, size, 0.1f );
    Handles.color = s_YAxisColor;
    var newHeight = Handles.ScaleSlider( cone.Height, center, transform.up, rot, size, 0.1f );
    if(newHeight != cone.Height ) {
      cone.Height = newHeight;
      transform.position = center - transform.up * cone.Height * 0.5f;
    }

    Handles.color = c;

    if ( Event.current.type == EventType.MouseDown ) {
      m_initialBR = cone.BottomRadius;
      m_initialTR = cone.TopRadius;
      m_initialHeight = cone.Height;
    }

    EditorGUI.BeginChangeCheck();
    var scale = Handles.ScaleValueHandle( 1.0f, center, rot, size, Handles.CubeHandleCap, 0.1f );
    if ( EditorGUI.EndChangeCheck() ) {
      cone.BottomRadius = m_initialBR * scale;
      cone.TopRadius = m_initialTR * scale;
      cone.Height = m_initialHeight * scale;
      transform.position = center - transform.up * cone.Height * 0.5f;
    }
  }

  private void HandleHollowCylinder( HollowCylinder cyl )
  {
    Vector3 scale = new Vector3(cyl.Radius, cyl.Height,cyl.Radius);
    var newScale = Handles.ScaleHandle( scale, pos, rot );

    cyl.Height = newScale.y;
    if ( newScale.x != scale.x )
      cyl.Radius = newScale.x;
    else if ( newScale.z != scale.z )
      cyl.Radius = newScale.z;
  }

  public override void OnToolGUI( EditorWindow _ )
  {
    if ( target == null )
      return;

    var GO = target as GameObject;

    transform = GO.transform;
    pos = GO.transform.position;
    rot = GO.transform.rotation;

    var shape = GO.GetComponent<Shape>();
    Undo.RecordObject( shape, "Resize collider" );
    Undo.RecordObject( transform, "transform");

    switch ( shape ) {
      case Box box:               HandleBox( box );               break;
      case Capsule cap:           HandleCapsule( cap );           break;
      case Cylinder cyl:          HandleCylinder( cyl );          break;
      case Sphere sphere:         HandleSphere( sphere );         break;
      case Cone cone:             HandleCone( cone );             break;
      case HollowCylinder holCyl: HandleHollowCylinder( holCyl ); break;
    }
    
  }
}