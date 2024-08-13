using AGXUnity;
using AGXUnity.Utils;
using agxUtil;
using PlasticGui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static agxUtil.SphereSkeleton;
using GUI = AGXUnity.Utils.GUI;


public static class SphereSkeletonExtenstions
{
  public static double CurrAvgRadius(this SphereSkeleton skeleton)
  {
    return skeleton.joints.Sum(j => j.radius) / skeleton.joints.Count;
  }

  public static double CurrLength(this SphereSkeleton skeleton)
  {
    double length = 0;
    foreach (var segment in skeleton.segmentSkeleton())
    {
      var joint = segment.begin();
      while (!joint.isEnd())
      {
        if (!joint.isLast())
          length += (joint.position - joint.next_joint().position).length();
        joint.inc();
      }
    }
    return length;
  }

  public static double CurrResolution(this SphereSkeleton skeleton)
  {
    return skeleton.joints.Count / skeleton.CurrLength();
  }
}

namespace AGXUnityEditor.Tools
{
  public class RouteFromMeshTool<NodeT> : Tool where NodeT : RouteNode, new()
  {
    private GameObject m_selectedParent = null;
    private Mesh m_selectedMesh = null;
    private MeshMerger m_mergedSelectedMesh = null;
    private SphereSkeleton m_skeleton = null;
    private double m_skeletonAvgRadius = 0;
    private float m_aggressiveness = 0;
    private bool m_showPreview = true;
    private bool m_useLongestContinuousPathInSkeleton = true;
    private bool m_useFixedRadius = false;
    private bool m_shouldSkeletonise = false;
    private bool m_userHasEdited = false;
    private float m_fixedRadius = 0;
    private bool m_displayVertexCountWarning = false;
    private SphereSkeletoniser m_skeletoniser = null;

    [HideInInspector]
    public Route<NodeT> Route { get; private set; }

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
    public Mesh SelectedMesh
    {
      get
      {
        return m_selectedMesh;
      }
      set 
      {
        if (m_selectedMesh != value)
        {         
          DisplayVertexCountWarning = false;
          m_selectedMesh = value;
          //Warning if mesh is very large        
          if (Preview && m_selectedMesh.vertexCount > VERTEX_WARNING_THRESHOLD)
          {
            Preview = false;
            DisplayVertexCountWarning = true;
          }
          if (Preview)
          {
            m_shouldSkeletonise = true;
          }
        }          
      }
    }

    public bool UseLongestPath
    {
      get { return m_useLongestContinuousPathInSkeleton; }
      set
      {
        UpdateSkeleton();
        m_useLongestContinuousPathInSkeleton = value;
      }
    }

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

    public bool DisplayVertexCountWarning
    {
      get { return m_displayVertexCountWarning; }
      set { m_displayVertexCountWarning = value; }
    }

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

    public RouteFromMeshTool(Route<NodeT> parentRoute)
      : base(isSingleInstanceTool: true)
    {
      Route = parentRoute;
      var cableObject = Route.gameObject.transform.parent == null ? Route.gameObject : Route.gameObject.transform.parent.gameObject;
      HandleSelectedObject(cableObject);
    }

    public override void OnSceneViewGUI(SceneView sceneView)
    {
      if (Preview && m_skeleton != null)
      {
        if (Event.current.alt) DrawSkeleton();
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

    public void OnInspectorGUI()
    {
      InspectorGUI.OnDropdownToolBegin("Create node route using visual mesh");

      var skin = InspectorEditor.Skin;
      var emptyContent = GUI.MakeLabel(" ");

      if (DisplayVertexCountWarning)
      {
        InspectorGUI.WarningLabel("Route preview has been disabled due to high vertex count of selected mesh. Enabling preview will likely result in long preview times.");
      }

      EditorGUILayout.BeginHorizontal();
      EditorGUILayout.PrefixLabel(GUI.MakeLabel("Select from scene"));      
      if (InspectorGUI.Button(MiscIcon.Mesh, !SelectGameObjectTool, ""))
      {
        SelectGameObjectTool = true;
      }
      EditorGUILayout.EndHorizontal();
      InspectorEditor.RequestConstantRepaint = SelectGameObjectTool;

      EditorGUILayout.BeginHorizontal();
      EditorGUILayout.PrefixLabel(GUI.MakeLabel("Base mesh", m_userHasEdited ? Color.red : Color.white, toolTip: "The mesh which is used for generating the initial route. Changing this value regenerates the skeleton, removing any adjustments made."));
      SelectedMesh = (Mesh)EditorGUILayout.ObjectField(m_selectedMesh, typeof(Mesh), true);
      EditorGUILayout.EndHorizontal();

      EditorGUILayout.BeginHorizontal();
      EditorGUILayout.PrefixLabel(GUI.MakeLabel("Parent"));
      m_selectedParent = (GameObject)EditorGUILayout.ObjectField(m_selectedParent, typeof(GameObject), true);
      EditorGUILayout.EndHorizontal();

      Preview = EditorGUILayout.BeginToggleGroup(GUI.MakeLabel("Preview (?)", toolTip: "Hold keys during preview\nShift: Remove joints\nControl: Add joints\nAlt: Show underlying skeleton"), Preview);
      UseFixedRadius = EditorGUILayout.BeginToggleGroup("Use Fixed Radius", UseFixedRadius);
      EditorGUILayout.BeginHorizontal();
      EditorGUILayout.PrefixLabel(GUI.MakeLabel("Radius"));
      m_fixedRadius = Mathf.Clamp(EditorGUILayout.FloatField(m_fixedRadius), 0, 5);
      EditorGUILayout.EndHorizontal();
      EditorGUILayout.EndToggleGroup();
      EditorGUILayout.EndToggleGroup();
      

      EditorGUILayout.BeginHorizontal();
      EditorGUILayout.LabelField(GUI.MakeLabel("Number of Nodes:"));
      EditorGUILayout.LabelField(m_skeleton != null ? GUI.MakeLabel(m_skeleton.joints.Count.ToString()) : GUI.MakeLabel("-"));
      EditorGUILayout.EndHorizontal();

      var setFactor = EditorGUILayout.DelayedFloatField(GUI.MakeLabel("Aggressiveness", m_userHasEdited ? Color.red : Color.white, toolTip: "The amount of priority given to achieving a skeleton structure during the algorithms run. Higher values tend to produce more detailed skeletons but possibly introduce artifacts. Changing this value regenerates the skeleton, removing any adjustments made."), m_aggressiveness);
      if (setFactor != m_aggressiveness)
      {
        m_aggressiveness = setFactor;
        m_shouldSkeletonise = true;
      }

      UseLongestPath = InspectorGUI.Toggle(GUI.MakeLabel("Longest Continous Path", toolTip: "Use the longest continuous path from the produced skeleton instead of the complete skeleton. Useful for omitting larger details in the mesh from the generated route or for reducing artifacts."), UseLongestPath);

      var applyCancelState = InspectorGUI.PositiveNegativeButtons(m_selectedParent != null && m_skeleton != null && m_skeleton?.joints.Count > 0,
                                                                   "Apply",
                                                                   "Apply current configuration.",
                                                                   "Cancel");      

      if (applyCancelState == InspectorGUI.PositiveNegativeResult.Positive)
      {
        if (Route.Any()) //Overwrite
        {
          switch (EditorUtility.DisplayDialogComplex("Overwrite", "Applying will overwrite the current route and any node settings. Would you like to transfer node settings to the new route?", "Apply", "Cancel", "Apply w/current node settings"))
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

    private void CreateRoute(bool applyCurrentNodeSettings)
    {
      Undo.SetCurrentGroupName($"Generating route from mesh \"{m_selectedMesh.name}\"");
      var undoGroupId = Undo.GetCurrentGroup();

      if (!Preview && m_skeleton == null)
        SkeletoniseSelectedMesh();

      float avgRadius = 0;

      var nodeList = new List<NodeT>();
      dfs_iterator currJoint = m_skeleton.begin();

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

      //If a route already exists then we will attempt to transfer parents and node types to the best of our ability
      if (Route.Any() && applyCurrentNodeSettings)
      {
        //Starting with the ends of the cable, making sure the ends don't swap
        NodeT oldFirst = Route.First(), newFirst = nodeList.First(), oldLast = Route.Last(), newLast = nodeList.Last();
        if ((oldFirst.Position - newFirst.Position).magnitude < (oldFirst.Position - newLast.Position).magnitude)
        {
          TransferNodeSettings(oldFirst, newFirst);
          TransferNodeSettings(oldLast, newLast);
        }
        else
        {
          TransferNodeSettings(oldFirst, newLast);
          TransferNodeSettings(oldLast, newFirst);
        }

        //Base the transfer on the route which has the least nodes.
        bool baseOnExistingRoute = nodeList.Count >= Route.NumNodes;

        IEnumerable<NodeT> from, to;
        from = baseOnExistingRoute ? Route : nodeList;
        to = baseOnExistingRoute ? nodeList : Route;

        foreach (var node in from.Skip(1).Take(Route.NumNodes - 2))
        {
          //Find the closest node in the node list and transfer the settings
          var closestNode = to.Aggregate(node, (closest, next) => (next.Position - node.Position).magnitude < (closest.Position - node.Position).magnitude ? next : closest);
          if (baseOnExistingRoute)
            TransferNodeSettings(node, closestNode);
          else
            TransferNodeSettings(closestNode, node);
        }
      }      

      Route.Clear();
      foreach(var node in nodeList)
      {
        Route.Add(node);
      }


      avgRadius /= Route.NumNodes;

      if (UseFixedRadius)
        avgRadius = m_fixedRadius;

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

      m_skeleton = null;
      m_selectedMesh = null;


      MeshRenderer renderer;
      if (m_selectedParent.TryGetComponent(out renderer))
      {
        Undo.RegisterCompleteObjectUndo(renderer, "Disable mesh renderer");
        renderer.enabled = false;
      }

      m_selectedParent = null;
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

    private void UpdateSkeleton()
    {
      if(m_skeletoniser != null)
      {
        m_skeleton = UseLongestPath ? agxUtil.agxUtilSWIG.getLongestContinousSkeletonSegment(m_skeletoniser.getSkeleton()) : m_skeletoniser.getSkeleton();
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

    public override void OnAdd()
    {
      base.OnAdd();
      EditorApplication.update += Update;
    }

    public override void OnRemove()
    {
      base.OnRemove();
      EditorApplication.update -= Update;
    }

    void Update()
    {
      if (m_shouldSkeletonise)
        SkeletoniseSelectedMesh();
    }

    const int VERTEX_WARNING_THRESHOLD = 100_000;

    private bool HandleSelectedObject(GameObject selected)
    {
      m_selectedParent = null;
      m_selectedMesh = null;
      m_skeletoniser = null;
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
        if(mesh == null)
        {
          return false;
        }
        //Warning if mesh is very large        
        if (Preview && mesh.vertexCount > VERTEX_WARNING_THRESHOLD)
        {
          Preview = false;
          DisplayVertexCountWarning = true;
        }
        m_selectedParent = selected;
        m_selectedMesh = mesh;
        m_skeletoniser = null;
        m_skeleton = null;
        if (Preview)
        {
          m_shouldSkeletonise = true;
        }
        return true;
      }
      return false;
    }

    private void SkeletoniseSelectedMesh()
    {
      m_shouldSkeletonise = false;
      m_userHasEdited = false;
      GameObject temp = new();
      temp.transform.localScale = m_selectedParent.transform.lossyScale;
      m_mergedSelectedMesh = MeshMerger.Merge(temp.transform, new Mesh[] { m_selectedMesh });
      UnityEngine.Object.DestroyImmediate(temp);
      m_skeletoniser = new SphereSkeletoniser(m_mergedSelectedMesh.Vertices, m_mergedSelectedMesh.Indices);
      m_skeletoniser.setFaceDevalueFactor(Mathf.Pow(10, m_aggressiveness));
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

    private void DrawRoutePreview(SceneView sceneView)
    {
      //Drawing without depth makes selection hard so a custom z-pass is used by simply sorting the draw calls in this function and otherwise always passing the zTest
      var drawcalls = new List<Tuple<float, Action, Color>>();
      Event e = Event.current;
      Color normalColor = Route.Any() ? Color.yellow : Color.blue, removeColor = Color.red, addColor = Color.green, edgeColor = normalColor;
      Matrix4x4 routeTransform = Matrix4x4.TRS(m_selectedParent.transform.position, m_selectedParent.transform.rotation, Vector3.one);
      Vector3 cameraPosRouteSpace = routeTransform.inverse * sceneView.camera.transform.position;      
      using (new Handles.DrawingScope(routeTransform))
      {
        float radius = UseFixedRadius ? m_fixedRadius : (float)m_skeletonAvgRadius;
        float diameter = radius * 2;
        var currJoint = m_skeleton.begin();
        SphereSkeleton.Joint prevJoint = null;
        while (!currJoint.isEnd())
        {
          float distanceToCamera;
          Vector3 currPos = currJoint.position.ToHandedVector3();          

          uint currJointSkeletoniserIdx = currJoint.skeletoniserIndex;
          if (prevJoint != null)
          {
            Vector3 prevPos = prevJoint.position.ToHandedVector3();
            uint prevJointSkeletoniserIdx = prevJoint.skeletoniserIndex;
            distanceToCamera = Mathf.Min((cameraPosRouteSpace - currJoint.position.ToHandedVector3()).magnitude, (cameraPosRouteSpace - prevJoint.position.ToHandedVector3()).magnitude);
            drawcalls.Add(new Tuple<float, Action, Color>(distanceToCamera, () => Handles.DrawLine(currPos, prevPos, 3), edgeColor));

            if (e.control)
            {
              Vector3 start = prevJoint.position.ToHandedVector3();
              Vector3 direction = (currJoint.position - prevJoint.position).ToHandedVector3();
              Vector3 midPoint = start + direction * 0.5f;
              var edgeCubeColor = addColor * 0.5f;
              edgeCubeColor.a = 1;
              float size = Mathf.Clamp(direction.magnitude - 2 * radius, radius / 4, radius);
              distanceToCamera = (cameraPosRouteSpace - midPoint).magnitude;
              drawcalls.Add(new Tuple<float, Action, Color>(distanceToCamera, () =>
              {
                if (Handles.Button(midPoint, Quaternion.LookRotation(direction), size, size, Handles.CubeHandleCap))
                {
                  m_skeletoniser.upscaleEdge(prevJointSkeletoniserIdx, currJointSkeletoniserIdx);
                  m_userHasEdited = true;
                  UpdateSkeleton();
                }
              }, edgeCubeColor));

            }
          }

          distanceToCamera = (cameraPosRouteSpace - currJoint.position.ToHandedVector3()).magnitude;
          if (!e.shift && !e.control || (currJoint.isLeaf() && !e.shift))
          {
            drawcalls.Add(new Tuple<float, Action, Color>(distanceToCamera, () => Handles.SphereHandleCap(0, currPos, Quaternion.identity, diameter, EventType.Repaint),  normalColor));
          }
          else
          {
            uint neighbSkeletoniserIdx1 = 0;
            uint neighbSkeletoniserIdx2 = 0;

            if (!currJoint.isLeaf())
            {
              neighbSkeletoniserIdx1 = m_skeleton.joints[(int)currJoint.adjJoints[0]].skeletoniserIndex;
              neighbSkeletoniserIdx2 = m_skeleton.joints[(int)currJoint.adjJoints[1]].skeletoniserIndex;
            }            

            drawcalls.Add(new Tuple<float, Action, Color>(distanceToCamera, () =>
            {
              if (Handles.Button(currPos, Quaternion.identity, diameter, radius, Handles.SphereHandleCap))
              {
                if (e.shift && m_skeleton.joints.Count > 2) //Remove joint
                {
                  //Remove corresponding vertex from skeletoniser and update skeleton
                  m_skeletoniser.removeVertex(currJointSkeletoniserIdx, false);
                  m_userHasEdited = true;
                  UpdateSkeleton();
                }
                else if (e.control) //Upscale at joint
                {
                  if (m_skeletoniser.upscaleVertex(currJointSkeletoniserIdx, neighbSkeletoniserIdx1, neighbSkeletoniserIdx2))
                  {
                    m_userHasEdited = true;
                    UpdateSkeleton();
                  }
                    
                }
              }
            }, e.shift && m_skeleton.joints.Count > 2 ? removeColor : addColor));
          }

          prevJoint = currJoint.deref();
          currJoint.inc();
        }

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
    
    void DrawSkeleton()
    {
      using (new Handles.DrawingScope(Matrix4x4.Translate(m_selectedParent.transform.position) * Matrix4x4.Rotate(m_selectedParent.transform.rotation)))
      {
        using (new Handles.DrawingScope(Color.green))
        {
          foreach (var joint in m_skeleton.joints)
          {
            Handles.SphereHandleCap(0, joint.position.ToHandedVector3(), Quaternion.identity, (float)joint.radius * 2, EventType.Repaint);
            using (new Handles.DrawingScope(Color.red))
            {
              foreach (var adjIdx in joint.adjJoints)
              {
                Handles.DrawLine(m_skeleton.joints[(int)adjIdx].position.ToHandedVector3(), joint.position.ToHandedVector3(), 4);
              }
            }
          }
        }
      }
    }
  }
}
