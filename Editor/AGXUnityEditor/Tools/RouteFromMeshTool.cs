using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Utils;
using GUI = AGXUnity.Utils.GUI;
using System.Linq;
using agxUtil;
using System.Text;
using Unity.VisualScripting;
using static agxUtil.SphereSkeleton;
using static Codice.Client.BaseCommands.Import.Commit;
using System;
using System.Drawing;
using static UnityEditor.FilePathAttribute;
using UnityEngine.UIElements;
using NUnit;


public static class SphereSkeletonExtenstions
{
  public static double CurrAvgRadius(this SphereSkeleton skeleton)
  {
    return skeleton.joints.Sum(j => j.radius) / skeleton.joints.Count;
  }

  public static double CurrLength(this SphereSkeleton skeleton)
  {
    double length = 0;
    foreach(var segment in skeleton.segmentSkeleton())
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
    private GameObject m_selectedMeshObject = null;
    private Mesh m_selectedMesh = null;
    private MeshMerger m_mergedSelectedMesh = null;
    private SphereSkeleton m_cachedSkeleton = null;
    private SphereSkeletonVector m_cachedSkeletonSegments = null;
    private SphereSkeleton m_processedSkeleton = null;
    private double m_processedSkeletonAvgRadius = 0;
    private float m_aggressiveness = 0;
    private bool m_showPreview = true;
    private bool m_useLongestContinousPathInSkeleton = true;
    private bool m_useFixedRadius = false;
    private float m_fixedRadius = 0;
    private bool m_displayVertexCountWarning = false;
    private SphereSkeletoniser m_skeletoniser = null;

    public Route<NodeT> Route { get; private set; }

    public bool Preview { get { return m_showPreview; } 
      set 
      {        
        if (!m_showPreview && value && m_cachedSkeleton == null)
        {
          SkeletoniseSelectedMesh();
          DisplayVertexCountWarning = false;
        }
        m_showPreview = value;
      } 
    }

    public bool UseLongestPath
    {
      get { return m_useLongestContinousPathInSkeleton; }
      set
      {   
        if(m_cachedSkeleton != null)
        {
          if (!m_useLongestContinousPathInSkeleton && value)
          {
            m_processedSkeleton = agxUtil.agxUtilSWIG.getLongestContinousSkeletonSegment(m_cachedSkeleton);
          }
          else if (m_useLongestContinousPathInSkeleton && !value)
          {
            m_processedSkeleton = new SphereSkeleton(m_cachedSkeleton);
          }
        }        
        m_useLongestContinousPathInSkeleton = value;
      }
    }

    public bool UseFixedRadius
    {
      get { return m_useFixedRadius; }
      set { m_useFixedRadius = value; }
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
      if(Preview && m_selectedMeshObject != null && m_cachedSkeleton != null)
      {
        DrawRoutePreview();               
      }

      InspectorEditor.RequestConstantRepaint = true;
    }

    private void CreateNode(Vector3 localPosition, Quaternion localRotation)
    {
      NodeT node = IFrame.Create<NodeT>(m_selectedMeshObject, localPosition, localRotation);
      if (Route is WireRoute)
        (node as WireRouteNode).Type = Wire.NodeType.FreeNode;
      else if (Route is CableRoute)
        (node as CableRouteNode).Type = Cable.NodeType.FreeNode;
      Route.Add(node);
    }

    public void OnInspectorGUI()
    {
      InspectorGUI.OnDropdownToolBegin("Create node route using visual mesh");

      var skin = InspectorEditor.Skin;
      var emptyContent = GUI.MakeLabel(" ");

      if(DisplayVertexCountWarning)
      {
        InspectorGUI.WarningLabel("Route preview has been disabled due to high vertex count of selected mesh. Enabling preview will likely result in long preview times.");
      }

      EditorGUILayout.BeginHorizontal();
      EditorGUILayout.PrefixLabel(GUI.MakeLabel("Base Mesh"));
      m_selectedMeshObject = (GameObject) EditorGUILayout.ObjectField(m_selectedMeshObject, typeof(GameObject), true);      
      if (InspectorGUI.Button(MiscIcon.Locate, SelectGameObjectTool, ""))
      {
        SelectGameObjectTool = true;
      }
      EditorGUILayout.EndHorizontal();

      Preview = InspectorGUI.Toggle(GUI.MakeLabel("Preview"), Preview);
      UseFixedRadius = EditorGUILayout.BeginToggleGroup("Use Fixed Radius", UseFixedRadius);
      EditorGUILayout.BeginHorizontal();
      EditorGUILayout.PrefixLabel(GUI.MakeLabel("Radius"));
      m_fixedRadius = Mathf.Clamp(EditorGUILayout.FloatField(m_fixedRadius), 0, 5);
      EditorGUILayout.EndHorizontal();
      EditorGUILayout.EndToggleGroup();



      EditorGUILayout.BeginHorizontal();
      EditorGUILayout.LabelField(GUI.MakeLabel("Number of Nodes"));
      EditorGUILayout.LabelField(m_processedSkeleton != null ? GUI.MakeLabel(m_processedSkeleton.joints.Count.ToString()) : GUI.MakeLabel("-"));
      EditorGUILayout.EndHorizontal();
     
      var setFactor = EditorGUILayout.DelayedFloatField(GUI.MakeLabel("Agressiveness"), m_aggressiveness);
      if(setFactor != m_aggressiveness)
      {
        m_aggressiveness = setFactor;
        HandleSelectedObject(m_selectedMeshObject);
      }

      UseLongestPath = InspectorGUI.Toggle(GUI.MakeLabel("Longest Continous Path"), UseLongestPath);

      var applyCancelState = InspectorGUI.PositiveNegativeButtons(m_selectedMeshObject != null && m_cachedSkeleton != null && m_processedSkeleton?.joints.Count > 0,
                                                                   "Apply",
                                                                   "Apply current configuration.",
                                                                   "Cancel");

      if (applyCancelState == InspectorGUI.PositiveNegativeResult.Positive)
      {

        Undo.SetCurrentGroupName($"Generating route from mesh \"{m_selectedMesh.name}\"");
        var undoGroupId = Undo.GetCurrentGroup();

        if (!Preview && m_cachedSkeleton == null)
          SkeletoniseSelectedMesh();


        Route.Clear();        
        float avgRadius = 0;

        //This is using the dfs iterator
        //Using cached skeleton, replace the current route with new nodes corresponding to the skeletons joints using the forward edges as direction
        dfs_iterator currJoint = m_processedSkeleton.begin();

        while (!currJoint.isEnd())
        {
          avgRadius += (float)currJoint.radius;
          Quaternion localRotation;
          if (currJoint.adjJoints.Count != 2 && Route.NumNodes > 0)
          {            
              localRotation = Route[Route.NumNodes - 1].LocalRotation;            
          }
          else
          {
            localRotation = Quaternion.FromToRotation(Vector3.forward, (currJoint.next_joint().position - currJoint.position).ToHandedVector3());
          }
          CreateNode(currJoint.position.ToHandedVector3(), localRotation);
          currJoint.inc();
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
        
        m_cachedSkeleton = null;        
        m_selectedMesh = null;

  
        MeshRenderer renderer;
        if (m_selectedMeshObject.TryGetComponent(out renderer))
        {
          Undo.RegisterCompleteObjectUndo(renderer, "Disable mesh renderer");
          renderer.enabled = false;
        }
          

        m_selectedMeshObject = null;
        Undo.CollapseUndoOperations(undoGroupId);

        PerformRemoveFromParent();
      }
      else if (applyCancelState == InspectorGUI.PositiveNegativeResult.Negative)
        PerformRemoveFromParent();

      InspectorGUI.OnDropdownToolEnd();
    }

    private void updateSkeleton(double miniumumResolution = float.NegativeInfinity)
    {
        m_cachedSkeleton = m_skeletoniser.getSkeleton(miniumumResolution);
        m_processedSkeleton = UseLongestPath ? agxUtil.agxUtilSWIG.getLongestContinousSkeletonSegment(m_cachedSkeleton) : new SphereSkeleton(m_cachedSkeleton);
        m_cachedSkeletonSegments = m_cachedSkeleton.segmentSkeleton();
        m_processedSkeletonAvgRadius = 0;
        foreach (var joint in m_processedSkeleton.joints)
        {
          m_processedSkeletonAvgRadius += joint.radius;
        }
        m_processedSkeletonAvgRadius /= m_processedSkeleton.joints.Count;
    }

    const int VERTEX_WARNING_THRESHOLD = 100_000;

    private bool HandleSelectedObject(GameObject selected)
    {
      if (!selected)
        return false;
      DisplayVertexCountWarning = false;

      MeshFilter foundFilter;

      if(!selected.TryGetComponent<MeshFilter>(out foundFilter))
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
        //Warning if mesh is very large        
        if (Preview && mesh.vertexCount > VERTEX_WARNING_THRESHOLD)
        {
          Preview = false;
          DisplayVertexCountWarning = true;
        }        
        m_selectedMeshObject = selected;
        m_selectedMesh = mesh;
        m_skeletoniser = null;
        m_cachedSkeleton = null;
        m_processedSkeleton = null;
        if (Preview)
        {
          SkeletoniseSelectedMesh();
        }
        return true;
      }
      return false;
    }

    private void SkeletoniseSelectedMesh()
    {
      GameObject temp = new();
      temp.transform.localScale = m_selectedMeshObject.transform.lossyScale;
      m_mergedSelectedMesh = MeshMerger.Merge(temp.transform, new Mesh[] { m_selectedMesh });
      UnityEngine.Object.DestroyImmediate(temp);
      m_skeletoniser = new SphereSkeletoniser(m_mergedSelectedMesh.Vertices, m_mergedSelectedMesh.Indices);
      m_skeletoniser.setFaceDevalueFactor(Mathf.Pow(10, m_aggressiveness));
      m_skeletoniser.collapseUntilSkeleton();

      updateSkeleton();
    }

    void LineHandleCap(int controlID, Vector3 position, Quaternion rotation, float thickness, float length, EventType eventType)
    {
      Vector3 scale = new Vector3(thickness, thickness, length);
      Matrix4x4 originalMatrix = Handles.matrix;
      Handles.matrix = Handles.matrix * Matrix4x4.TRS(position, rotation, scale);
      Handles.CylinderHandleCap(controlID, Vector3.zero, Quaternion.identity, 1.0f, eventType);
      Handles.matrix = originalMatrix;
    }

    private void DrawRoutePreview()
    {
      Event e = Event.current;
      UnityEngine.Color jointColor, edgeColor;
      if (e.shift && m_processedSkeleton.joints.Count > 2) jointColor = UnityEngine.Color.red;
      else if (e.control) jointColor = UnityEngine.Color.green;
      else jointColor = UnityEngine.Color.blue;

      edgeColor = UnityEngine.Color.blue;

      using (new Handles.DrawingScope(Matrix4x4.Translate(m_selectedMeshObject.transform.position) * Matrix4x4.Rotate(m_selectedMeshObject.transform.rotation)))
      {
        using (new Handles.DrawingScope(jointColor))
        {
          float radius = UseFixedRadius ? m_fixedRadius : (float)m_processedSkeletonAvgRadius;
          float diameter = radius * 2;
          var currJoint = m_processedSkeleton.begin();
          SphereSkeleton.Joint prevJoint = null;
          while (!currJoint.isEnd())
          {  
            if(!e.shift && !e.control)
            {
              Handles.SphereHandleCap(0, currJoint.position.ToHandedVector3(), Quaternion.identity, diameter, EventType.Repaint);
            } 
            else if (Handles.Button(currJoint.position.ToHandedVector3(), Quaternion.identity, diameter, radius, Handles.SphereHandleCap))
            {
              if (e.shift && m_processedSkeleton.joints.Count > 2) //Remove joint
              {
                agxUtilSWIG.removeJointFromSkeleton(m_processedSkeleton, currJoint.index, m_mergedSelectedMesh.Vertices);
                break;
              } else if (e.control) //Upscale at joint
              {
                break;
              }           
            }

            if(prevJoint != null)
            {
              using (new Handles.DrawingScope(edgeColor))
              {
                //if(e.control)
                //{
                //  Vector3 start = prevJoint.position.ToHandedVector3();
                //  Vector3 direction = (currJoint.position - prevJoint.position).ToHandedVector3();
                //  float length = direction.magnitude;

                //  Vector3 midPoint = start + direction * 0.5f;
                //  if (Handles.Button(midPoint, Quaternion.LookRotation(direction), diameter / 10, radius / 10, (int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType) =>
                //  {
                //    LineHandleCap(controlID, position, rotation, size, length, eventType);
                //  }))
                //  {
                //    Debug.Log("Hej");
                //  }
                //} else
                //{
                //  Handles.DrawLine(prevJoint.position.ToHandedVector3(), currJoint.position.ToHandedVector3(), 3);
                //}

                Handles.DrawLine(prevJoint.position.ToHandedVector3(), currJoint.position.ToHandedVector3(), 3);

                if(e.control)
                {
                  Vector3 start = prevJoint.position.ToHandedVector3();
                  Vector3 direction = (currJoint.position - prevJoint.position).ToHandedVector3();
                  Vector3 midPoint = start + direction * 0.5f;
                  
                  if (Handles.Button(midPoint, Quaternion.LookRotation(direction), radius, radius, Handles.CubeHandleCap))
                  {
                    Debug.Log("Hej");
                  }                  
                }
              }
            }
            prevJoint = currJoint.deref();
            currJoint.inc();
          }                
        }
      }
    }

    void DrawSkeleton()
    {
      using (new Handles.DrawingScope(Matrix4x4.Translate(m_selectedMeshObject.transform.position) * Matrix4x4.Rotate(m_selectedMeshObject.transform.rotation)))
      {
        using (new Handles.DrawingScope(UnityEngine.Color.green))
        {          
          foreach(var joint in m_processedSkeleton.joints)
          {
            Handles.SphereHandleCap(0, joint.position.ToHandedVector3(), Quaternion.identity, (float)joint.radius * 2, EventType.Repaint);
            using (new Handles.DrawingScope(UnityEngine.Color.red))
            {
              foreach (var adjIdx in joint.adjJoints)
              {
                Handles.DrawLine(m_processedSkeleton.joints[(int)adjIdx].position.ToHandedVector3(), joint.position.ToHandedVector3(), 4);
              }
            }              
          }
        }
      }
    }
  }
}
