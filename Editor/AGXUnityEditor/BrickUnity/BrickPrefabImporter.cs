using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AGXUnity.IO;

using AGXUnity.BrickUnity;
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
using AGXUnityEditor.IO;

namespace AGXUnityEditor.BrickUnity
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

    private Dictionary<B_Connector, GameObject> connectorDict;
    private Dictionary<B_Connector, GameObject> implicitConnectorDict; // Implicit connectors that should not by synced
    private Dictionary<B_RigidBody, GameObject> bodyDict;

    private Dictionary<string, Object> shapeMaterials;
    private Dictionary<string, Object> contactMaterials;
    // TODO: Should switch to a dictionary for renderMaterials instead of List as soon as
    // we can guarentee unique names for rendermaterials.
    // private Dictionary<string, Object> renderMaterials;
    private Dictionary<string, Object> frictionModels;


    /// <summary>
    /// Import a Brick file into a prefab GameObject.
    /// </summary>
    /// <param name="filepath">Path to the Brick file.</param>
    /// <param name="modelName">Name of the model in the Brick file.</param>
    /// <returns>A reference to the created GameObject.</returns>
    public GameObject ImportFile(string filepath, string modelName)
    {
      connectorDict = new Dictionary<B_Connector, GameObject>();
      implicitConnectorDict = new Dictionary<B_Connector, GameObject>();
      bodyDict = new Dictionary<B_RigidBody, GameObject>();
      shapeMaterials = new Dictionary<string, Object>();
      contactMaterials = new Dictionary<string, Object>();
      frictionModels = new Dictionary<string, Object>();
      renderMaterials = new List<Object>();

      var b_component = BrickUtils.LoadComponentFromFile(filepath, modelName);
      var b_simulation = new Brick.AgxBrick.BrickSimulation();
      b_simulation.AddComponent(b_component);

      // TODO: if source filepath is within the Assets directory so should we set the rootpath to that.
      RootPath = "Assets";
      Name = b_component._ModelValuePath.Name.Str;
      GetOrCreateDataDirectory();
      // TODO: If the DataDirectory already exists. For all asset types load the existing assets into the appropriate dictionary.
      GetSavedAssets(contactMaterials, RestoredAssetsRoot.ContainingType.ContactMaterial);
      GetSavedAssets(frictionModels, RestoredAssetsRoot.ContainingType.FrictionModel);


      // Creates ShapeMaterials and ContactMaterials
      HandleMaterials(b_component);

      // Handle nodes recursively
      var go_brickComponent = new GameObject(b_component.GetValueNameOrModelPath());
      try
      {
        HandleNode(b_component, ref go_brickComponent);
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


    private void HandleMaterials(B_Component b_component)
    {
      MaterialFactory.CreateShapeMaterials(shapeMaterials, b_component.Materials);
      MaterialFactory.CreateOrUpdateContactMaterials(shapeMaterials, contactMaterials, frictionModels, b_component.ContactMaterials);
    }


    // Recursively create GameObjects with corresponding AGXUnity components from a Brick.Node and its children
    private GameObject HandleNode(
      B_Node b_node,
      ref GameObject go,
      GameObject go_parent = null,
      GameObject go_external = null)
    {
      switch (b_node)
      {
        case B_Geometry b_geometry:
          HandleGeometry(go, b_geometry);
          break;
        case B_RigidBody b_body:
          HandleRigidBody(ref go, go_external, b_body);
          break;
        case B_Visual.Shape b_visualShape:
          HandleVisuals(ref go, b_visualShape);
          break;
        case B_Component b_component:
          HandleComponent(ref go, ref go_external, b_component);
          break;
        default:
          break;
      }

      // Set the transform
      go.SetLocalTransformFromBrick(b_node);

      // worldPositionStays=false makes sure that all the gameobjects are set according to their parent.
      if (go_parent != null)
        go.transform.SetParent(go_parent.transform, false);

      go.AddBrickObject(b_node, go_parent);

      // Save the connectors for later, since we need to initialize all
      // RigidBodies before we can add the connectors
      foreach (var b_connector in b_node._Values.OfType<B_Connector>())
      {
        if (string.IsNullOrEmpty(b_connector.ExternalReference))
        {
          connectorDict.Add(b_connector, go);
        }
        else if (go_external != null)
        {
          // If the Connector is an external, then the constraint with its references are already created. We just
          // have to set the regularization parameters and controllers from the Brick object.
          HandleExternalConnector(go, go_external, b_connector);
        }
        else
        {
          throw new AGXUnity.Exception($"Found Brick connector with external reference {b_connector.ExternalReference} without external component");
        }
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
          HandleNode(b_childNode, ref go_child, go, go_external);
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


    // Add an AGXUnity shape to a GameObject from a Brick.Physics.Geometry object
    private void HandleGeometry(GameObject go, B_Geometry b_geometry)
    {
      if (b_geometry.IsExternal)
        return;
      var au_shape = go.AddShape(b_geometry);
      if (b_geometry.Material != null)
        if (shapeMaterials.ContainsKey(b_geometry.Material.Name))
          au_shape.Material = shapeMaterials[b_geometry.Material.Name] as AGXUnity.ShapeMaterial;
    }


    // Adds an AGXUnity.RigidBody from a Brick.Physics.Mechanics.RigidBody object to a GameObject. If the Brick body
    // has an external reference, the referenced body will have its properties set from the Brick object instead.
    private void HandleRigidBody(ref GameObject go, GameObject go_external, B_RigidBody b_body)
    {
      if (string.IsNullOrEmpty(b_body.ExternalReference))
      {
        go.AddRigidBody(b_body);
      }
      else if (go_external != null)
      {
        HandleExternalRigidBody(ref go, go_external, b_body);
      }
      else
      {
        throw new AGXUnity.Exception($"Got an external reference ({b_body.ExternalReference}) but no external component is present.");
      }
      bodyDict.Add(b_body, go);
    }


    // Set the properties of an AGX RigidBody referenced by a Brick RigidBody object
    private static void HandleExternalRigidBody(ref GameObject go, GameObject go_external, B_RigidBody b_body)
    {
      var extRef = b_body.ExternalReference;

      // Try to find external body by UUID
      bool bodyFound = false;
      bool testUuid(Uuid uuid) => uuid.Str == extRef;
      var c_uuid = go_external.GetComponentsInChildren<Uuid>().SingleOrDefault(testUuid);
      GameObject go_rb_external = null;
      if (c_uuid != default && c_uuid.GetComponent<AGXUnity.RigidBody>() != null)
      {
        go_rb_external = c_uuid.gameObject;
        bodyFound = true;
      }

      // Try to find external body by name
      bool testRb(AGXUnity.RigidBody rb) => rb.name == extRef;
      var c_rb = go_external.GetComponentsInChildren<AGXUnity.RigidBody>().SingleOrDefault(testRb);
      if (c_rb != default)
      {
        go_rb_external = c_rb.gameObject;
        bodyFound = true;
      }

      if (!bodyFound)
        throw new AGXUnity.Exception($"No external body with name or UUID \"{extRef}\" was found.");

      Object.DestroyImmediate(go);
      go = go_rb_external;
    }


    // Handle a connector, i.e. create and configure an AGXUnity Constraint GameObject and add a BrickObject component
    // The "synchronize" argument determines if the connector should be synched with the Brick data tree during runtime
    private GameObject HandleConnector(B_Connector b_connector, GameObject go_parent, bool synchronize)
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
      if (b_body1 == null)
      {
        c_attachmentPair.ReferenceObject = bodyDict[b_body2 as B_RigidBody].gameObject;
        c_attachmentPair.ReferenceFrame.LocalPosition = b_attachment2.LocalPosition.ToHandedVector3();
        c_attachmentPair.ReferenceFrame.LocalRotation = b_attachment2.LocalRotation.ToHandedQuaternion();
        c_attachmentPair.ConnectedObject = null;
        c_attachmentPair.ConnectedFrame.LocalPosition = b_attachment1.LocalPosition.ToHandedVector3();
        c_attachmentPair.ConnectedFrame.LocalRotation = b_attachment1.LocalRotation.ToHandedQuaternion();
      } else
      {
        c_attachmentPair.ReferenceObject = bodyDict[b_body1 as B_RigidBody].gameObject;
        c_attachmentPair.ReferenceFrame.LocalPosition = b_attachment1.LocalPosition.ToHandedVector3();
        c_attachmentPair.ReferenceFrame.LocalRotation = b_attachment1.LocalRotation.ToHandedQuaternion();
        c_attachmentPair.ConnectedObject = b_body2 is null ? null : bodyDict[b_body2 as B_RigidBody].gameObject;
        c_attachmentPair.ConnectedFrame.LocalPosition = b_attachment2.LocalPosition.ToHandedVector3();
        c_attachmentPair.ConnectedFrame.LocalRotation = b_attachment2.LocalRotation.ToHandedQuaternion();
      }

      constraint.SetComplianceAndDamping(b_connector.MainInteraction, overwriteIfDefault: true);
      constraint.SetControllers(b_connector, overwriteIfDefault: true);

      return go_constraint;
    }


    // Set the constraint properties of an AGX Constraint referenced by a Brick connector object
    private void HandleExternalConnector(GameObject go, GameObject go_external, B_Connector b_connector)
    {
      var tf_constraint = go_external.transform.Find(b_connector.ExternalReference);
      var c_constraint = tf_constraint.gameObject.GetComponent<AGXUnity.Constraint>();
      c_constraint.gameObject.name = b_connector.GetValueNameOrModelPath();
      c_constraint.SetComplianceAndDamping(b_connector.MainInteraction, true);
      var c_brickObject = c_constraint.gameObject.AddBrickObject(b_connector, go);
      c_constraint.SetControllers(b_connector, true);
      c_brickObject.synchronize = true;
    }


    // Add a renderer component to a GameObject from a Brick Visual
    private void HandleVisuals(ref GameObject go, B_Visual.Shape b_visualShape)
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
    }


    // Handle a Brick Component. This only does something if the component has an external file path. In that case,
    // read the external file and assign the corresponding GameObject to go_external so that it can be referenced by
    // the Brick Node's children
    private void HandleComponent(ref GameObject go, ref GameObject go_external, B_Component b_component)
    {
      if (b_component._externalFilepathIsDefault)
        return;

      var externalFilepath = b_component.ExternalFilepath;
      var componentFilePath = b_component._ModelValue.File.Filepath;
      var componentFolderPath = Path.GetDirectoryName(componentFilePath);
      var fullPaths = new List<string>
            {
              Path.Combine(componentFolderPath, externalFilepath),
              Path.GetFullPath(externalFilepath),
              Path.Combine(System.Environment.GetEnvironmentVariable("BRICK_DIR"), externalFilepath)
            };

      var fullPath = fullPaths.FirstOrDefault(path => File.Exists(path));
      if (fullPath == default)
      {
        Debug.LogError("Paths searched:\n  " + string.Join("\n  ", fullPaths));
        throw new FileNotFoundException("External file not found", externalFilepath);
      }

      // Copy the AGX file to the Data directory (under Assets), then use AGXUnity to import it
      var ext = Path.GetExtension(fullPath);
      if (ext == ".agx" || ext == ".aagx")
      {
        var filename = Path.GetFileName(fullPath);
        var dataFilepath = Path.Combine(this.DataDirectoryPath, filename);
        GetOrCreateDataDirectory();
        File.Copy(fullPath, dataFilepath, true);
        var oldName = go.name;
        Object.DestroyImmediate(go);
        var agxPrefab = AGXFileImporter.Import(dataFilepath);
        go = Object.Instantiate(agxPrefab);
        go.name = oldName + " (external)";
      }
      else
      {
        throw new AGXUnity.Exception($"Brick could not load external file {fullPath}. Unknown file extension: {ext}");
      }

      go_external = go;
    }


    private DirectoryInfo GetOrCreateDataDirectory()
    {
      if (!AssetDatabase.IsValidFolder(DataDirectoryPath))
        AssetDatabase.CreateFolder(RootPath, Name + "_Data");
      return new DirectoryInfo(DataDirectoryPath);
    }

    private void RefreshAssets()
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
