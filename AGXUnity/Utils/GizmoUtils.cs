using UnityEngine;

public static class GizmoUtils
{
  public static void DrawCylinder( Vector3 from, Vector3 to, float radius = 0.1f )
  {
    var midpoint = (from + to)/2;
    var axis = to - from;
    var rot = Quaternion.FromToRotation(Vector3.up,axis);
    var scale = new Vector3(radius, axis.magnitude / 2, radius);
    Gizmos.DrawMesh( Resources.GetBuiltinResource<Mesh>( "Cylinder.fbx" ), midpoint, rot, scale );
  }
}
