using AGXUnity;
using AGXUnity.Utils;
using agxUtil;
using PlasticGui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  /// <summary>
  /// A tool for transforming a unity mesh into a AGX cable route. Based on "Sphere-Mesh skeletonisation" by Thiery et. al.
  /// </summary>
  /// <typeparam name="NodeT">The type of node used in the attached route</typeparam>
  public class RouteFromMeshTool<NodeT> : Tool where NodeT : RouteNode, new()
  {
    /// <summary>
    /// The threshhold at which the tool warns about computation times and automatically disables preview
    /// </summary>
    const int VERTEX_WARNING_THRESHOLD = 100_000;
    /// <summary>
    /// The amount of dsitance in radii which makes the tool consider a created node to be the same conceptual node in a previous route.
    /// </summary>
    const float NODE_TRANSFER_DISTANCE_THRESHOLD = 2f; //2 radii

    /// <summary>
    /// The currently selected parent for the route/skeleton
    /// </summary>
    private GameObject m_selectedParent = null;     
    /// <summary>
    /// The currently cached skeleton. Is updated on skeletonisation or modifications to the structure of the skeleton
    /// </summary>
    private SphereSkeleton m_skeleton = null;
    /// <summary>
    /// The average radius of the displayed skeleton.
    /// </summary>
    private double m_skeletonAvgRadius = 0;

    private float m_aggressiveness = 0;
    /// <summary>
    /// A measure of how aggressive the skeletonisation process should be. Is used to calculate the "face devalue factor" by using it as an exponent.
    /// </summary>
    public float Aggressiveness
    {
      get {  return m_aggressiveness; } 
      set 
      {
        if(m_aggressiveness != value)
        {
          m_aggressiveness = value;
          SkeletoniseSelectedMesh();
        }
      }
    }    
    /// <summary>
    /// Used to warn the user about lost changes.
    /// </summary>
    private bool m_userHasEdited = false;
    public bool UserHasEdited
    {
      get { return m_userHasEdited; }
      set 
      { 
        if(!m_userHasEdited && value)
        {
          InspectorEditor.RequestConstantRepaint = true;
        }
        m_userHasEdited = value;
      }
    }
    
    /// <summary>
    /// The current agx skeletoniser which holds the mesh and carries out the skeletonisation process
    /// </summary>
    private SphereSkeletoniser m_skeletoniser = null;
    
    /// <summary>
    /// The Route object to which the generated route will be written
    /// </summary>
    public Route<NodeT> Route { get; private set; }

    private bool m_showPreview = true;
    /// <summary>
    /// If the preivew of the skeleton/route should be visible
    /// </summary>
    public bool Preview
    {
      get { return m_showPreview; }
      set
      {
        if (!m_showPreview && value && m_skeleton == null)
        {
          SkeletoniseSelectedMesh();
          DisplayVertexCountWarning = false;
        }
        m_showPreview = value;
      }
    }

    /// <summary>
    /// Constructs the localToWorld matrix for the skeleton/route preview
    /// </summary>
    /// <returns>The matrix transforming the joints to world space</returns>
    private Matrix4x4 SkeletonToWorld()
    {
      if (m_selectedParent != null)
        return Matrix4x4.TRS(m_selectedParent.transform.position, m_selectedParent.transform.rotation, Vector3.one);
      else
        return Matrix4x4.identity;
    }

    private Mesh m_selectedMesh = null;
    /// <summary>
    /// The currently selected mesh from which to base the route
    /// </summary>
    public Mesh SelectedMesh
    {
      get { return m_selectedMesh; }
      set
      {
        if (m_selectedMesh != value)
        {
          DisplayVertexCountWarning = false;
          m_selectedMesh = value;

          if (m_selectedMesh == null)
          {
            m_skeleton = null;
            m_skeletoniser = null;
            return;
          }            

          //Warning if mesh is very large        
          if (Preview && m_selectedMesh.vertexCount > VERTEX_WARNING_THRESHOLD)
          {
            Preview = false;
            DisplayVertexCountWarning = true;
          }
          if (Preview)
          {
            SkeletoniseSelectedMesh();
          }
        }
      }
    }

    /// <summary>
    /// Toggle for fetching only the longest continous path in the skeletoniser instead of the entire skeleton
    /// </summary>
    private bool m_useLongestContinuousPathInSkeleton = true;
    public bool UseLongestPath
    {
      get { return m_useLongestContinuousPathInSkeleton; }
      set
      {
        UpdateSkeleton();
        m_useLongestContinuousPathInSkeleton = value;
      }
    }
    /// <summary>
    /// The exposed manually set radius used for the cable after application and used for the preview of the skeleton/route
    /// </summary>
    private float m_fixedRadius = 0;
    private bool m_useFixedRadius = false;
    public bool UseFixedRadius
    {
      get { return m_useFixedRadius; }
      set
      {
        if (!value && m_useFixedRadius && m_skeleton != null)
        {
          m_fixedRadius = (float)m_skeletonAvgRadius;
        }
        m_useFixedRadius = value;
      }
    }

    /// <summary>
    /// Toggles the visibility of the vertex count warning
    /// </summary>
    public bool DisplayVertexCountWarning { get; set; } = false;

    /// <summary>
    /// Property for handling the usage of the select game object tool
    /// </summary>
    public bool SelectGameObjectTool
    {
      get { return GetChild<SelectGameObjectTool>() != null; }
      set
      {
        if (value && !SelectGameObjectTool)
        {
          RemoveAllChildren();

          var selectGameObjectTool = new SelectGameObjectTool()
          {
            OnSelect = go =>
            {
              HandleSelectedObject(go);
              SelectGameObjectTool = false;
            }
          };

          AddChild(selectGameObjectTool);
        }
        else if (!value)
          RemoveChild(GetChild<SelectGameObjectTool>());
      }
    }

    /// <summary>
    /// Constructor which sets the route object and attempts to set default values to this scripts parent and a mesh on the same object
    /// </summary>
    /// <param name="parentRoute"></param>
    public RouteFromMeshTool(Route<NodeT> parentRoute)
      : base(isSingleInstanceTool: true)
    {
      Route = parentRoute;
      var cableObject = Route.gameObject.transform.parent == null ? Route.gameObject : Route.gameObject.transform.parent.gameObject;
      HandleSelectedObject(cableObject);
    }

    /// <summary>
    /// Draws the route/skeleton preview if possible
    /// </summary>
    /// <param name="sceneView"></param>
    public override void OnSceneViewGUI(SceneView sceneView)
    {
      if (Preview && m_skeleton != null)
      {
        if (Event.current.alt && m_skeletoniser != null) DrawSkeleton();
        else DrawRoutePreview(sceneView);
      }
    }

    private NodeT CreateNode(Vector3 localPosition, Quaternion localRotation)
    {
      NodeT node = IFrame.Create<NodeT>(m_selectedParent, localPosition, localRotation);
      if (Route is WireRoute)
        (node as WireRouteNode).Type = Wire.NodeType.FreeNode;
      else if (Route is CableRoute)
        (node as CableRouteNode).Type = Cable.NodeType.FreeNode;
      return node;
    }

    private string AwaitingUserActionDotsWithPadding()
    {
      string noPadding = AwaitingUserActionDots();
      return noPadding + new string(' ', 3 - noPadding.Length);
    }

    /// <summary>
    /// Draws the editor and performs any skeletonisation actions when needed.
    /// </summary>
    public void OnInspectorGUI()
    {
      InspectorEditor.RequestConstantRepaint = false;
      InspectorGUI.OnDropdownToolBegin("Create node route using visual mesh");

      var skin = InspectorEditor.Skin;
      var emptyContent = GUI.MakeLabel(" ");

      if (DisplayVertexCountWarning)
      {
        InspectorGUI.WarningLabel("Route preview has been disabled due to high vertex count of selected mesh. Enabling preview will likely result in long preview times.");
      }

      if (GUILayout.Button(SelectGameObjectTool ? "Waiting for selection" + AwaitingUserActionDotsWithPadding() : "Select object in scene", GUILayout.ExpandWidth(false)))
      {
        SelectGameObjectTool = true;
      }


      EditorGUILayout.LabelField(GUI.MakeLabel("Generation settings:", toolTip: "Settings used in the generation of the initial route. Changing these will require a regeneration of the route."));      
      InspectorEditor.RequestConstantRepaint = SelectGameObjectTool;

      EditorGUI.BeginDisabledGroup(SelectGameObjectTool);
      EditorGUILayout.BeginHorizontal();
      if(!UserHasEdited || SelectGameObjectTool)
        EditorGUILayout.PrefixLabel(GUI.MakeLabel("Base mesh", toolTip: "The mesh which is used for generating the initial route. Changing this value regenerates the skeleton, removing any adjustments made."));
      else        
        EditorGUILayout.PrefixLabel(GUI.MakeLabel("Base mesh", Color.yellow, toolTip: "The mesh which is used for generating the initial route. Changing this value regenerates the skeleton, removing any adjustments made."));

      SelectedMesh = (Mesh)EditorGUILayout.ObjectField(SelectedMesh, typeof(Mesh), true);
      EditorGUILayout.EndHorizontal();

      if (!UserHasEdited || SelectGameObjectTool)
        Aggressiveness = EditorGUILayout.DelayedFloatField(GUI.MakeLabel("Aggressiveness", toolTip: "The amount of priority given to achieving a skeleton structure during the algorithms run. Higher values tend to produce more detailed skeletons but possibly introduce artifacts. Changing this value regenerates the skeleton, removing any adjustments made."), Aggressiveness);
      else
        Aggressiveness = EditorGUILayout.DelayedFloatField(GUI.MakeLabel("Aggressiveness", Color.yellow, toolTip: "The amount of priority given to achieving a skeleton structure during the algorithms run. Higher values tend to produce more detailed skeletons but possibly introduce artifacts. Changing this value regenerates the skeleton, removing any adjustments made."), Aggressiveness);

      EditorGUILayout.BeginHorizontal();
      if (!UserHasEdited || SelectGameObjectTool)
        EditorGUILayout.PrefixLabel(GUI.MakeLabel("Parent", toolTip: "The game object which the routes transform is inherited from. This object will also be the parent of the route nodes unless previous node settings are transferred. Changing this value regenerates the skeleton, removing any adjustments made."));
      else
        EditorGUILayout.PrefixLabel(GUI.MakeLabel("Parent", Color.yellow, toolTip: "The game object which the routes transform is inherited from. This object will also be the parent of the route nodes unless previous node settings are transferred. Changing this value regenerates the skeleton, removing any adjustments made."));

      m_selectedParent = (GameObject)EditorGUILayout.ObjectField(m_selectedParent, typeof(GameObject), true);
      EditorGUILayout.EndHorizontal();

      //Settings which do not affect skeletonisation ----------------------------------------------------
      UseLongestPath = InspectorGUI.Toggle(GUI.MakeLabel("Longest Continuous Path", toolTip: "Use the longest continuous path from the produced skeleton instead of the complete skeleton. Useful for omitting larger details in the mesh from the generated route or for reducing artifacts."), UseLongestPath);
      if (GUILayout.Button("Regenerate", GUILayout.ExpandWidth(false)))
      {
        SkeletoniseSelectedMesh();
      }

      InspectorGUI.Separator();
      Preview = EditorGUILayout.BeginToggleGroup(GUI.MakeLabel("Preview/Edit", toolTip: "Preview the route which will be created."), Preview);
      if (Preview)
      {
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(GUI.MakeLabel("Shift: Remove joints"));
        EditorGUILayout.LabelField(GUI.MakeLabel("Control: Add joints"));
        EditorGUILayout.LabelField(GUI.MakeLabel("Alt: Show underlying skeleton"));
        EditorGUILayout.EndVertical();    
      }
      EditorGUILayout.BeginHorizontal();
      EditorGUILayout.LabelField(GUI.MakeLabel("Number of Nodes:"), GUILayout.ExpandWidth(false));
      EditorGUILayout.LabelField(Preview ? GUI.MakeLabel(m_skeleton?.joints.Count.ToString()) : GUI.MakeLabel("-"), GUILayout.ExpandWidth(false));
      EditorGUILayout.EndHorizontal();

      UseFixedRadius = EditorGUILayout.BeginToggleGroup("Use Fixed Radius", UseFixedRadius);      
      m_fixedRadius = Mathf.Clamp(EditorGUILayout.FloatField("Radius", m_fixedRadius), 0, 5);      
      EditorGUILayout.EndToggleGroup();
      EditorGUILayout.EndToggleGroup();      

      var applyCancelState = InspectorGUI.PositiveNegativeButtons(m_skeleton != null && m_skeleton?.joints.Count > 0,
                                                                   "Apply",
                                                                   "Apply current configuration.",
                                                                   "Cancel");
      EditorGUI.EndDisabledGroup();

      if (applyCancelState == InspectorGUI.PositiveNegativeResult.Positive)
      {
        if (Route.Any()) //If there already exists a route on the cable
        {
          switch (EditorUtility.DisplayDialogComplex("Overwrite", "Applying will overwrite the current route and any node settings. Would you like to attempt transfer node settings to the new route? This process is not guaranteed to transfer correctly, especially when the number of nodes will differ.", "Apply", "Cancel", "Apply w/current node settings"))
          {
            case 0: //OK
              CreateRoute(false);
              PerformRemoveFromParent();
              break;
            case 1: //Cancel
              break;
            case 2: //Apply with settings
              CreateRoute(true);
              PerformRemoveFromParent();
              break;
            default: break;
          }
          GUIUtility.ExitGUI(); //This is needed to avoid errors with unfinished/falsly started groups after dialog
        }
        else
        {
          CreateRoute(false);
          PerformRemoveFromParent();
        }
      }
      else if (applyCancelState == InspectorGUI.PositiveNegativeResult.Negative)
        PerformRemoveFromParent();

      InspectorGUI.OnDropdownToolEnd();
    }    

    /// <summary>
    /// Uses the current route/skeleton to create nodes in the parent route object
    /// </summary>
    /// <param name="applyCurrentNodeSettings">If an attempt should be made to translate the current nodes settings (type and parent) to the created nodes</param>
    private void CreateRoute(bool applyCurrentNodeSettings)
    {
      Undo.SetCurrentGroupName($"Generating route from mesh \"{SelectedMesh.name}\"");
      var undoGroupId = Undo.GetCurrentGroup();

      if (!Preview && m_skeleton == null)
        SkeletoniseSelectedMesh();

      float avgRadius = 0;

      var nodeList = new List<NodeT>();
      SphereSkeleton.dfs_iterator currJoint = m_skeleton.begin();

      //Iterate and create nodes
      while (!currJoint.isEnd())
      {
        avgRadius += (float)currJoint.radius;
        Quaternion localRotation;
        if (currJoint.adjJoints.Count != 2 && nodeList.Count > 0)
        {
          localRotation = nodeList.Last().LocalRotation;
        }
        else
        {
          localRotation = Quaternion.FromToRotation(Vector3.forward, (currJoint.next_joint().position - currJoint.position).ToHandedVector3());
        }
        nodeList.Add(CreateNode(currJoint.position.ToHandedVector3(), localRotation));
        currJoint.inc();
      }

      if (UseFixedRadius)
        avgRadius = m_fixedRadius;
      else
        avgRadius /= nodeList.Count;

      //If a route already exists then we will attempt to transfer parents and node types to the best of our ability
      if (Route.Any() && applyCurrentNodeSettings)
      {
        //Starting with the ends of the cable and trying to match them to our best ability
        NodeT[] oldEnds = { Route.First(), Route.Last() }, newEnds = { nodeList.First(), nodeList.Last() };
        //If one pair of new and old nodes match very closely then we assume they should still be matched
        float minDist = float.PositiveInfinity;
        int minOldEnd = -1, minNewEnd = -1;
        foreach (var oldEnd in oldEnds.Select((v, i) => new Tuple<NodeT, int>(v, i)))
        {
          foreach (var newEnd in newEnds.Select((v, i) => new Tuple<NodeT, int>(v, i)))
          {
            var dist = (oldEnd.Item1.Position - newEnd.Item1.Position).magnitude;
            if (dist < minDist)
            {
              minOldEnd = oldEnd.Item2;
              minNewEnd = newEnd.Item2;
              minDist = dist;
            }
          }
        }

        if (minDist < NODE_TRANSFER_DISTANCE_THRESHOLD * avgRadius) //If one pair is close enough to assume they want to be considered the same by the user. The other pair is then implied to be a match as well
        {
          TransferNodeSettings(oldEnds[minOldEnd], newEnds[minNewEnd]);
          TransferNodeSettings(oldEnds[(minOldEnd + 1) % 2], newEnds[(minNewEnd + 1) % 2]);
        }
        else
        {
          TransferNodeSettings(oldEnds[0], newEnds[0]);
          TransferNodeSettings(oldEnds[1], newEnds[1]);
        }

        //Base the transfer on the route which has the least nodes. Each leftover node in the route with more nodes is either discarded or created using normal values.
        bool baseOnExistingRoute = nodeList.Count >= Route.NumNodes;

        IEnumerable<NodeT> from, to;
        List<NodeT> used = new();
        from = baseOnExistingRoute ? Route : nodeList;
        to = baseOnExistingRoute ? nodeList : Route;

        foreach (var node in from.Skip(1).Take(Route.NumNodes - 2)) //Skip the ends as they were handled earlier
        {
          //Find the closest node in the node list and transfer the settings
          var closestNode = to.Where(n => !used.Contains(n)).Aggregate(node, (closest, next) => (next.Position - node.Position).magnitude < (closest.Position - node.Position).magnitude ? next : closest);
          used.Add(closestNode);
          if (baseOnExistingRoute)
            TransferNodeSettings(node, closestNode);
          else
            TransferNodeSettings(closestNode, node);
        }
      }

      Route.Clear();
      foreach (var node in nodeList)
      {
        Route.Add(node);
      }

      if (Route is WireRoute)
      {
        Route.gameObject.GetComponent<Wire>().Radius = avgRadius;
        Route.gameObject.GetComponent<Wire>().ResolutionPerUnitLength = 0.5f / avgRadius;
      }
      else if (Route is CableRoute)
      {
        Route.gameObject.GetComponent<Cable>().Radius = avgRadius;
        Route.gameObject.GetComponent<Cable>().ResolutionPerUnitLength = 0.5f / avgRadius;
      }

      //Disable the renderer to signify the conversion
      MeshRenderer renderer;
      if (m_selectedParent != null && m_selectedParent.TryGetComponent(out renderer))
      {
        Undo.RegisterCompleteObjectUndo(renderer, "Disable mesh renderer");
        renderer.enabled = false;
      }

      Undo.CollapseUndoOperations(undoGroupId);
    }

    private bool TransferNodeSettings(NodeT from, NodeT to)
    {
      to.SetParent(from.Parent);
      if (Route is WireRoute)
        (to as WireRouteNode).Type = (from as WireRouteNode).Type;
      else if (Route is CableRoute)
        (to as CableRouteNode).Type = (from as CableRouteNode).Type;
      else
        return false;
      return true;
    }

    /// <summary>
    /// Re-fetch the skeleton from the skeletoniser and re-calculate the average radius
    /// </summary>
    private void UpdateSkeleton()
    {
      if (m_skeletoniser != null)
      {
        m_skeleton = UseLongestPath ? agxUtil.agxUtilSWIG.getLongestContinuousSkeletonSegment(m_skeletoniser.getSkeleton()) : m_skeletoniser.getSkeleton();
        m_skeletonAvgRadius = 0;
        foreach (var joint in m_skeleton.joints)
        {
          m_skeletonAvgRadius += joint.radius;
        }
        m_skeletonAvgRadius /= m_skeleton.joints.Count;
        if (!UseFixedRadius)
        {
          m_fixedRadius = (float)m_skeletonAvgRadius;
        }
      }
    }

    /// <summary>
    /// Attempt use an object as the source of the conversion. Sets the given object as parent and attempts to find a mesh filter in the objects hierarchy.
    /// </summary>
    /// <param name="selected">The object to use</param>
    /// <returns>True if both mesh and game object was set. False otherwise</returns>
    private bool HandleSelectedObject(GameObject selected)
    {
      m_selectedParent = selected;
      if (!selected)
        return false;
      DisplayVertexCountWarning = false;

      MeshFilter foundFilter;

      if (!selected.TryGetComponent<MeshFilter>(out foundFilter))
      {
        selected.TraverseChildren(delegate (GameObject child)
        {
          if (child.TryGetComponent<MeshFilter>(out foundFilter))
          {
            selected = child;
          }
        });
      }

      if (selected.TryGetComponent<MeshFilter>(out foundFilter))
      {
        var mesh = foundFilter.sharedMesh;
        if (mesh == null)
        {
          return false;
        }
        //Warning if mesh is very large        
        if (Preview && mesh.vertexCount > VERTEX_WARNING_THRESHOLD)
        {
          Preview = false;
          DisplayVertexCountWarning = true;
        }
        SelectedMesh = mesh;
        m_skeletoniser = null;
        m_skeleton = null;
        if (Preview)
        {
          SkeletoniseSelectedMesh();
        }
        return true;
      }
      return false;
    }

    /// <summary>
    /// Skeletonise the currently selected mesh
    /// </summary>
    private void SkeletoniseSelectedMesh()
    {
      if(SelectedMesh == null)
        return;

      UserHasEdited = false;
      GameObject temp = new();
      temp.transform.localScale = m_selectedParent != null ? m_selectedParent.transform.lossyScale : Vector3.one;
      var mergedSelectedMesh = MeshMerger.Merge(temp.transform, new Mesh[] { SelectedMesh });
      UnityEngine.Object.DestroyImmediate(temp);
      m_skeletoniser = new SphereSkeletoniser(mergedSelectedMesh.Vertices, mergedSelectedMesh.Indices);
      m_skeletoniser.setFaceDevalueFactor(Mathf.Pow(10, Aggressiveness));
      m_skeletoniser.collapseUntilSkeleton();

      UpdateSkeleton();
    }

    void LineHandleCap(int controlID, Vector3 position, Quaternion rotation, float thickness, float length, EventType eventType)
    {
      Vector3 scale = new Vector3(thickness, thickness, length);
      Matrix4x4 originalMatrix = Handles.matrix;
      Handles.matrix = Handles.matrix * Matrix4x4.TRS(position, rotation, scale);
      Handles.CylinderHandleCap(controlID, Vector3.zero, Quaternion.identity, 1.0f, eventType);
      Handles.matrix = originalMatrix;
    }

    /// <summary>
    /// Draw the current route/skeleton using handles
    /// </summary>
    /// <param name="sceneView">The scene view that is currently being viewed. Used to determine the camera's position.</param>
    private void DrawRoutePreview(SceneView sceneView)
    {
      //Drawing without depth makes selection hard so a custom z-pass is used by simply sorting the draw calls in this function by distance form the camera and otherwise always passing the zTest
      var drawcalls = new List<Tuple<float, Action, Color>>(); //Make sure to provide proper capture for each drawcall!
      Event e = Event.current;
      Color normalColor = Route.Any() ? Color.yellow : Color.blue, removeColor = Color.red, addColor = Color.green, edgeColor = normalColor;
      Matrix4x4 skeletonToWorld = SkeletonToWorld();
      Vector3 cameraPosRouteSpace = skeletonToWorld.inverse.MultiplyPoint(sceneView.camera.transform.position);
      using (new Handles.DrawingScope(skeletonToWorld))
      {
        float handleRadius = UseFixedRadius ? m_fixedRadius : (float)m_skeletonAvgRadius;
        float handleDiameter = handleRadius * 2;
        SphereSkeleton.dfs_iterator currDFSJoint = m_skeleton.begin();
        SphereSkeleton.Joint prevDFSJoint = null;
        while (!currDFSJoint.isEnd())
        {
          float distanceToCamera;
          Vector3 currPos = currDFSJoint.position.ToHandedVector3();

          uint currJointSkeletoniserIdx = currDFSJoint.skeletoniserIndex;
          //Draw edge and potentially upscale button for edge
          if (prevDFSJoint != null)
          {
            Vector3 prevPos = prevDFSJoint.position.ToHandedVector3();
            uint prevJointSkeletoniserIdx = prevDFSJoint.skeletoniserIndex;
            //Make sure edge buttons always draw infront of the edge
            distanceToCamera = Mathf.Max((cameraPosRouteSpace - currPos).magnitude, (cameraPosRouteSpace - prevPos).magnitude);
            drawcalls.Add(new Tuple<float, Action, Color>(distanceToCamera, () => Handles.DrawLine(currPos, prevPos, 3), edgeColor));

            if (e.control && m_skeletoniser.isUpscalePossible(prevJointSkeletoniserIdx, currJointSkeletoniserIdx))
            {
              Vector3 facingDirection = currPos - prevPos;              
              Vector3 midPoint = prevPos + facingDirection * 0.5f;
              //Vector3 directionToCamera = cameraPosRouteSpace - midPoint;
              var edgeCubeColor = addColor * 0.5f;
              edgeCubeColor.a = 1;
              //Reduce the size of the cube when two joints are very close
              float size = Mathf.Clamp(facingDirection.magnitude - 2 * handleRadius, handleRadius / 2, handleRadius);                            

              distanceToCamera = (cameraPosRouteSpace - midPoint).magnitude;
              drawcalls.Add(new Tuple<float, Action, Color>(distanceToCamera, () =>
              {
                if (Handles.Button(midPoint, Quaternion.LookRotation(facingDirection), size, size, Handles.CubeHandleCap))
                {
                  m_skeletoniser.upscaleEdge(prevJointSkeletoniserIdx, currJointSkeletoniserIdx);
                  UserHasEdited = true;
                  UpdateSkeleton();
                }
              }, edgeCubeColor));

            }
          }
          
          distanceToCamera = (cameraPosRouteSpace - currPos).magnitude - handleRadius;
          //Draw joints normally if no buttons are held or if the current node is ineligible for the current mode
          if ((!e.shift && !e.control) || /*Ineligible for upscale*/ (e.control && (!m_skeletoniser.isUpscalePossible(currJointSkeletoniserIdx) || currDFSJoint.isLeaf())) || /*Cannot remove past 2 joints*/ (e.shift && m_skeleton.joints.Count <= 2))
          {
            drawcalls.Add(new Tuple<float, Action, Color>(distanceToCamera, () => Handles.SphereHandleCap(0, currPos, Quaternion.identity, handleDiameter, EventType.Repaint), normalColor));
          }
          else
          {
            uint neighbSkeletoniserIdx1 = 0;
            uint neighbSkeletoniserIdx2 = 0;

            if (e.control)
            {
              //Prepare edges for upscale call
              var closestJoints = currDFSJoint.adjJoints.Select(idx => m_skeleton.joints[(int)idx]).OrderBy(j => j.position.distance(currDFSJoint.position)).Take(2).ToList();
              neighbSkeletoniserIdx1 = closestJoints[0].skeletoniserIndex;
              neighbSkeletoniserIdx2 = closestJoints[1].skeletoniserIndex;
            }

            drawcalls.Add(new Tuple<float, Action, Color>(distanceToCamera, () =>
            {
              if (Handles.Button(currPos, Quaternion.identity, handleDiameter, handleRadius, Handles.SphereHandleCap))
              {
                if (e.shift && m_skeleton.joints.Count > 2) //Remove joint
                {
                  //Remove corresponding vertex from skeletoniser and update skeleton
                  m_skeletoniser.removeVertex(currJointSkeletoniserIdx, false);
                  UserHasEdited = true;
                  UpdateSkeleton();
                }
                else if (e.control) //Upscale at joint
                {
                  if (m_skeletoniser.upscaleVertex(currJointSkeletoniserIdx, neighbSkeletoniserIdx1, neighbSkeletoniserIdx2))
                  {
                    UserHasEdited = true;
                    UpdateSkeleton();
                  }

                }
              }
            }, e.control ? addColor : removeColor));
          }

          prevDFSJoint = currDFSJoint.deref();          
          currDFSJoint.inc();
        }

        //Sort in descending order to draw the furthest objects first
        drawcalls.Sort((t1, t2) => -t1.Item1.CompareTo(t2.Item1));

        foreach (var drawcall in drawcalls)
        {
          using (new Handles.DrawingScope(drawcall.Item3))
          {
            drawcall.Item2.Invoke();
          }
        }
      }
    }

    /// <summary>
    /// Draws the current skeleton without a specific traversal order.
    /// </summary>
    void DrawSkeleton()
    {
      using (new Handles.DrawingScope(SkeletonToWorld()))
      {
        using (new Handles.DrawingScope(Color.green))
        {
          var skel = m_skeletoniser.getSkeleton();
          foreach (var joint in skel.joints)
          {
            Handles.SphereHandleCap(0, joint.position.ToHandedVector3(), Quaternion.identity, (float)joint.radius * 2, EventType.Repaint);
            using (new Handles.DrawingScope(Color.red))
            {
              foreach (var adjIdx in joint.adjJoints)
              {
                Handles.DrawLine(skel.joints[(int)adjIdx].position.ToHandedVector3(), joint.position.ToHandedVector3(), 4);
              }
            }
          }
        }
      }
    }
  }
}
