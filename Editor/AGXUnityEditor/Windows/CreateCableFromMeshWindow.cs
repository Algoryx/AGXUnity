using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using AGXUnity;
using AGXUnity.Utils;

using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Windows
{
  public class CreateCableFromMeshWindow : EditorWindow
  {
    public MeshFilter MeshFilter = null;
    public float RadiusGuess = 0.1f;
    public float ResolutionGuess = 10;

    public float GizmoSize = 0.05f;

    public bool DrawVertices = true;

    private List<Vector3> m_movedVertexPositions = new List<Vector3>();
    private List<Vector3> m_nodePositions = new List<Vector3>();

    private MeshFilter m_previousMesh = null;
    private float m_previousResolutionGuess = Mathf.NegativeInfinity;

    private float m_previousRadiusGuess = Mathf.NegativeInfinity;
    private bool m_rendererInitiallyVisible = true;

    private static Mesh m_mesh = null;
    private static Material m_material = null;

    private static GameObject m_instance = null;

    private string m_errorMessage = "";

    private bool m_sorted = false;

    private float m_calculatedRadius = 0;
    
    public static UnityEngine.Mesh GetOrCreateGizmoMesh(string resourceName)
    {
      if (m_mesh != null)
        return m_mesh;

      GameObject tmp = Resources.Load<GameObject>(@resourceName);
      m_instance = tmp;
      MeshFilter[] filters = tmp.GetComponentsInChildren<MeshFilter>();
      CombineInstance[] combine = new CombineInstance[filters.Length];

      for (int i = 0; i < filters.Length; ++i)
      {
        combine[i].mesh = filters[i].sharedMesh;
        combine[i].transform = filters[i].transform.localToWorldMatrix;
      }

      MeshRenderer[] renderers = tmp.GetComponentsInChildren<MeshRenderer>();
      m_material = new Material(renderers[0].sharedMaterial);

      m_mesh = new UnityEngine.Mesh();
      m_mesh.CombineMeshes(combine);

      return m_mesh;
    }

    private static void DrawMeshGizmo(string resourceName, Color color, Vector3 position, Quaternion rotation, Vector3 scale)
    {
      Matrix4x4 matrixTRS = Matrix4x4.TRS(position, rotation * Quaternion.FromToRotation(Vector3.up, Vector3.forward), scale);
      UnityEngine.Mesh mesh = GetOrCreateGizmoMesh(resourceName);
      if (mesh == null)
        return;

      m_material.color = color;
      m_material.SetPass(0);

      var cam = Camera.main;
      
      Graphics.DrawMeshNow(mesh, matrixTRS);
    }

    public static void Open()
    {
      var window = EditorWindow.GetWindowWithRect<CreateCableFromMeshWindow>( new Rect( 300, 300, 400, 400 ),
                                                                              true,
                                                                              "Create Cable from mesh" );
     
      SceneView.duringSceneGui += window.OnSceneGUI;
    }

    void OnGUI()
    {
      EditorGUILayout.LabelField( GUI.MakeLabel( "Detect and create cable from Mesh", true ),
                                  InspectorEditor.Skin.LabelMiddleCenter );
                                
      EditorGUILayout.SelectableLabel( "When a MeshFilter is selected, this utility will try to detect appropriate placements for the cable nodes in order to simulate a cable with the same radius and route as the mesh. Adjust the guess values until the red spheres meet and the green spheres are along the cable route, then click Create. The created cable can be adjusted with Rigidbody attachments, cable properties etc.",
                                       InspectorEditor.Skin.LabelWordWrap, GUILayout.MinHeight(100) );


      MeshFilter = (MeshFilter) EditorGUILayout.ObjectField(MeshFilter, typeof(MeshFilter), true);

      if (GUILayout.Button("Toggle mesh renderer visibility"))
        ToggleMeshVisibility(MeshFilter);

      InspectorGUI.BrandSeparator( 1, 6 );

      GUILayout.Label("Cable detection values", EditorStyles.boldLabel);
      RadiusGuess = EditorGUILayout.FloatField("Radius guess", RadiusGuess);
      ResolutionGuess = EditorGUILayout.FloatField("Resolution guess", ResolutionGuess);
      GizmoSize = EditorGUILayout.FloatField("Gizmo size", GizmoSize);
      DrawVertices = EditorGUILayout.Toggle("Draw Vertices", DrawVertices);

      if (m_previousRadiusGuess != RadiusGuess || m_previousResolutionGuess != ResolutionGuess)
      {
        CalculatePositions();
        m_previousResolutionGuess = ResolutionGuess;
        m_previousRadiusGuess = RadiusGuess;
      }
      else if (m_previousMesh != MeshFilter)
      {
        if (m_previousMesh != null)
          RestoreMesh(m_previousMesh);

        if (MeshFilter != null)
        {
          var renderer = MeshFilter.gameObject.GetComponent<MeshRenderer>();
          if (renderer != null)
            m_rendererInitiallyVisible = renderer.enabled;
        }

        CalculatePositions();

        m_previousMesh = MeshFilter;
      }

      InspectorGUI.BrandSeparator( 1, 6 );

      var fieldColor = EditorGUIUtility.isProSkin ?
                         Color.white :
                         Color.black;

      bool cableOk = m_nodePositions.Count > 0 && m_sorted;

      InspectorGUI.SelectableTextField( GUI.MakeLabel( "Number of cable nodes" ),
                                        m_nodePositions.Count.ToString().Color( fieldColor ),
                                        InspectorEditor.Skin.Label );
      InspectorGUI.SelectableTextField( GUI.MakeLabel( "Calculated radius" ),
                                        m_calculatedRadius.ToString().Color( fieldColor ),
                                        InspectorEditor.Skin.Label );
      InspectorGUI.SelectableTextField( GUI.MakeLabel( "Are the nodes ordered?" ),
                                        ((cableOk) ? "yes" : "no").Color( cableOk ? Color.green : Color.red ),
                                        InspectorEditor.Skin.Label );
      InspectorGUI.SelectableTextField( GUI.MakeLabel( "Mesh vertices" ),
                                        m_movedVertexPositions.Count.ToString().Color( fieldColor ),
                                        InspectorEditor.Skin.Label );

      InspectorGUI.BrandSeparator( 1, 6 );

      if (GUILayout.Button("Create"))
        OnCreate();

      if (m_errorMessage.Length > 0)
        InspectorGUI.SelectableTextField( GUI.MakeLabel( "Error:" ),
                                        m_errorMessage.Color( Color.red ),
                                        InspectorEditor.Skin.Label );
    }

    private Color m_originalColor;

    private void ToggleMeshVisibility(MeshFilter mesh)
    {
      if (!mesh)
        return;

      var renderer = mesh.gameObject.GetComponent<MeshRenderer>();
      if (renderer != null)
        renderer.enabled = !renderer.enabled;
    }

    private void RestoreMesh(MeshFilter mesh)
    {
      if (!mesh)
        return;

      var renderer = mesh.gameObject.GetComponent<MeshRenderer>();
      if (renderer != null)
        renderer.enabled = m_rendererInitiallyVisible;
    }

    void OnCreate()
    {
      m_errorMessage = "";
      if (!m_sorted)
        m_errorMessage = "Cable nodes seems unordered!";
      else if (MeshFilter == null)
        m_errorMessage = "No Mesh Filter component assigned!";
      else if (m_nodePositions.Count < 2)
        m_errorMessage = "Need more than one cable route point!";

      if (m_errorMessage.Length > 0)
        return;

      var oldObject = MeshFilter.gameObject;

      GameObject go = Factory.Create<Cable>();
      go.name = oldObject.name + " - agxCable";
      go.transform.SetParent(oldObject.transform.parent);

      var cable = go.GetComponent<Cable>();
      cable.Radius = m_calculatedRadius > 0 ? m_calculatedRadius : 0.1f;
      Vector3 dir = Vector3.zero;
      for (int i = 0; i < m_nodePositions.Count; i++)
      {
        dir = (i < m_nodePositions.Count - 1) ? m_nodePositions[i + 1] - m_nodePositions[i] : dir;
        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up); // Vector3.Cross(dir, Vector3.left)
        cable.Route.Add((i == 0 || i == m_nodePositions.Count - 1) ? Cable.NodeType.BodyFixedNode : Cable.NodeType.FreeNode, go, m_nodePositions[i], rot);
      }

      var newCableRenderer = go.AddComponent<AGXUnity.Rendering.CableRenderer>();
      var meshRenderer = MeshFilter.GetComponent<MeshRenderer>();
      if (meshRenderer != null)
      {
        List<Material> materials = new List<Material>();
        meshRenderer.GetSharedMaterials(materials);
        if (materials.Count > 0)
          newCableRenderer.Material = materials[0];
      }

      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "Create Cable from Mesh Window" );

      Undo.RecordObject( oldObject, "Disable Cable Mesh Object" );
      oldObject.SetActive(false);

      this.Close();
    }

    void OnDestroy()
    {
      SceneView.duringSceneGui -= this.OnSceneGUI; 

      RestoreMesh(m_previousMesh);
    }

    void OnSceneGUI(SceneView view)
    { 

      if (DrawVertices)
        foreach (var pos in m_movedVertexPositions)
          DrawMeshGizmo("Debug/SphereRenderer", Color.red, pos, Quaternion.identity, Vector3.one * GizmoSize * 0.7f);

      for (int i = 0; i < m_nodePositions.Count; i++)
      {
        var color = new Color(0.5f, (float)i / m_nodePositions.Count, 0.4f, 1f);
        DrawMeshGizmo("Debug/SphereRenderer", color, m_nodePositions[i], Quaternion.identity, Vector3.one * GizmoSize * 2f);
      }
    }

    private List<Vector3> AveragePositions(List<Vector3> searchPositions)
    {
      var nodePositions = new List<Vector3>();
      Vector3 position = Vector3.zero;
      float searchRange = 1 / ResolutionGuess;
      searchRange *= searchRange;
      int n = 0;
      while (searchPositions.Count > 1) // at least two vertices left
      {
        var origin = searchPositions[0];
        position = origin;
        searchPositions.RemoveAt(0);
        n = 1;
        for (int i = searchPositions.Count - 1; i >= 0; i--)
        {
          if ((searchPositions[i] - origin).sqrMagnitude < searchRange)
          {
            position += searchPositions[i];
            n++;
            searchPositions.RemoveAt(i);
          }
        }

        nodePositions.Add(position / (float)n);
      }
      
      return nodePositions;
    }

    // Checks if list is sorted by seeing if the cable "bends" by more than 90 degrees, essentially
    private bool IsSorted(List<Vector3> list)
    {
      // Check if sorted
      bool sorted = true;
      if (list.Count > 2)
      {
        Vector3 previousDir = list[1] - list[0];
        for (int i = 1; i < list.Count - 1; i++)
        {
          Vector3 dir = list[i + 1] - list[i];
          if (Vector3.Dot(dir, previousDir) < 0)
            sorted = false;
          previousDir = dir;
        }
      }
      else if (list.Count < 2)
      {
        sorted = false;
      }
      return sorted;
    }

    private void CalculatePositions()
    {
      m_movedVertexPositions.Clear();
      m_nodePositions.Clear();
      m_calculatedRadius = 0;

      if (MeshFilter == null)
        return;

      var mesh = MeshFilter.sharedMesh;

      var vertices = mesh.vertices;
      var normals = mesh.normals;

      var meshTransform = MeshFilter.gameObject.transform;

      List<Vector3> transformedVertices = new List<Vector3>();
      List<Vector3> transformedNormals = new List<Vector3>();
      List<float> radiusGuesses = new List<float>();
      foreach (var vertex in vertices)
        transformedVertices.Add(meshTransform.TransformPoint(vertex));
      foreach (var normal in normals)
        transformedNormals.Add(meshTransform.TransformDirection(normal));

      // First, we want a guess of node positions and then move each vertex close to those guesses backwards
      var searchPositions = transformedVertices.GetRange(0, transformedVertices.Count); // Copy
      var searchNormals = transformedNormals.GetRange(0, transformedNormals.Count);
      var firstPositions = new List<Vector3>();
      float searchRange = 1 / ResolutionGuess;
      searchRange *= searchRange;
      int n = 0;
      List<Vector3> matches = new List<Vector3>();
      List<Vector3> matchNormals = new List<Vector3>();

      while (searchPositions.Count > 1) // at least two vertices left
      {
        matches.Clear();
        matchNormals.Clear();
        var origin = searchPositions[0];
        Vector3 position = origin;

        matches.Add(position);
        matchNormals.Add(searchNormals[0]);
        searchPositions.RemoveAt(0);
        searchNormals.RemoveAt(0);
        n = 1;

        for (int j = searchPositions.Count - 1; j >= 0; j--)
        {
          if ((searchPositions[j] - origin).sqrMagnitude < searchRange)
          {
            matches.Add(searchPositions[j]);
            matchNormals.Add(searchNormals[j]);
            position += searchPositions[j];
            searchPositions.RemoveAt(j);
            searchNormals.RemoveAt(j);
            n++;
          }
        }

        Vector3 newPosition = position / (float)n;
        firstPositions.Add(newPosition);
        for (int i = 0; i < matches.Count; i++)
        {
          Vector3 direction = matches[i] - newPosition;
          float magnitude = direction.magnitude;
          float sign = Mathf.Sign(Vector3.Dot(matchNormals[i], direction));
          m_movedVertexPositions.Add(matches[i] - sign * RadiusGuess * matchNormals[i]);
          // If the normal of the point is pointing straight away from the node we add the distance to the radius guess list
          if (sign > 0 && Vector3.Cross(direction, matchNormals[i]).magnitude < 0.05f * magnitude)
            radiusGuesses.Add(magnitude);
        }
      }

      m_nodePositions = AveragePositions(m_movedVertexPositions.GetRange(0, m_movedVertexPositions.Count));

      // TODO iterate once more with new node positions for moving nodes

      // Check if sorted
      m_sorted = IsSorted(m_nodePositions);

      if (!m_sorted)
      {
        List<Vector3> sortedNodes = new List<Vector3>();
        sortedNodes.Add(m_nodePositions[0]); // TODO possibly allow to choose starting node
        Vector3 current = m_nodePositions[0];
        m_nodePositions.RemoveAt(0);
        while (m_nodePositions.Count > 1)
        {
          float minDistance = float.MaxValue;
          int closest = -1;
          for (int j = 0; j < m_nodePositions.Count; j++)
          {
            float distance = (m_nodePositions[j] - current).magnitude;
            if (distance < minDistance)
            {
              minDistance = distance;
              closest = j;
            }
          }
          current = m_nodePositions[closest];
          m_nodePositions.RemoveAt(closest);
          sortedNodes.Add(current);
        }
        sortedNodes.Add(m_nodePositions[0]);
        m_nodePositions = sortedNodes;

        m_sorted = IsSorted(m_nodePositions);
      }

      // Calculate the cable radius by taking the median guess from above
      if (radiusGuesses.Count > 1)
      {
        radiusGuesses.Sort();
        m_calculatedRadius = radiusGuesses[(int)((float)radiusGuesses.Count * 0.5f)];
      }
      else
        m_calculatedRadius = 0.1f;
    }
  }
}