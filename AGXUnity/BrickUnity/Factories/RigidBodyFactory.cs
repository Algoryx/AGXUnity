using System;
using UnityEngine;

using AGXUnity.Utils;

using B_RigidBody = Brick.Physics.Mechanics.RigidBody;
using B_Geometry = Brick.Physics.Geometry;

namespace AGXUnity.BrickUnity.Factories
{
  public static class RigidBodyFactory
  {
    public static AGXUnity.RigidBody AddRigidBody(this GameObject go, B_RigidBody b_body)
    {
      var au_body = go.AddComponent<AGXUnity.RigidBody>();
      au_body.MotionControl = b_body.MotionControl.ToAgxMotionControl();

      var mp3d = b_body.MassProperties as Brick.Physics.MassProperties.MassProperties3D;
      if (!mp3d._massIsDefault)
      {
        au_body.MassProperties.Mass.UserValue = Convert.ToSingle(b_body.Mass);
        au_body.MassProperties.Mass.UseDefault = false;
      }

      if (!mp3d._inertiaIsDefault)
      {
        au_body.MassProperties.InertiaDiagonal.UserValue = mp3d.Inertia.ToVector3();
        au_body.MassProperties.InertiaDiagonal.UseDefault = false;
      }

      if (!mp3d._localTransformIsDefault)
      {
        au_body.MassProperties.CenterOfMassOffset.UserValue = b_body.MassProperties.LocalPosition.ToHandedVector3();
        au_body.MassProperties.CenterOfMassOffset.UseDefault = false;
      }

      return au_body;
    }

    public static AGXUnity.Collide.Shape AddShape(this GameObject go, B_Geometry b_geometry)
    {
      AGXUnity.Collide.Shape au_shape;
      switch (b_geometry)
      {
        case B_Geometry.Box b_box:
          au_shape = go.AddBox(b_box);
          break;
        case B_Geometry.Sphere b_sphere:
          au_shape = go.AddSphere(b_sphere);
          break;
        case B_Geometry.Cylinder b_cylinder:
          au_shape = go.AddCylinder(b_cylinder);
          break;
        case B_Geometry.Trimesh b_triMesh:
          au_shape = go.AddMesh(b_triMesh);
          break;
        case B_Geometry.Plane b_plane:
          au_shape = go.AddPlane(b_plane);
          break;
        default:
          Debug.LogError($"Cannot create Shape for Brick object {b_geometry}. Unsupported type: {b_geometry.GetType()}");
          return null;
      }
      au_shape.CollisionsEnabled = b_geometry.EnableCollisions;
      return au_shape;
    }

    public static AGXUnity.Collide.Sphere AddSphere(this GameObject go, B_Geometry.Sphere b_sphere)
    {
      var au_sphere = go.AddComponent<AGXUnity.Collide.Sphere>();
      au_sphere.Radius = (float)b_sphere.Radius;
      return au_sphere;
    }

    public static AGXUnity.Collide.Box AddBox(this GameObject go, B_Geometry.Box b_box)
    {
      var au_box = go.AddComponent<AGXUnity.Collide.Box>();
      au_box.HalfExtents = (b_box.Lengths / 2.0).ToVector3();
      return au_box;
    }

    public static AGXUnity.Collide.Cylinder AddCylinder(this GameObject go, B_Geometry.Cylinder b_cylinder)
    {
      var au_cylinder = go.AddComponent<AGXUnity.Collide.Cylinder>();
      au_cylinder.Radius = (float)b_cylinder.Radius;
      au_cylinder.Height = (float)b_cylinder.Length;
      return au_cylinder;
    }

    public static AGXUnity.Collide.Mesh AddMesh(this GameObject go, B_Geometry.Trimesh b_triMesh)
    {
      var au_mesh = go.AddComponent<AGXUnity.Collide.Mesh>();
      // Errors can occur while reading trimesh. For example if file not found.
      b_triMesh.InitTask.Wait();

      // Copy vertices and indices into agx Vectors for processing in
      // AGXUnity.Utils.MeshSplitter as is done in
      // AGXUnityEditor.IO.InputAGXFile
      // Can this be done avioded? If Brick already provided these. Or
      // if MeshSplitter could take a list of agx.Vec3 instead of an agx.Vector?
      var agxVertices = new agx.Vec3Vector();
      var agxIndices = new agx.UInt32Vector();
      foreach (var v in b_triMesh.Vertices)
      {
        agxVertices.Add(v);
      }
      foreach (var i in b_triMesh.Indices)
      {
        agxIndices.Add((uint)i);
      }

      // Create Unity meshes with AGXUnitys utility MeshSplitter
      var meshes = MeshSplitter.Split(
                                        agxVertices,
                                        agxIndices,
                                        v => v.ToHandedVector3(),
                                        UInt16.MaxValue
                                        ).Meshes;
      au_mesh.SetSourceObject(null);
      foreach (var mesh in meshes)
      {
        au_mesh.AddSourceObject(mesh);
      }
      return au_mesh;
    }

    public static void SetPlaneEquationTransform(this GameObject go, Brick.Math.Vec3 b_normal, double b_d)
    {
      // Set the transform according to the plane equation
      var normal = b_normal.ToHandedVector3();
      Quaternion rotation = new Quaternion();
      rotation.SetFromToRotation(go.transform.up, normal);
      var distance = (float)b_d / normal.magnitude;

      // Rotate transforms up vector to point along
      go.transform.localRotation = rotation;
      go.transform.localPosition = -normal.normalized * distance;
    }

    public static AGXUnity.Collide.Plane AddPlane(this GameObject go, B_Geometry.Plane b_plane)
    {
      GameObject planeEquation = new GameObject("PlaneEquation");
      planeEquation.transform.parent = go.transform;

      var au_plane = planeEquation.AddComponent<AGXUnity.Collide.Plane>();

      var b_normal = new Brick.Math.Vec3(b_plane.A, b_plane.B, b_plane.C);
      planeEquation.SetPlaneEquationTransform(b_normal, b_plane.D);

      return au_plane;
    }
  }
}