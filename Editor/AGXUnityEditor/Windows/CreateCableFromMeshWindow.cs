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
    public CableProperties Properties = null;
    public ShapeMaterial ShapeMaterial = null;
    public float RadiusGuess = 0.1f;
    public float ResolutionGuess = 10;
    public float DotProductRequirement = 0;

    public float GizmoSize = 0.05f;

    public bool DrawVertices = false;

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

    private float m_numEnhancements = 0;

    private float m_cableLength = 0;

    private float m_calculatedRadius = 0;

    // Average of dot products of vectors between adjacent node points
    float m_straightness = float.MaxValue; 

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
      var window = EditorWindow.GetWindowWithRect<CreateCableFromMeshWindow>( new Rect( 300, 300, 400, 600 ),
                                                                              true,
                                                                              "Create Cable from mesh" );
     
      SceneView.duringSceneGui += window.OnSceneGUI;
    }

    void OnGUI()
    {

      EditorGUILayout.LabelField( GUI.MakeLabel( "Detect and create cable from Mesh", true ),
                                  InspectorEditor.Skin.LabelMiddleCenter );

      if (EditorApplication.isPlaying)
      {
        EditorGUILayout.LabelField("This utility should not be used during play mode!");
        return;
      }

      EditorGUILayout.LabelField( "When a MeshFilter is selected, this utility will try to detect appropriate placements for the cable nodes in order to simulate a cable with the same radius and route as the mesh.",
                                       InspectorEditor.Skin.LabelWordWrap, GUILayout.MinHeight(50) );

      EditorGUILayout.LabelField( "Not guaranteed to work: relies on mesh vertices resembling a cable shape, the cable not too tightly rolled up etc.",
                                       InspectorEditor.Skin.LabelWordWrap, GUILayout.MinHeight(40) );

      EditorGUILayout.LabelField( "Select the mesh and press the Auto Enhance buttons a few times, if this doesn't find the cable shape you can try adjusting the guess values manually and then auto enhancing again.",
                                       InspectorEditor.Skin.LabelWordWrap, GUILayout.MinHeight(50) );



      EditorGUILayout.Space();

      MeshFilter = (MeshFilter) EditorGUILayout.ObjectField(MeshFilter, typeof(MeshFilter), true);

      EditorGUILayout.Space();

      Properties = (CableProperties) EditorGUILayout.ObjectField(Properties, typeof(CableProperties), true);
      ShapeMaterial = (ShapeMaterial) EditorGUILayout.ObjectField(ShapeMaterial, typeof(ShapeMaterial), true);

      if (GUILayout.Button("Toggle mesh renderer visibility"))
        ToggleMeshVisibility(MeshFilter);

      InspectorGUI.BrandSeparator( 1, 6 );

      GUILayout.Label("Cable detection values", EditorStyles.boldLabel);
      RadiusGuess = EditorGUILayout.FloatField("Radius guess", RadiusGuess);

      float guess = EditorGUILayout.FloatField("Resolution guess", ResolutionGuess);
      if (guess != ResolutionGuess)
      {
        m_numEnhancements = 0;
        ResolutionGuess = guess;
      }

      EditorGUILayout.LabelField( "Straightness requirement", InspectorEditor.Skin.LabelWordWrap);
      float dotProduct = EditorGUILayout.Slider(DotProductRequirement, -1f, 1f);
      if (DotProductRequirement != dotProduct)
      {
        DotProductRequirement = dotProduct;
        m_sorted = CheckCable(m_nodePositions);
      }

      if (GUILayout.Button("Enhance - Straighter"))
        Enhance();
      if (GUILayout.Button("Enhance - Longer"))
        Enhance(false);

      InspectorGUI.BrandSeparator( 1, 6 );

      GizmoSize = EditorGUILayout.FloatField("Gizmo size", GizmoSize);
      DrawVertices = EditorGUILayout.Toggle("Draw Debug Vertices", DrawVertices);

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

        Enhance();
        Enhance();
        Enhance();
        Enhance();

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
      InspectorGUI.SelectableTextField( GUI.MakeLabel( "Approximate length" ),
                                        m_cableLength.ToString().Color( fieldColor ),
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
      if (MeshFilter == null)
        m_errorMessage = "No Mesh Filter component assigned!";
      else if (!m_sorted)
        m_errorMessage = "Cable nodes seems unordered!";
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

      cable.Properties = Properties;
      cable.Material = ShapeMaterial;

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

      // Soft reset
      m_previousMesh = null;
      MeshFilter = null;
      RadiusGuess = m_previousRadiusGuess = 0.1f;
      ResolutionGuess = m_previousResolutionGuess = 10;
      m_numEnhancements = 0;
      m_sorted = false;
      m_nodePositions.Clear();
      m_movedVertexPositions.Clear();
      m_cableLength = m_calculatedRadius = 0;
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

    private List<Vector3> GetNodePositionsFromVertices(List<Vector3> vertexPositions)
    {
      var nodePositions = new List<Vector3>();
      Vector3 position = Vector3.zero;
      float searchRange = 1 / ResolutionGuess;
      searchRange *= searchRange;
      int n = 0;
      while (vertexPositions.Count > 1) // at least two vertices left
      {
        var origin = vertexPositions[0];
        position = origin;
        vertexPositions.RemoveAt(0);
        n = 1;
        for (int i = vertexPositions.Count - 1; i >= 0; i--)
        {
          if ((vertexPositions[i] - origin).sqrMagnitude < searchRange)
          {
            position += vertexPositions[i];
            n++;
            vertexPositions.RemoveAt(i);
          }
        }

        nodePositions.Add(position / (float)n);
      }
      
      return nodePositions;
    }


    // Checks if list is sorted by seeing if the cable "bends" by more than 90 degrees
    private bool CheckCable(List<Vector3> list)
    {
      bool sorted = true;
      float straightness = 0;
      m_cableLength = 0;
      if (list.Count > 2)
      {
        Vector3 previousDir = list[1] - list[0];
        m_cableLength += previousDir.magnitude;
        for (int i = 1; i < list.Count - 1; i++)
        {
          Vector3 dir = list[i + 1] - list[i];
          m_cableLength += dir.magnitude;
          float dot = Vector3.Dot(dir.normalized, previousDir.normalized);
          straightness += dot;
          if (dot < DotProductRequirement)
            sorted = false;
          previousDir = dir;
        }

        m_straightness = straightness / (list.Count - 2);
      }
      else if (list.Count < 2)
      {
        sorted = false;
        m_straightness = 0;
      }
      else // exactly 2 nodes
      {
        m_cableLength = (list[0] - list[1]).magnitude;
        m_straightness = 1;
      }

      return sorted;
    }

    private void GetUpdatedNodesAndVertices(out List<Vector3> nodesOut, List<Vector3> nodesIn, out List<Vector3> movedVertices, List<Vector3> originalVertices, List<Vector3> normals, out List<float> radiusGuesses)
    {
      movedVertices = new List<Vector3>();
      nodesOut = new List<Vector3>();
      radiusGuesses = new List<float>();
      
      float searchRange = 1 / ResolutionGuess;
      float searchRangeSqr = searchRange * searchRange;
      int n = 0;
      List<Vector3> matches = new List<Vector3>();
      List<Vector3> matchNormals = new List<Vector3>();
      nodesOut = new List<Vector3>();

      for (int i = 0; i < nodesIn.Count; i++)
      {
        matches.Clear();
        matchNormals.Clear();
        Vector3 position = Vector3.zero;
        n = 0;

        for (int j = originalVertices.Count - 1; j >= 0; j--)
        {
          if ((originalVertices[j] - nodesIn[i]).sqrMagnitude < searchRangeSqr)
          {
            matches.Add(originalVertices[j]);
            matchNormals.Add(normals[j]);
            position += originalVertices[j];
            originalVertices.RemoveAt(j);
            normals.RemoveAt(j);
            n++;
          }
        }

        if (n == 0)
          continue;

        Vector3 newPosition = position / (float)n;
        nodesOut.Add(newPosition);
        for (int k = 0; k < matches.Count; k++)
        {
          Vector3 direction = matches[k] - newPosition;
          float magnitude = direction.magnitude;
          float sign = Mathf.Sign(Vector3.Dot(matchNormals[k], direction));
          movedVertices.Add(matches[k] - sign * RadiusGuess * matchNormals[k]);
          // If the normal of the point is pointing straight away from the node we add the distance to the radius guess list
          if (sign > 0 && Vector3.Cross(direction, matchNormals[k]).magnitude < 0.05f * magnitude)
            radiusGuesses.Add(magnitude);
        }
      }
    }

    // Brute force-y try different values for resolution. This could be wildly more efficient but we don't need efficiency
    private void Enhance(bool straighter = true)
    {
      float previousRadius = RadiusGuess;
      float previousResolution = ResolutionGuess;
      float previousLength = m_sorted ? m_cableLength : 0f;

      float bestStraightness = m_straightness;
      float bestResolution = ResolutionGuess;

      
      List<float> guesses;
      if (m_numEnhancements == 0)
        guesses = new List<float> {ResolutionGuess / 5f, ResolutionGuess / 2f, ResolutionGuess / 1.5f, ResolutionGuess / 1.1f, ResolutionGuess * 1.1f, ResolutionGuess * 1.5f, ResolutionGuess * 2f, ResolutionGuess * 5f};
      else
      {
        guesses = new List<float>();
        for (float i = -0.3f; i < 0.34f; i += 0.05f)
          guesses.Add(ResolutionGuess + ResolutionGuess * i / m_numEnhancements);
      }

      foreach (var guess in guesses)
      {
        ResolutionGuess = guess;
        RadiusGuess = m_calculatedRadius;

        CalculatePositions(1);

        if (m_sorted && m_nodePositions.Count > 2 && (straighter ? m_straightness > bestStraightness : m_cableLength > previousLength))
        {
          bestResolution = guess;
          bestStraightness = m_straightness;
        }
      }

      ResolutionGuess = bestResolution;
      CalculatePositions();
      RadiusGuess = m_calculatedRadius;
      m_numEnhancements++;

      GizmoSize = m_calculatedRadius * 0.98f;
    }

    private void CalculatePositions(int iterations = 2)
    {
      m_errorMessage = "";

      m_movedVertexPositions.Clear();
      m_nodePositions.Clear();
      m_calculatedRadius = 0;

      if (MeshFilter == null)
        return;

      var mesh = MeshFilter.sharedMesh;
      var vertices = mesh.vertices;
      var normals = mesh.normals;
      var meshTransform = MeshFilter.gameObject.transform;

      List<float> radiusGuesses = new List<float>();

      List<Vector3> transformedVertices = new List<Vector3>();
      List<Vector3> transformedNormals = new List<Vector3>();
      foreach (var vertex in vertices)
        transformedVertices.Add(meshTransform.TransformPoint(vertex));
      foreach (var normal in normals)
        transformedNormals.Add(meshTransform.TransformDirection(normal));

      // Initial node guesses
      m_nodePositions = GetNodePositionsFromVertices(transformedVertices.GetRange(0, transformedVertices.Count));

      // Enhance
      for (int i = 0; i < iterations; i++)
      {
        GetUpdatedNodesAndVertices(
          out m_nodePositions, 
          m_nodePositions.GetRange(0, m_nodePositions.Count),
          out m_movedVertexPositions,
          transformedVertices.GetRange(0, transformedVertices.Count),
          transformedNormals.GetRange(0, transformedNormals.Count),
          out radiusGuesses);
      }

      // Check if sorted
      m_sorted = CheckCable(m_nodePositions);

      // If not, sort
      if (!m_sorted && m_nodePositions.Count > 1)
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

        m_sorted = CheckCable(m_nodePositions);
      }

      // Calculate the cable radius by taking the median guess from above
      if (radiusGuesses.Count > 1)
      {
        radiusGuesses.Sort();
        m_calculatedRadius = radiusGuesses[(int)(((float)radiusGuesses.Count) * 0.5f)];
      }
      else
        m_calculatedRadius = 0.1f;
    }
  }
}