using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityMeshImporter;

using B_VisualShape = Brick.Visual.Shape;

namespace AGXUnity.BrickUnity.Factories
{
  public static class VisualFactory
  {
    public static GameObject CreatePlaneVisual(Brick.Math.Vec3 b_normal, double d, Brick.Math.Vec2 b_widths)
    {
      var rendererPath = "Debug/PlaneRenderer";
      var instance = AGXUnity.PrefabLoader.Instantiate<GameObject>(rendererPath);
      var go_parent = new GameObject();
      instance.transform.parent = go_parent.transform;

      instance.transform.localScale = new Vector3
      {
        x = (float)b_widths.X / 10f,
        y = 1f,
        z = (float)b_widths.Y / 10f
      };
      instance.name = "PlaneEquationRenderer";

      instance.SetPlaneEquationTransform(b_normal, d);

      return go_parent;
    }

    public static GameObject CreateBoxVisual(Brick.Math.Vec3 b_lengths)
    {
      var rendererPath = "Debug/BoxRenderer";
      var instance = AGXUnity.PrefabLoader.Instantiate<GameObject>(rendererPath);

      instance.transform.localScale = b_lengths.ToVector3();

      return instance;
    }

    public static GameObject CreateSphereVisual(double radius)
    {
      var rendererPath = "Debug/SphereRenderer";
      var instance = AGXUnity.PrefabLoader.Instantiate<GameObject>(rendererPath);

      instance.transform.localScale = Vector3.one * (float)radius * 2.0f;

      return instance;
    }

    public static GameObject CreateCylinderVisual(double radius, double length)
    {
      var rendererPath = "Debug/CylinderRenderer";
      var instance = AGXUnity.PrefabLoader.Instantiate<GameObject>(rendererPath);
      instance.transform.localScale = new Vector3
      {
        x = (float)radius * 2f,
        y = (float)length / 2f,
        z = (float)radius * 2f
      };
      return instance;
    }

    public static void FindAndDestoryMeshCollider(this GameObject go)
    {
      var extra_collider = go.GetComponent<MeshCollider>();
      if (extra_collider != null)
        UnityEngine.Object.DestroyImmediate(extra_collider);
    }

    public static GameObject CreateFileVisual(string filepath)
    {
      // Currently a problem if "/" is included in names/comments in .obj files
      // See: https://github.com/assimp/assimp/issues/3532
      var go = MeshImporter.Load(filepath);
      if (go is null)
        Debug.LogError($"Could not locate: {filepath}");
      List<Transform> children = new List<Transform>();
      // Have to add children to list first because it becomes wierd too loop through all
      // children and removing some at the same time as adding new
      foreach (Transform child in go.transform)
      {
        children.Add(child);
      }
      foreach (var child in children)
      {
        var nrChildren = child.childCount;
        var grandChildren = new List<Transform>();
        foreach (Transform grandchild in child)
        {
          grandChildren.Add(grandchild);
          grandchild.gameObject.FindAndDestoryMeshCollider();
        }
        // Since the number of children of child
        // is reduced when setting the parent,
        // we have to do it in a second step
        foreach(Transform grandchild in grandChildren)
           grandchild.parent = go.transform;

        if (nrChildren > 0)
          UnityEngine.Object.DestroyImmediate(child.gameObject);
        else
          child.gameObject.FindAndDestoryMeshCollider();
      }

      return go;
    }

    public static GameObject CreateVisual(B_VisualShape b_visual)
    {
      GameObject go_visual = null;

      switch (b_visual)
      {
        case Brick.Visual.Box b_vBox:
          go_visual = CreateBoxVisual(b_vBox.Lengths);
          break;
        case Brick.Visual.Sphere b_vSphere:
          go_visual = CreateSphereVisual(b_vSphere.Radius);
          break;
        case Brick.Visual.Cylinder b_vCylinder:
          go_visual = CreateCylinderVisual(b_vCylinder.Radius, b_vCylinder.Length);
          break;
        case Brick.Visual.Plane b_vPlane:
          var normal = new Brick.Math.Vec3(b_vPlane.A, b_vPlane.B, b_vPlane.C);
          go_visual = CreatePlaneVisual(normal, b_vPlane.D, b_vPlane.Widths);
          break;
        case Brick.Visual.File b_vMeshFile:
          go_visual = CreateFileVisual(b_vMeshFile.AbsoluteFilepath);
          go_visual.transform.localScale = b_vMeshFile.Scaling.ToVector3();
          break;
        default:
          throw new Exception($"Unknown shape! {b_visual._ModelValuePath}");
      }

      foreach (var renderer in go_visual.GetComponentsInChildren<MeshRenderer>())
      {
        if (!(b_visual is Brick.Visual.File)) // The mesh importer will already have added a material
        {
          renderer.sharedMaterial = new UnityEngine.Material(Shader.Find("Standard"));
        }

        if (!b_visual._colorIsDefault)
          renderer.sharedMaterial.color = b_visual.Color.ToUnityColor();

        if (!b_visual._metallicIsDefault)
          renderer.sharedMaterial.SetFloat("_Metallic", (float)b_visual.Metallic);

        if (!b_visual._smoothnessIsDefault)
          renderer.sharedMaterial.SetFloat("_Glossiness", (float)b_visual.Smoothness);

        if (!b_visual._textureIsDefault)
        {
          var pngBytes = File.ReadAllBytes(b_visual.AbsoluteTextureFilepath);
          var tex = new Texture2D(1, 1);
          tex.LoadImage(pngBytes);
          renderer.sharedMaterial.SetTexture("_MainTex", tex);
        }
      }

      return go_visual;
    }
  }
}