using agx.extensions;
using AGXUnity;
using AGXUnity.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace AGXUnity.Rendering
{
  [AddComponentMenu("AGXUnity/Rendering/Skinned Cable Renderer")]
  [RequireComponent(typeof(Cable))]
  [ExecuteAlways]
  [HelpURL("https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#cable-rendering")]
  public class SkinnedCableRenderer : ScriptComponent
  {
    private Cable m_Cable;
    private SkinnedMeshRenderer m_renderer;

    [SerializeField]
    private Mesh m_sourceMesh;
    public Mesh SourceMesh
    {
      get { return m_sourceMesh; }
      set
      {
        if (value != m_sourceMesh)
        {
          //Clear transform when user changes source mesh manually (or programmatically)
          m_sourceMesh = value;
          m_initialized = false;
        }
      }
    }

    [HideInInspector]
    public Material Material
    {
      get {
        if(SharedMaterial != null)
        {
          var copiedMaterial = new Material(SharedMaterial);
          SharedMaterial = copiedMaterial;
          return copiedMaterial;
        }
        return null;
      }
      set
      {
        SharedMaterial = value;
      }
    }

    public Material SharedMaterial;

    private Mesh m_skinned;

    private List<Transform> m_bones;

    [SerializeField]
    private Material m_defaultMaterial;

    [SerializeField]
    private bool m_initialized = false;

    public void SetParameters(Mesh mesh, Material sharedMaterial)
    {
      m_sourceMesh = mesh;
      Material = sharedMaterial;
      m_initialized = false;
    }

    float perpendicularDistance(Vector3 point, Vector3 lineStart, Vector3 lineDirection, Vector2 interval)
    {
      Vector3 PV = point - lineStart;

      // Calculate at which value of t the point is closest to the line (where the vector V->projD_PV touches the line)
      float t = Vector3.Dot(PV, lineDirection) / lineDirection.sqrMagnitude;

      if (t >= interval.x && t <= interval.y)    // Projection is within the interval and the distance is the length of the perpendicular vector            
        return (PV - lineDirection * t).magnitude;
      else // Projection is outside the interval, return distance to closest endpoint
        return (point - (lineStart + (t < interval.x ? interval.x : interval.y) * lineDirection)).magnitude;
    }

    //Reset is called on componenet add/reset
    private void Reset()
    {
      Cable cable = GetComponent<Cable>();
      if (cable != null)
      {
        m_sourceMesh = cable.RouteMeshSource;
        Material = cable.RouteMeshMaterial;
      }
      m_defaultMaterial = new Material(Shader.Find("Standard"));
    }

    // OnEnable is called before the first frame update and on enables afterwards
    override protected void OnEnable()
    {
      base.OnEnable();

      if (m_initialized && m_renderer != null)
        m_renderer.enabled = true;

      if (m_initialized || !Application.isPlaying || SourceMesh == null)
        return;

      m_Cable = GetComponent<Cable>().GetInitialized();
      if (!gameObject.TryGetComponent<SkinnedMeshRenderer>(out m_renderer))
        m_renderer = gameObject.AddComponent<SkinnedMeshRenderer>();


      var cableIt = m_Cable.Native.begin();
      var cableEnd = m_Cable.Native.end();

      m_bones = new List<Transform>();

      int i = 0;
      while (!cableIt.EqualWith(cableEnd))
      {
        var startPos = cableIt.getBeginPosition();
        var endPos = cableIt.getEndPosition();

        var rot = cableIt.getRigidBody().getRotation().ToHandedQuaternion();

        var b = new GameObject("Bone " + i);
        b.hideFlags = HideFlags.HideInHierarchy;
        b.transform.parent = transform;
        b.transform.SetPositionAndRotation(startPos.ToHandedVector3(), rot);
        m_bones.Add(b.transform);
        cableIt.inc();
        i++;
      }

      BoneWeight[] boneWeights = new BoneWeight[SourceMesh.vertexCount];
      Vector3[] bonePos = m_bones.Select(b => b.localPosition).ToArray();
      Vector3[] verts = SourceMesh.vertices;
      Vector3[] normals = SourceMesh.normals;
      var objTransform = transform.localToWorldMatrix;
      Vector3 lastNodePosition = transform.InverseTransformPoint(m_Cable.Route.Last().Position);
      Parallel.For(0, SourceMesh.vertexCount, (i) =>
      {
        Vector3 v = verts[i];
        //Find the bone edge with the lowest perpendicular distance to the vertex and choose the two bones as weighting.
        //If the last node from the original route happens to be closer than any bone, makes sure the vertex is weighted by the last two segments/bones (this is to avoid artifacts from the cable being shortened by the routing algorithm).      
        var idx = m_bones.Count - 2;
        float minDist = Vector3.Distance(lastNodePosition, v);

        for (int j = 0; j < bonePos.Length - 1; j++)
        {
          var toBone = bonePos[j] - v;
          Vector3 vertexNormal = normals[i];
          float dist = perpendicularDistance(v, bonePos[j], bonePos[j + 1] - bonePos[j], new Vector2(0, 1));

          if (dist < minDist)
          {
            minDist = dist;
            idx = j;
          }
          else if (j > 0 && Mathf.Abs(dist - minDist) < 0.01 * m_Cable.Radius)
          {
            //Compare distances to j-1 and j+1 to find if (j, j+1) is a better choice than (j-1, j)
            if ((bonePos[j + 1] - v).magnitude > (bonePos[j - 1] - v).magnitude)
            {
              minDist = dist;
              idx = j;
            }
          }
        }

        boneWeights[i].boneIndex0 = idx;
        boneWeights[i].boneIndex1 = idx + 1;
        boneWeights[i].weight0 = Mathf.Pow(1.0f / Mathf.Max((bonePos[idx] - v).magnitude, 0.01f), 2);
        boneWeights[i].weight1 = Mathf.Pow(1.0f / Mathf.Max((bonePos[idx + 1] - v).magnitude, 0.01f), 2);
      });

      m_skinned = new Mesh();
      m_skinned.vertices = verts;
      m_skinned.normals = SourceMesh.normals;
      m_skinned.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
      m_skinned.triangles = SourceMesh.triangles;
      m_skinned.boneWeights = boneWeights;
      m_skinned.bindposes = m_bones.Select(b => b.worldToLocalMatrix * transform.localToWorldMatrix).ToArray();

      m_renderer.bones = m_bones.ToArray();
      m_renderer.quality = SkinQuality.Bone2;
      m_renderer.sharedMesh = m_skinned;
      if (SharedMaterial == null)
        m_renderer.sharedMaterial = m_defaultMaterial;
      else
        m_renderer.sharedMaterial = SharedMaterial;

      m_renderer.hideFlags = HideFlags.HideInInspector;

      Simulation.Instance.StepCallbacks.PostStepForward += Post;
      m_initialized = true;
    }

    protected override void OnDisable()
    {
      base.OnDisable();
      if (m_renderer != null)
        m_renderer.enabled = false;
    }

    // Update is called once per frame
    void Update()
    {
      if (!Application.isPlaying && SourceMesh != null)
      {
        for (int i = 0; i < SourceMesh.subMeshCount; i++)
          Graphics.RenderMesh(new RenderParams(SharedMaterial != null ? SharedMaterial : m_defaultMaterial), SourceMesh, i, transform.localToWorldMatrix);
      }
    }

    private void OnDrawGizmos()
    {
      if (!SourceMesh)
        return;
      Gizmos.color = Color.clear;
      if (!Application.isPlaying && SourceMesh != null)
        Gizmos.DrawMesh(SourceMesh, -1, transform.position, transform.rotation, transform.lossyScale);
    }

    void Post()
    {
      var cableIt = m_Cable.Native.begin();
      var cableEnd = m_Cable.Native.end();

      int i = 0;
      var bounds = new Bounds(m_renderer.transform.InverseTransformPoint(cableIt.getBeginPosition().ToHandedVector3()), new Vector3(m_Cable.Diameter, m_Cable.Diameter, m_Cable.Diameter));
      while (!cableIt.EqualWith(cableEnd))
      {
        var startPos = cableIt.getBeginPosition();
        var endPos = cableIt.getEndPosition();

        var b = m_bones[i];
        var rot = cableIt.getRigidBody().getRotation().ToHandedQuaternion();
        b.transform.SetPositionAndRotation(startPos.ToHandedVector3(), rot);
        cableIt = cableIt.inc();
        bounds.Encapsulate(m_renderer.transform.InverseTransformPoint(b.transform.position));
        i++;
      }

      m_renderer.localBounds = bounds;
    }
  }

}