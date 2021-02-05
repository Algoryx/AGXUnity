using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AGXUnity.IO;

using AGXUnity.BrickUnity.Factories;
using B_Node = Brick.Scene.Node;
using B_Component = Brick.Physics.Component;
using B_RigidBody = Brick.Physics.Mechanics.RigidBody;
using B_Connector = Brick.Physics.Mechanics.AttachmentPairConnector;
using B_MultiConnector = Brick.Physics.Mechanics.MultiAttachmentConnector;
using B_Geometry = Brick.Physics.Geometry;
using B_Visual = Brick.Visual;
using B_RbAttachment = Brick.Physics.Mechanics.RigidBodyAttachment;
using System.Linq;

namespace AGXUnity.BrickUnity
{
  public class BrickPrefabImporter
  {
    private List<Object> renderMaterials;

    public string RootPath { get; private set; }
    public string Name { get; private set; }
    string DataDirectoryPath
    {
      get
      {
        return RootPath + "/" + Name + "_Data";
      }
    }

    private Dictionary<B_RbAttachment, GameObject> attachmentDict;
    private Dictionary<B_Connector, GameObject> connectorDict;
    private Dictionary<B_Connector, GameObject> implicitConnectorDict; // Implicit connectors that should not by synced
    private Dictionary<B_RigidBody, GameObject> bodyDict;

    private Dictionary<string, Object> shapeMaterials;
    private Dictionary<string, Object> contactMaterials;
    // TODO: Should switch to a dictionary for renderMaterials instead of List as soon as
    // we can guarentee unique names for rendermaterials.
    // private Dictionary<string, Object> renderMaterials;
    private Dictionary<string, Object> frictionModels;


    public GameObject ImportFile(string filepath, string modelName)
    {
      attachmentDict = new Dictionary<B_RbAttachment, GameObject>();
      connectorDict = new Dictionary<B_Connector, GameObject>();
      implicitConnectorDict = new Dictionary<B_Connector, GameObject>();
      bodyDict = new Dictionary<B_RigidBody, GameObject>();
      shapeMaterials = new Dictionary<string, Object>();
      contactMaterials = new Dictionary<string, Object>();
      frictionModels = new Dictionary<string, Object>();
      renderMaterials = new List<Object>();

      var b_component = BrickUtils.LoadComponentFromFile(filepath, modelName);
      if (!Brick.Physics.Mechanics.Utils.IsPositioned(b_component))
      {
        var stopAfterDefinedOrder = true;
        Brick.Physics.ComponentLoader.InitializeComponent(b_component, stopAfterDefinedOrder);
      }

      // TODO: if source filepath is within the Assets directory so should we set the rootpath to that.
      RootPath = "Assets";
      Name = b_component._ModelValuePath.Name.Str;
      var dirInfo = GetOrCreateDataDirectory();
      // TODO: If the DataDirectory already exists. For all asset types load the existing assets into the appropriate dictionary.
      GetSavedAssets(contactMaterials, RestoredAssetsRoot.ContainingType.ContactMaterial);
      GetSavedAssets(frictionModels, RestoredAssetsRoot.ContainingType.FrictionModel);


      // Creates ShapeMaterials and ContactMaterials
      HandleMaterials(b_component);

      // Handle nodes recursively
      var go_brickComponent = new GameObject(b_component.GetValueNameOrModelPath());
      try
      {
        HandleNode(b_component, go_brickComponent);
      }
      catch (System.Exception)
      {
        Object.DestroyImmediate(go_brickComponent);
        throw;
      }

      // Handle connectors
      foreach (var connectorGameObjectPair in connectorDict)
      {
        var b_connector = connectorGameObjectPair.Key;
        var go_parent = connectorGameObjectPair.Value;
        HandleConnector(b_connector, go_parent, true);
      }

      // Handle implicit connectors
      foreach (var connectorGameObjectPair in implicitConnectorDict)
      {
        var b_connector = connectorGameObjectPair.Key;
        var go_parent = connectorGameObjectPair.Value;
        HandleConnector(b_connector, go_parent, false);
      }

      var runtimeObject = go_brickComponent.AddComponent<BrickRuntimeComponent>();
      runtimeObject.filePath = filepath;
      runtimeObject.modelName = modelName;

      RefreshAssets();
      // Add contactMaterials to manager after saving
      AGXUnity.ContactMaterialManager contactMaterialManager = AGXUnity.UniqueGameObject<AGXUnity.ContactMaterialManager>.Instance;
      foreach (KeyValuePair<string, Object> entry in contactMaterials)
      {
        contactMaterialManager.Add(entry.Value as AGXUnity.ContactMaterial);
      }

      return go_brickComponent;
    }

    public void HandleMaterials(B_Component b_component)
    {
      MaterialFactory.CreateShapeMaterials(shapeMaterials, b_component.Materials);
      MaterialFactory.CreateOrUpdateContactMaterials(shapeMaterials, contactMaterials, frictionModels, b_component.ContactMaterials);
    }

    public GameObject HandleNode(B_Node b_node, GameObject go, GameObject go_parent = null)
    {
      // Set the transform
      go.SetLocalTransformFromBrick(b_node);

      switch (b_node)
      {
        case B_Geometry b_geometry:
          var au_shape = go.AddShape(b_geometry);
          if (b_geometry.Material != null)
            if (shapeMaterials.ContainsKey(b_geometry.Material.Name))
              au_shape.Material = shapeMaterials[b_geometry.Material.Name] as AGXUnity.ShapeMaterial;
          break;
        case B_RigidBody b_body:
          go.AddRigidBody(b_body);
          bodyDict.Add(b_body, go);
          break;
        case B_RbAttachment b_rbAttachment:
          attachmentDict.Add(b_rbAttachment, go);
          break;
        case B_Visual.Shape b_visualShape:
          go = HandleVisuals(go, b_visualShape);
          break;
        default:
          break;
      }

      // worldPositionStays=false makes sure that all the gameobjects are set according to their parent.
      if (go_parent != null)
        go.transform.SetParent(go_parent.transform, false);

      go.AddBrickObject(b_node, go_parent);

      // Save the connectors for later, since we need to initialize all
      // RigidBodies before we can add the connectors
      foreach (var b_connector in b_node._Values.OfType<B_Connector>())
      {
        connectorDict.Add(b_connector, go);
      }

      // Handle MultiConnectors
      foreach (var b_multiConnector in b_node._Values.OfType<B_MultiConnector>())
      {
        foreach (var b_connector in b_multiConnector.CreatedConnectors)
          implicitConnectorDict.Add(b_connector, go);
      }

      // Handle child nodes
      foreach (var b_childNode in b_node.Children)
      {
        GameObject go_child = new GameObject(b_childNode.GetValueNameOrModelPath());
        // If something goes wrong the created gameobject must be destroyed. Or it will linger in the hierarchy
        try
        {
          HandleNode(b_childNode, go_child, go);
        }
        catch (System.Exception)
        {
          Object.DestroyImmediate(go_child);
          Object.DestroyImmediate(go);
          throw;
        }
      }
      return go;
    }


    // Handle a connector, i.e. create and configure an AGXUnity Constraint GameObject and add a BrickObject component
    // The "synchronize" argument determines if the connector should be synched with the Brick data tree during runtime
    public GameObject HandleConnector(B_Connector b_connector, GameObject go_parent, bool synchronize)
    {
      var go_constraint = AGXUnity.Factory.Create(b_connector.GetAGXUnityConstraintType());
      go_constraint.name = b_connector._ModelValue.Name.Str;
      go_constraint.transform.SetParent(go_parent.transform, false);
      var c_brickObject = go_constraint.AddBrickObject(b_connector, go_parent);
      c_brickObject.synchronize = synchronize;

      var constraint = go_constraint.GetComponent<AGXUnity.Constraint>();

      var b_attachment1 = b_connector.Attachment1;
      var b_attachment2 = b_connector.Attachment2;
      var b_body1 = b_attachment1.Body;
      var b_body2 = b_attachment2.Body;
      var c_attachmentPair = constraint.AttachmentPair;
      c_attachmentPair.ReferenceObject = bodyDict[b_body1 as B_RigidBody];
      c_attachmentPair.ReferenceFrame = new AGXUnity.ConstraintFrame(attachmentDict[b_attachment1]);
      c_attachmentPair.ConnectedObject = b_body2 is null ? null : bodyDict[b_body2 as B_RigidBody];
      c_attachmentPair.ConnectedFrame = new AGXUnity.ConstraintFrame(attachmentDict[b_attachment2]);

      constraint.SetComplianceAndDamping(b_connector.MainInteraction);
      constraint.SetControllers(b_connector);

      return go_constraint;
    }


    public GameObject HandleVisuals(GameObject go, B_Visual.Shape b_visualShape)
    {
      var name = b_visualShape._ModelValuePath.Name.Str;
      Object.DestroyImmediate(go);
      go = VisualFactory.CreateVisual(b_visualShape);
      go.name = name;
      go.SetLocalTransformFromBrick(b_visualShape);

      // Make sure the RenderMaterial is saved
      foreach (var renderer in go.GetComponentsInChildren<MeshRenderer>())
      {
        var material = renderer.sharedMaterial;
        if (material == null)
          continue;
        material.name = b_visualShape.GetValueNameOrModelPath();
        renderMaterials.Add(material);
      }
      return go;
    }


    public DirectoryInfo GetOrCreateDataDirectory()
    {
      if (!AssetDatabase.IsValidFolder(DataDirectoryPath))
        AssetDatabase.CreateFolder(RootPath, Name + "_Data");
      return new DirectoryInfo(DataDirectoryPath);
    }

    public void RefreshAssets()
    {
      createOrUpdateAssets(shapeMaterials, RestoredAssetsRoot.ContainingType.ShapeMaterial);
      createOrUpdateAssets(contactMaterials, RestoredAssetsRoot.ContainingType.ContactMaterial);
      createOrUpdateAssets(renderMaterials, RestoredAssetsRoot.ContainingType.RenderMaterial);
      createOrUpdateAssets(frictionModels, RestoredAssetsRoot.ContainingType.FrictionModel);
      AssetDatabase.SaveAssets();
      AssetDatabase.Refresh();
    }

    private void createOrUpdateAssets(List<Object> objects, RestoredAssetsRoot.ContainingType type)
    {
      var assetName = $"{DataDirectoryPath}/{RestoredAssetsRoot.FindName(Name, type)}.asset";
      var root = AssetDatabase.LoadMainAssetAtPath(assetName);
      if (root == null)
      {
        root = RestoredAssetsRoot.Create(Name, type);
        AssetDatabase.CreateAsset(root, $"{DataDirectoryPath}/{root.name}.asset");
      }

      foreach (var o in objects)
      {
        AssetDatabase.AddObjectToAsset(o, root);
      }
    }

    private void createOrUpdateAssets(Dictionary<string, Object> objects, RestoredAssetsRoot.ContainingType type)
    {
      var assetName = $"{DataDirectoryPath}/{RestoredAssetsRoot.FindName(Name, type)}.asset";
      var root = AssetDatabase.LoadMainAssetAtPath(assetName);
      if (root == null)
      {
        root = RestoredAssetsRoot.Create(Name, type);
        AssetDatabase.CreateAsset(root, $"{DataDirectoryPath}/{root.name}.asset");
      }
      foreach (KeyValuePair<string, Object> entry in objects)
      {
        // Stops us from adding an already loaded asset to the root asset.
        if (AssetDatabase.GetAssetPath(entry.Value) != assetName)
          AssetDatabase.AddObjectToAsset(entry.Value, root);
      }

    }

    private void GetSavedAssets(Dictionary<string, Object> dict, RestoredAssetsRoot.ContainingType type)
    {
      var filepath = $"{DataDirectoryPath}/{RestoredAssetsRoot.FindName(Name, type)}.asset";
      var root = AssetDatabase.LoadAssetAtPath(filepath, typeof(RestoredAssetsRoot));
      foreach (var o in AssetDatabase.LoadAllAssetRepresentationsAtPath(filepath))
      {
        dict.Add(o.name, o);
      }
    }
  }
}