using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using AGXUnity;

namespace AGXUnityEditor
{
  public class CreateCableFromMeshWindow : EditorWindow
  {
    public float ResolutionGuess = 10;
    public float RadiusGuess = 0.1f;

    public float GizmoSize = 0.05f;

    public MeshFilter MeshFilter = null;

    private List<Vector3> m_movedVertexPositions = new List<Vector3>();
    private List<Vector3> m_nodePositions = new List<Vector3>();

    private MeshFilter m_previousMesh = null;
    private float m_previousResolutionGuess = Mathf.NegativeInfinity;

    private float m_previousRadiusGuess = Mathf.NegativeInfinity;

    private static Mesh m_mesh = null;
    private static Material m_material = null;

    private static GameObject m_instance = null;

    private bool m_sorted = false;
    
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
      var window = EditorWindow.GetWindowWithRect<CreateCableFromMeshWindow>( new Rect( 300, 300, 400, 350 ),
                                                   true,
                                                   "Create Cable from mesh" );
      SceneView.duringSceneGui += window.OnSceneGUI;
    }

    void OnGUI()
    {
      GUILayout.Label("Base Settings", EditorStyles.boldLabel);
      ResolutionGuess = EditorGUILayout.FloatField("Resolution guess", ResolutionGuess);
      RadiusGuess = EditorGUILayout.FloatField("Radius guess", RadiusGuess);
      GizmoSize = EditorGUILayout.FloatField("Gizmo size", GizmoSize);
      MeshFilter = (MeshFilter) EditorGUILayout.ObjectField(MeshFilter, typeof(MeshFilter), true);

      if (GUILayout.Button("Create"))
        OnWizardCreate();

      if (m_previousRadiusGuess != RadiusGuess || m_previousResolutionGuess != ResolutionGuess)
      {
        CalculatePositions();
        m_previousResolutionGuess = ResolutionGuess;
        m_previousRadiusGuess = RadiusGuess;
      }
      else if (m_previousMesh != MeshFilter)
      {
        //InitMesh(MeshFilter);
        CalculatePositions();
        //if (m_previousMesh != null)
        //  RestoreMesh(m_previousMesh);
        m_previousMesh = MeshFilter;
      }

      GUILayout.Label("Number of nodes: " + m_nodePositions.Count, EditorStyles.label);
      GUILayout.Label("Nodes in order: " + ((m_nodePositions.Count > 0) ? m_sorted.ToString() : "-"), EditorStyles.label);
    }

    private Color m_originalColor;

    private void InitMesh(MeshFilter mesh)
    {
      var renderer = mesh.gameObject.GetComponent<MeshRenderer>();
      if (renderer != null)
      {
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        m_originalColor = block.GetColor("_Color");
        Debug.Log("Saving color: " + m_originalColor);
        //block.SetColor("_Color", new Color(m_originalColor.r, m_originalColor.g, m_originalColor.b, 0.3f));
        //renderer.SetPropertyBlock(block);
      }
    }

    private void RestoreMesh(MeshFilter mesh)
    {
      var renderer = mesh.gameObject.GetComponent<MeshRenderer>();
      if (renderer != null)
      {
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        Debug.Log("Returning color: " + m_originalColor);
        //block.SetColor("_Color", m_originalColor);
        //renderer.SetPropertyBlock(block);
      }
    }

    void OnWizardCreate()
    {
      if (!m_sorted || MeshFilter == null || m_nodePositions.Count < 3)
        return; // TODO warn user instead of closing guide

      var oldObject = MeshFilter.gameObject;

      GameObject go = Factory.Create<Cable>();
      go.name = oldObject.name + " - agxCable";
      go.transform.SetParent(oldObject.transform.parent);

      // TODO undo
      //if ( go != null )
      //  Undo.RegisterCreatedObjectUndo( go, "Cable from Mesh Wizard" );

      var cable = go.GetComponent<Cable>();
      cable.Radius = RadiusGuess; // TODO is this good enough? For the specific cable tested for we could conceive of analytically improved results...
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

      oldObject.SetActive(false);


      this.Close();

      //if (MeshFilter != null)
      //  RestoreMesh(MeshFilter);
    }

    void OnDestroy()
    {
      SceneView.duringSceneGui -= this.OnSceneGUI; 

      //if (MeshFilter != null)
      //  RestoreMesh(MeshFilter);
    }

    void OnSceneGUI(SceneView view)
    { 

      foreach (var pos in m_movedVertexPositions)
        DrawMeshGizmo("Debug/SphereRenderer", Color.red, pos, Quaternion.identity, Vector3.one * GizmoSize * 0.9f);

      for (int i = 0; i < m_nodePositions.Count; i++)
      {
        var color = new Color(0.5f, (float)i / m_nodePositions.Count, 0.4f, 1f);
        DrawMeshGizmo("Debug/SphereRenderer", color, m_nodePositions[i], Quaternion.identity, Vector3.one * GizmoSize);
      }
    }

    void OnWizardUpdate()
    {
      //helpString = "Helpful info on how this works, limitations etc";
    }

    private void CalculatePositions()
    {
      if (MeshFilter == null)
        return;

      var mesh = MeshFilter.sharedMesh;

      var vertices = mesh.vertices;
      var normals = mesh.normals;

      m_movedVertexPositions.RemoveRange(0, m_movedVertexPositions.Count);

      var transform = MeshFilter.gameObject.transform;

      Vector3 position = Vector3.zero;
      for (int i = 0; i < vertices.Length; i++)
      {
        position = transform.TransformPoint(vertices[i] - RadiusGuess * normals[i]);
        m_movedVertexPositions.Add(position);
      }

      m_nodePositions.RemoveRange(0, m_nodePositions.Count);
      var searchPositions = m_movedVertexPositions.GetRange(0, m_movedVertexPositions.Count); // Copy
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

        m_nodePositions.Add(position / (float)n);
      }

      // Check if sorted
      m_sorted = true;
      if (m_nodePositions.Count > 2)
      {
        Vector3 previousDir = m_nodePositions[1] - m_nodePositions[0];
        for (int i = 1; i < m_nodePositions.Count - 1; i++)
        {
          Vector3 dir = m_nodePositions[i + 1] - m_nodePositions[i];
          if (Vector3.Dot(dir, previousDir) < 0)
            m_sorted = false;
          previousDir = dir;
        }
      }
      Debug.Log("Sorted: " + m_sorted);

      // TODO what if not sorted? 
      // Probably start with the first node, find the closest node to that one, remove the first, start over, bit like the above algorithm...
    }
  }
}