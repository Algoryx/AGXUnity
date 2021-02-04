using System.Collections.Generic;
using System;
using UnityEngine;

using B_Node = Brick.Scene.Node;
using B_Material = Brick.Physics.Material;
using B_ContactMaterial = Brick.Physics.ContactMaterial;

namespace BrickUnity.Factories
{
  public static class MaterialFactory
  {
    public static void CreateShapeMaterials(Dictionary<string, UnityEngine.Object> materialDict, List<B_Material> b_mats)
    {
      foreach (var b_m in b_mats)
      {
        if (!materialDict.ContainsKey(b_m.Name))
        {
          var mat = CreateShapeMaterial(b_m);
          materialDict.Add(b_m.Name, mat);
        }
      }
    }

    public static AGXUnity.ShapeMaterial CreateShapeMaterial(B_Material b_m)
    {
      var au_mat = ScriptableObject.CreateInstance<AGXUnity.ShapeMaterial>();
      au_mat.name = b_m.Name;
      au_mat.Density = (float)b_m.Bulk.Density;
      // AGXUnity ShapeMaterial do not have Viscosity or YoungsModulus.
      // Or SurfaceMaterials
      // And Brick.Physics.Material do not have wire YoungsModulus.
      //au_mat.YoungsWireBend = (float)b_m.Bulk.YoungsModulus;
      //au_mat.YoungsWireStretch = (float)b_m.Bulk.YoungsModulus;
      return au_mat;
    }

    public static void CreateOrUpdateContactMaterials(Dictionary<string, UnityEngine.Object> shapeMaterials,
                                                        Dictionary<string, UnityEngine.Object> contactMaterials,
                                                        Dictionary<string, UnityEngine.Object> frictonModels,
                                                        List<B_ContactMaterial> b_cms)
    {
      foreach (var b_cm in b_cms)
      {
        AGXUnity.ContactMaterial au_cm = null;
        AGXUnity.FrictionModel au_fm = null;
        if (contactMaterials.ContainsKey(b_cm.Name))
        {
          au_cm = contactMaterials[b_cm.Name] as AGXUnity.ContactMaterial;
          if (frictonModels.ContainsKey($"fm_{au_cm.name}"))
            au_fm = frictonModels[$"fm_{au_cm.name}"] as AGXUnity.FrictionModel;
        }
        else
        {
          au_cm = ScriptableObject.CreateInstance<AGXUnity.ContactMaterial>();
          contactMaterials.Add(b_cm.Name, au_cm);
        }

        RestoreContactMaterial(au_cm,
                                au_fm,
                                shapeMaterials[b_cm.Material1.Name] as AGXUnity.ShapeMaterial,
                                shapeMaterials[b_cm.Material2.Name] as AGXUnity.ShapeMaterial,
                                b_cm);

        if (!frictonModels.ContainsKey($"fm_{au_cm.name}"))
          frictonModels.Add(au_cm.FrictionModel.name, au_cm.FrictionModel);
      }
    }

    private static void RestoreContactMaterial(AGXUnity.ContactMaterial au_cm,
                                                AGXUnity.FrictionModel au_fm,
                                                AGXUnity.ShapeMaterial au_mat1,
                                                AGXUnity.ShapeMaterial au_mat2,
                                                B_ContactMaterial b_cm)
    {
      au_cm.name = b_cm.Name;
      au_cm.Material1 = au_mat1;
      au_cm.Material2 = au_mat2;

      au_cm.YoungsModulus = Convert.ToSingle(b_cm.YoungsModulus);
      au_cm.Restitution = Convert.ToSingle(b_cm.Restitution);
      au_cm.Damping = Convert.ToSingle((b_cm.Damping / b_cm.YoungsModulus));
      au_cm.SurfaceViscosity = new Vector2(Convert.ToSingle(b_cm.SurfaceViscosity),
                                            Convert.ToSingle(b_cm.SurfaceViscosity));
      au_cm.FrictionCoefficients = new Vector2(Convert.ToSingle(b_cm.PrimaryFrictionCoefficient),
                                                Convert.ToSingle(b_cm.SecondaryFrictionCoefficient));
      au_cm.AdhesiveForce = Convert.ToSingle(b_cm.AdhesiveForce);
      au_cm.AdhesiveOverlap = Convert.ToSingle(b_cm.AdhesiveOverlap);

      // TODO: Should one be able to model FrictionModels in brick?
      // And then be able to add the same frictionModel to multiple contactMaterials.
      au_fm = RestoreFrictionModel(au_fm, b_cm.FrictionReferenceNode);
      au_fm.name = $"fm_{au_cm.name}";
      au_cm.FrictionModel = au_fm;

      // au_cm.UseContactArea = b_cm.
      // au_cm.ContactReductionLevel = b_cm.Contact
      // au_cm.ContactReductionLevel = b_cm.Con
    }

    private static AGXUnity.FrictionModel RestoreFrictionModel(AGXUnity.FrictionModel fm, B_Node b_frictionReferenceNode)
    {
      if (fm == null)
        fm = ScriptableObject.CreateInstance<AGXUnity.FrictionModel>();
      if (b_frictionReferenceNode == null)
      {
        fm.Type = AGXUnity.FrictionModel.EType.ScaleBoxFriction;
        fm.SolveType = AGXUnity.FrictionModel.ESolveType.Direct;
      }
      else
      {
        fm.Type = AGXUnity.FrictionModel.EType.BoxFriction;
        fm.SolveType = AGXUnity.FrictionModel.ESolveType.Direct;
      }
      return fm;
    }
  }
}
