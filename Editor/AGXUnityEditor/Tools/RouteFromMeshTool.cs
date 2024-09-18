using AGXUnity;
using AGXUnity.Utils;
using agxUtil;
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
  public class RouteFromMeshTool<ParentT, NodeT> : Tool 
    where ParentT : ScriptComponent
    where NodeT : RouteNode, 
    new()
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
    private SphereSkeleton Skeleton
    {
      get
      {
        if (UseLongestPath)
          return m_longestSkeleton;
        return m_skeleton;
      }
      set
      {
        if (m_skeleton != value)
        {
          m_skeleton = value;
          m_longestSkeleton = value == null ? null : agxUtilSWIG.getLongestContinuousSkeletonSegment(m_skeleton);         
        }
      }
    }
    private SphereSkeleton m_skeleton = null;
    /// <summary>
    /// The longest path in the current skeleton. Is updated on skeletonisation or modifications to the structure of the skeleton
    /// </summary>
    private SphereSkeleton m_longestSkeleton = null;
    /// <summary>
    /// The average radius of the displayed skeleton.
    /// </summary>
    public float SkeletonRadius()
    {
      return (float)(UseLongestPath ? m_longestSkeletonAvgRadius : m_skeletonAvgRadius);
    }

    private double m_skeletonAvgRadius = 0;
    /// <summary>
    /// The average radius of the longest skeleton path.
    /// </summary>
    private double m_longestSkeletonAvgRadius = 0;

    private float m_aggressiveness = 0;
    /// <summary>
    /// A measure of how aggressive the skeletonisation process should be. Is used to calculate the "face devalue factor" by using it as an exponent.
    /// </summary>
    public float Aggressiveness
    {
      get { return m_aggressiveness; }
      set
      {
        if (m_aggressiveness != value)
        {
          m_aggressiveness = value;
          if(Preview)
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
        if (!m_userHasEdited && value)
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
        if (m_showPreview != value && Skeleton == null)
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

    private GameObject m_meshSource = null;
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
          m_meshSource = null;

          if (m_selectedMesh == null)
          {
            Skeleton = null;
            m_skeletoniser = null;
            return;
          }

          //Warning if mesh is very large        
          if (Preview && m_selectedMesh.vertexCount > VERTEX_WARNING_THRESHOLD)
          {
            Preview = false;
            DisplayVertexCountWarning = true;
          }
          if (Preview && !UserHasEdited)
          {
            SkeletoniseSelectedMesh();
          }
        }
      }
    }

    private bool m_useLongestContinuousPathInSkeleton = true;
    /// <summary>
    /// Toggle for fetching only the longest continous path in the skeletoniser instead of the entire skeleton
    /// </summary>
    public bool UseLongestPath
    {
      get { return m_useLongestContinuousPathInSkeleton; }
      set
      {
        if (value != m_useLongestContinuousPathInSkeleton)
        {
          m_useLongestContinuousPathInSkeleton = value;
          m_skeletoniser.applyRadius(value ? m_longestSkeletonAvgRadius : m_skeletonAvgRadius);
        }
      }
    }

    private bool m_showUnderlyingSkeleton = false;
    private bool m_transferNodeSettings = false;

    private float m_fixedRadius = 0;
    /// <summary>
    /// The exposed manually set radius used for the cable after application and used for the preview of the skeleton/route
    /// </summary>
    public float FixedRadius
    {
      get { return m_fixedRadius; }
      set
      {
        if(value != m_fixedRadius)
        {
          m_fixedRadius = value;
          if(UseFixedRadius)
            m_skeletoniser.applyRadius(m_fixedRadius);
        }
      }
    }
   
    private bool m_useFixedRadius = false;
    /// <summary>
    /// Toggle for using a fixed radius for the joints in the skeletonisation process
    /// </summary>
    public bool UseFixedRadius
    {
      get { return m_useFixedRadius; }
      set
      {
        if (value != m_useFixedRadius) 
        {
          m_useFixedRadius = value;
          if (!value && Skeleton != null)
          {
            m_skeletoniser.applyRadius(UseLongestPath ? m_longestSkeletonAvgRadius : m_skeletonAvgRadius);
            FixedRadius = (float)SkeletonRadius();
          }
          else if (value)
          {
            m_skeletoniser.applyRadius(FixedRadius);
          }
        }        
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
      var cableObject = Route.gameObject;
      HandleSelectedObject(cableObject);
    }

    /// <summary>
    /// Draws the route/skeleton preview if possible
    /// </summary>
    /// <param name="sceneView"></param>
    public override void OnSceneViewGUI(SceneView sceneView)
    {
      if (Preview && Skeleton != null)
      {
        var normalColor = Route.Any() ? Color.yellow : Color.blue;
        if (Event.current.shift)
        {
          DrawRoutePreview(sceneView, jointColor: Color.red, jointClickableHandle: _ => Skeleton.joints.Count > 2, unclickableColor: normalColor, onJointClickCallback: j =>
          {
            m_skeletoniser.removeVertex(j.skeletoniserIndex);
            UserHasEdited = true;
            UpdateSkeleton();
          }, edgeColor: normalColor, ignoreFilters: m_showUnderlyingSkeleton);
        }
        else if (Event.current.control)
        {
          DrawRoutePreview(sceneView, jointColor: Color.green, jointClickableHandle: jIdx => m_skeletoniser.isUpscalePossible(jIdx), unclickableColor: normalColor, onJointClickCallback: j =>
          {            
            if (m_skeletoniser.upscaleJoint(j.skeletoniserIndex))
            {
              UserHasEdited = true;
              UpdateSkeleton();
            }
          }, edgeColor: normalColor, edgeClickableHandle: (j1Idx, j2Idx) => m_skeletoniser.isUpscalePossible(j1Idx, j2Idx), onEdgeClickCallback: (j1Idx, j2Idx) =>
          {
            m_skeletoniser.upscaleEdge(j1Idx, j2Idx);
            UserHasEdited = true;
            UpdateSkeleton();
          }, ignoreFilters: m_showUnderlyingSkeleton);
        }
        else
        {
          DrawRoutePreview(sceneView, jointColor: normalColor, edgeColor: normalColor, ignoreFilters: m_showUnderlyingSkeleton);
        }
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

      EditorGUILayout.LabelField(GUI.MakeLabel("Route Target", bold: true));

      if (GUILayout.Button(SelectGameObjectTool ? "Waiting for selection" + AwaitingUserActionDotsWithPadding() : "Select object in scene", GUILayout.ExpandWidth(false)))
      {
        SelectGameObjectTool = true;
      }

      
      InspectorEditor.RequestConstantRepaint = SelectGameObjectTool;

      EditorGUI.BeginDisabledGroup(SelectGameObjectTool);
      m_selectedParent = (GameObject)EditorGUILayout.ObjectField(GUI.MakeLabel("Parent", toolTip: "The game object which the routes transform is inherited from. This object will also be the parent of the route nodes unless previous node settings are transferred. Changing this value regenerates the skeleton, removing any adjustments made."), m_selectedParent, typeof(GameObject), true);
      SelectedMesh = (Mesh)EditorGUILayout.ObjectField(GUI.MakeLabel("Base mesh", toolTip: "The mesh which is used for generating the initial route. Changing this value regenerates the skeleton, removing any adjustments made."), SelectedMesh, typeof(Mesh), true);

      EditorGUILayout.LabelField(GUI.MakeLabel("Route Generation Settings", bold: true));
      EditorGUI.indentLevel = 1;
      Aggressiveness = EditorGUILayout.DelayedFloatField(GUI.MakeLabel("Aggressiveness", toolTip: "The amount of priority given to achieving a skeleton structure during the algorithms run. Higher values tend to produce more detailed skeletons but possibly introduce artifacts. Changing this value regenerates the skeleton, removing any adjustments made."), Aggressiveness);

      //Settings which do not affect skeletonisation ----------------------------------------------------
      UseLongestPath = InspectorGUI.Toggle(GUI.MakeLabel("Longest Continuous Path", toolTip: "Use the longest continuous path from the produced skeleton instead of the complete skeleton. Useful for omitting larger details in the mesh from the generated route or for reducing artifacts."), UseLongestPath);
      uint newNumNodes = (uint)System.Math.Clamp(EditorGUILayout.DelayedIntField(GUI.MakeLabel("Number of Nodes:", toolTip: "The current number of nodes in the skeleton. This value can be increased to attempt an automatic upscaling to that number of joints."), Preview && Skeleton != null ? Skeleton.joints.Count : 0, GUILayout.ExpandWidth(false)), 0, uint.MaxValue);
      if (newNumNodes > Skeleton?.joints.Count)
      {
        m_skeletoniser.upscaleSkeleton(newNumNodes - (uint)Skeleton.joints.Count + m_skeletoniser.remainingVertices(), SphereSkeletoniser.UpscalingMethod.BOTH, m_longestSkeleton);        
        UserHasEdited = true;
        UpdateSkeleton();
      }
      EditorGUILayout.BeginHorizontal();
      UseFixedRadius = InspectorGUI.Toggle(GUI.MakeLabel("Use Fixed Radius"), UseFixedRadius);
      FixedRadius = Mathf.Clamp(EditorGUILayout.FloatField(FixedRadius), 0, float.MaxValue);
      EditorGUILayout.EndHorizontal();

      EditorGUI.indentLevel = 0;

      Preview = EditorGUILayout.BeginToggleGroup(GUI.MakeLabel("Modify Route in Editor", toolTip: "Preview the route which will be created and modify individual joints."), Preview);
      if (Preview)
      {
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(GUI.MakeLabel("Shift + Click: Remove joints"));
        EditorGUILayout.LabelField(GUI.MakeLabel("Control + Click: Add joints"));
        EditorGUILayout.EndVertical();
      }
      //Segmenting the skeleton currently crashes unity so this is disabled for now
      //m_showUnderlyingSkeleton = InspectorGUI.Toggle(GUI.MakeLabel("Show underlying skeleton", toolTip: "Display all joints in the skeleton regardless of connectivity. Can be useful for removing disjoint skeletons produced by disjoint parts of the input mesh or for removing artifacts."), m_showUnderlyingSkeleton);
      if (GUILayout.Button(GUI.MakeLabel("Regenerate"), GUILayout.ExpandWidth(false)))
      {
        SkeletoniseSelectedMesh();
      }
      EditorGUILayout.EndToggleGroup();

      if (Route.Any())
      {
        InspectorGUI.WarningLabel("This component already has a node route. Generating a new route will overwrite existing nodes. Check <b>Approximate Nodes</b> to transfer existing node settings (eg. \"body fixed node\") to the new route. This feature is experimental and may not lead to perfect results.");

        var position = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(false,
                                                                             EditorGUIUtility.singleLineHeight +
                                                                             EditorGUIUtility.standardVerticalSpacing));

        GUIStyle toggleStyle = GUI.Skin.toggle;
        var toggleWidth = toggleStyle.fixedHeight > 0 ? toggleStyle.fixedHeight : toggleStyle.CalcSize(new GUIContent("")).x;

        var label = GUI.MakeLabel("Approximate existing nodes", toolTip: "Attempt to transfer settings from the existing nodes onto the nodes generated by the tool. Will prioritise preserving special nodes.");
        var labelSize = GUI.Skin.label.CalcSize(label);
        labelSize.x += 10;

        var toggleRect = new Rect(position.xMax - labelSize.x - toggleWidth,
                                  position.y + EditorGUIUtility.standardVerticalSpacing,
                                   toggleWidth + labelSize.x,
                                   toggleWidth);
        var labelRect = new Rect(position.xMax - labelSize.x,
                                   position.y + EditorGUIUtility.standardVerticalSpacing,
                                   labelSize.x,
                                   labelSize.y);

        m_transferNodeSettings = UnityEngine.GUI.Toggle(toggleRect, m_transferNodeSettings, emptyContent);
        UnityEngine.GUI.Label(labelRect, label);
      }

      var applyCancelState = InspectorGUI.PositiveNegativeButtons(Skeleton != null && Skeleton?.joints.Count > 0,
                                                                  "Apply",
                                                                  "Apply current configuration.",
                                                                  "Cancel");

      EditorGUI.EndDisabledGroup();

      if (applyCancelState == InspectorGUI.PositiveNegativeResult.Positive)
      {
        CreateRoute(Route.Any() && m_transferNodeSettings);
        PerformRemoveFromParent();
      }
      else if (applyCancelState == InspectorGUI.PositiveNegativeResult.Negative)
        PerformRemoveFromParent();

      InspectorGUI.OnDropdownToolEnd();
    }

    void TransferSettingsBySpecialNodes(ref List<NodeT> destNodes, float cableRadius, float threshhold)
    {
      var specialNodes = Route.Where(n =>
      {
        if (n.Parent != m_selectedParent)
          return true;
        if (n is WireRouteNode)
          return (n as WireRouteNode).Type != AGXUnity.Wire.NodeType.FreeNode;
        if (n is CableRouteNode)
          return (n as CableRouteNode).Type != AGXUnity.Cable.NodeType.FreeNode;
        return false;
      });

      //Create a sliding window which also has the ends as a pair with themselves
      var destNodePairs = destNodes.Zip(destNodes.Skip(1), (a, b) => Tuple.Create(a, b)).Prepend(Tuple.Create(destNodes.First(), destNodes.First())).Append(Tuple.Create(destNodes.Last(), destNodes.Last()));

      foreach(var node in specialNodes)
      {
        //We try to find a pair of nodes from the destNodes input which produces the least distance. The previous fixed node should most likely lay between those if the user wanted the special nodes to persist in their current locations.
        var bestPair = destNodePairs.Select((n, i) => new { nodePair = n, idx = i } ).Aggregate((currMin, p) =>
        {
          return (currMin == null) || (Vector3.Distance(p.nodePair.Item1.Position, node.Position) + Vector3.Distance(p.nodePair.Item2.Position, node.Position)) < Vector3.Distance(currMin.nodePair.Item1.Position, node.Position) + Vector3.Distance(currMin.nodePair.Item2.Position, node.Position) ? p : currMin;
        });
        destNodes.Insert(bestPair.idx, node);
      }
      //Merge nodes that are too close to the transferred special nodes
      destNodes = destNodes.Where(n => specialNodes.Contains(n) || specialNodes.All(sn => Vector3.Distance(n.Position, sn.Position) >= cableRadius * threshhold)).ToList();
    }

    void TransferSettingsByDistance(IEnumerable<NodeT> destNodes, float cableRadius, float threshhold)
    {
      //Starting with the ends of the cable and trying to match them to our best ability
      NodeT[] oldEnds = { Route.First(), Route.Last() }, newEnds = { destNodes.First(), destNodes.Last() };
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

      if (minDist < threshhold * cableRadius) //If one pair is close enough to assume they want to be considered the same by the user. The other pair is then implied to be a match as well
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
      bool baseOnExistingRoute = destNodes.Count() >= Route.NumNodes;

      IEnumerable<NodeT> from, to;
      List<NodeT> used = new();
      //Skip the ends as they were handled earlier
      from = (baseOnExistingRoute ? Route : destNodes);
      from = from.Skip(1).Take(from.Count() - 2);
      to = (baseOnExistingRoute ? destNodes : Route);
      to = to.Skip(1).Take(to.Count() - 2);

      foreach (var node in from)
      {
        //Find the closest node in the node list and transfer the settings
        var closestNode = to.Where(n => !used.Contains(n)).Aggregate(to.First(), (closest, next) => (next.Position - node.Position).magnitude < (closest.Position - node.Position).magnitude ? next : closest);
        used.Add(closestNode);
        if (baseOnExistingRoute)
          TransferNodeSettings(node, closestNode);
        else
          TransferNodeSettings(closestNode, node);
      }
    }

    /// <summary>
    /// Uses the current route/skeleton to create nodes in the parent route object
    /// </summary>
    /// <param name="applyCurrentNodeSettings">If an attempt should be made to translate the current nodes settings (type and parent) to the created nodes</param>
    private void CreateRoute(bool applyCurrentNodeSettings)
    {
      Undo.SetCurrentGroupName($"Generating route from mesh \"{SelectedMesh.name}\"");
      var undoGroupId = Undo.GetCurrentGroup();

      if (!Preview && Skeleton == null)
        SkeletoniseSelectedMesh();

      float avgRadius = 0;

      var nodeList = new List<NodeT>();
      SphereSkeleton.dfs_iterator currJoint = Skeleton.begin();
      float minDist = float.PositiveInfinity;

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
          agx.Vec3 edge = currJoint.next_joint().position - currJoint.position;
          minDist = Mathf.Min(minDist, (float)edge.length());

          localRotation = Quaternion.FromToRotation(Vector3.forward, edge.ToHandedVector3());
        }
        nodeList.Add(CreateNode(currJoint.position.ToHandedVector3(), localRotation));
        currJoint.inc();
      }

      if (UseFixedRadius)
        avgRadius = FixedRadius;
      else
        avgRadius /= nodeList.Count;

      //If a route already exists then we will attempt to transfer parents and node types to the best of our ability
      if (Route.Any() && applyCurrentNodeSettings)
      {
        //TransferSettingsByDistance(nodeList, avgRadius, NODE_TRANSFER_DISTANCE_THRESHOLD);
        TransferSettingsBySpecialNodes(ref nodeList, avgRadius, NODE_TRANSFER_DISTANCE_THRESHOLD);
      }

      Route.Clear();
      foreach (var node in nodeList)
      {
        Route.Add(node);
      }

      if (Route is WireRoute)
      {
        Route.gameObject.GetComponent<Wire>().Radius = UseFixedRadius ? m_fixedRadius : SkeletonRadius();
      }
      else if (Route is CableRoute)
      {
        Route.gameObject.GetComponent<Cable>().Radius = UseFixedRadius ? m_fixedRadius : SkeletonRadius();
        Route.gameObject.GetComponent<Cable>().ResolutionPerUnitLength = Mathf.Max(minDist, 0.5f / avgRadius); //Max reasonable resolution / 2
      }

      //Disable the renderer to signify the conversion
      MeshRenderer renderer;
      if (m_meshSource != null && m_meshSource.TryGetComponent(out renderer))
      {
        Undo.RegisterCompleteObjectUndo(renderer, "Disable mesh renderer");
        renderer.enabled = false;
      }

      //It is possible that the selected node will cause problems so we try to deselect a node in the parent tool
      //TODO: Check with someone if this is a stupid thing to do
      var routeTool = GetParent() as RouteTool<ParentT, NodeT>;
      routeTool.Selected = null;

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
        var skel = UseLongestPath ? agxUtilSWIG.getLongestContinuousSkeletonSegment(m_skeletoniser.getSkeleton()) : m_skeletoniser.getSkeleton();
        m_skeletoniser.consolidateSkeleton(skel, 1);
        Skeleton = m_skeletoniser.getSkeleton();
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
        m_meshSource = selected;
        m_skeletoniser = null;
        Skeleton = null;
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
      if (SelectedMesh == null)
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

      //Calculate radius for restoring radius later
      m_skeletonAvgRadius = 0;
      m_longestSkeletonAvgRadius = 0;

      if (m_skeleton != null)
      {
        foreach (var joint in m_skeleton.joints)
        {
          m_skeletonAvgRadius += joint.radius;
        }
        m_skeletonAvgRadius /= m_skeleton.joints.Count;


        foreach (var joint in m_longestSkeleton.joints)
        {
          m_longestSkeletonAvgRadius += joint.radius;
        }
        m_longestSkeletonAvgRadius /= m_longestSkeleton.joints.Count;
      }

      if (!UseFixedRadius)
      {
        FixedRadius = (float)(UseLongestPath ? m_longestSkeletonAvgRadius : m_skeletonAvgRadius);
      }

      m_skeletoniser.applyRadius(UseLongestPath ? m_longestSkeletonAvgRadius : m_skeletonAvgRadius);
      UpdateSkeleton();
    }

    /// <summary>
    /// Draw the current route/skeleton using handles
    /// </summary>
    /// <param name="sceneView">The scene view that is currently being viewed. Used to determine the camera's position.</param>
    private void DrawRoutePreview(SceneView sceneView, Color? jointColor = null, Func<uint, bool> jointClickableHandle = null, Color? unclickableColor = null, Action<SphereSkeleton.Joint> onJointClickCallback = null, Color? edgeColor = null, Func<uint, uint, bool> edgeClickableHandle = null, Action<uint, uint> onEdgeClickCallback = null, bool ignoreFilters = false)
    {
      if (!jointColor.HasValue)
        jointColor = Color.blue;
      if (!edgeColor.HasValue)
        edgeColor = Color.blue;

      bool useLongestPath = UseLongestPath;
      if (ignoreFilters)
        UseLongestPath = false;

      //Drawing without depth makes selection hard so a custom z-pass is used by simply sorting the draw calls in this function by distance form the camera and otherwise always passing the zTest
      var drawcalls = new List<Tuple<float, Action, Color>>(); //Make sure to provide proper capture for each drawcall!
      Matrix4x4 skeletonToWorld = SkeletonToWorld();
      Vector3 cameraPosRouteSpace = skeletonToWorld.inverse.MultiplyPoint(sceneView.camera.transform.position);

      SphereSkeletonVector skeletonSegments;
      if (ignoreFilters)
      {
        skeletonSegments = m_skeleton.segmentSkeleton();
      }
      else
      {
        skeletonSegments = new SphereSkeletonVector
        {
          Skeleton
        };
      }

      //Segment skeleton to draw entire structure when the longest path isn't used. Otherwise DFS will not visit joints is disjoint parts of the skeleton
      foreach (var segment in skeletonSegments)
      {

        float handleRadius = UseFixedRadius ? FixedRadius : SkeletonRadius();
        float handleDiameter = handleRadius * 2;
        //Traverse DFS to mimic resulting route
        SphereSkeleton.dfs_iterator currDFSJoint = segment.begin();
        SphereSkeleton.Joint prevDFSJoint = null;
        while (!currDFSJoint.isEnd())
        {
          float distanceToCamera;
          Vector3 currPos = currDFSJoint.position.ToHandedVector3();
          uint currDFSJointSkeletoniserIndex = currDFSJoint.skeletoniserIndex;
          //Show actual edges instead of the DFS path if ignoreFilters is true
          if (ignoreFilters)
            prevDFSJoint = currDFSJoint.prev_joint();

          //Draw edge and potentially upscale button for edge
          if (prevDFSJoint != null)
          {
            uint prevDFSJointSkeletoniserIndex = prevDFSJoint.skeletoniserIndex;
            Vector3 prevPos = prevDFSJoint.position.ToHandedVector3();
            //Make sure edge buttons always draw infront of the edge
            distanceToCamera = Mathf.Max((cameraPosRouteSpace - currPos).magnitude, (cameraPosRouteSpace - prevPos).magnitude);
            drawcalls.Add(new Tuple<float, Action, Color>(distanceToCamera, () => Handles.DrawLine(currPos, prevPos, 3), edgeColor.Value));

            if (edgeClickableHandle != null && edgeClickableHandle(prevDFSJointSkeletoniserIndex, currDFSJointSkeletoniserIndex))
            {
              Vector3 facingDirection = currPos - prevPos;
              Vector3 midPoint = prevPos + facingDirection * 0.5f;
              var edgeCubeColor = jointColor.Value * 0.5f;
              edgeCubeColor.a = 1;
              //Reduce the size of the cube when two joints are very close
              float size = Mathf.Clamp(facingDirection.magnitude - 2 * handleRadius, handleRadius / 2, handleRadius);

              distanceToCamera = (cameraPosRouteSpace - midPoint).magnitude;
              drawcalls.Add(new Tuple<float, Action, Color>(distanceToCamera, () =>
              {
                if (Handles.Button(midPoint, Quaternion.LookRotation(facingDirection), size, size, Handles.CubeHandleCap))
                  onEdgeClickCallback(prevDFSJointSkeletoniserIndex, currDFSJointSkeletoniserIndex);
              }, edgeCubeColor));
            }
          }

          distanceToCamera = (cameraPosRouteSpace - currPos).magnitude - handleRadius;
          //Draw joint
          if (jointClickableHandle != null && jointClickableHandle(currDFSJoint.skeletoniserIndex))
          {
            var jointToAlter = currDFSJoint.deref();
            drawcalls.Add(new Tuple<float, Action, Color>(distanceToCamera, () =>
            {
              if (Handles.Button(currPos, Quaternion.identity, handleDiameter, handleRadius, Handles.SphereHandleCap))
                onJointClickCallback(jointToAlter);
            }, jointColor.Value));
          }
          else
          {
            drawcalls.Add(new Tuple<float, Action, Color>(distanceToCamera, () => Handles.SphereHandleCap(0, currPos, Quaternion.identity, handleDiameter, EventType.Repaint), unclickableColor.HasValue ? unclickableColor.Value : jointColor.Value));
          }
          prevDFSJoint = currDFSJoint.deref();
          currDFSJoint.inc();
        }
      }
      //Sort in descending order to draw the furthest objects first
      drawcalls.Sort((t1, t2) => -t1.Item1.CompareTo(t2.Item1));

      if (ignoreFilters)
        UseLongestPath = useLongestPath;

      using (new Handles.DrawingScope(skeletonToWorld))
      {
        float furthestDistance = drawcalls.First().Item1;
        float closestDistance = drawcalls.Last().Item1;
        for (int i = 0; i < drawcalls.Count; i++)
        {
          var drawcall = drawcalls[i];
          float shade = 1f - (drawcall.Item1 / furthestDistance) * (0.6f-(closestDistance / furthestDistance / 0.6f));
          var shadeColor = new Color(shade, shade, shade);
          using (new Handles.DrawingScope(drawcall.Item3 * shadeColor))
          {
            drawcall.Item2.Invoke();
          }
        }
      }
    }
  }
}
