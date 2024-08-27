using AGXUnity;
using AGXUnity.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

[RequireComponent( typeof( Cable ) )]
[ExecuteAlways]
public class SkinnedCableRenderer : ScriptComponent
{
  private Cable m_Cable;
  private SkinnedMeshRenderer m_renderer;

  public Mesh SourceMesh;
  public Material Material;

  private Mesh m_skinned;

  private List<Transform> m_bones;

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

  // Start is called before the first frame update
  override protected void OnEnable()
  {
    base.OnEnable();
    if ( !Application.isPlaying || SourceMesh == null)
      return;
    var start = DateTime.Now;
    m_Cable = GetComponent<Cable>().GetInitialized();    
    if ( !gameObject.TryGetComponent<SkinnedMeshRenderer>( out m_renderer ) )
      m_renderer = gameObject.AddComponent<SkinnedMeshRenderer>();

    var cableIt = m_Cable.Native.begin();
    var cableEnd = m_Cable.Native.end();

    m_bones = new List<Transform>();

    int i = 0;
    while ( !cableIt.EqualWith( cableEnd ) ) {
      var startPos = cableIt.getBeginPosition();
      var endPos = cableIt.getEndPosition();

      var rot = cableIt.getRigidBody().getRotation().ToHandedQuaternion();

      var b = new GameObject("Bone " + i);
      b.transform.parent = transform;
      b.transform.SetPositionAndRotation( startPos.ToHandedVector3(), Quaternion.FromToRotation( Vector3.forward, ( endPos - startPos ).ToHandedVector3() ) );
      b.transform.SetPositionAndRotation(startPos.ToHandedVector3(), rot);
      m_bones.Add( b.transform );
      cableIt.inc();
      i++;
    }

    BoneWeight[] boneWeights = new BoneWeight[SourceMesh.vertexCount];
    Vector3[] bonePos = m_bones.Select(b => b.localPosition).ToArray();
    Vector3[] verts = SourceMesh.vertices;
    Vector3[] normals = SourceMesh.normals;
    var objTransform = transform.localToWorldMatrix;
    Vector3 lastNodePosition = m_Cable.Route.Last().Position;
    Parallel.For( 0, SourceMesh.vertexCount, ( i ) =>
    {
      Vector3 v = verts[i];
      //Find the bone edge with the lowest perpendicular distance to the vertex and choose the two bones as weighting.
      //If the last node from the original route happens to be closer than any bone, makes sure the vertex is weighted by the last two segments/bones (this is to avoid artifacts from the cable being shortened by the routign algorithm).      
      var idx = m_bones.Count-2;      
      float minDist = Vector3.Distance(lastNodePosition, v);

      for ( int j = 0; j < bonePos.Length-1; j++ ) {
        var toBone = bonePos[j] - v;        
        //Weight the distance by the similarity of the distance vector to the vertex anti-normal
        Vector3 vertexNormal = normals[i];
        float distanceWeight = (Mathf.PI - Mathf.Acos(Vector3.Dot(-vertexNormal.normalized, toBone.normalized))) / Mathf.PI;
        //float dist = toBone.magnitude - distanceWeight * m_Cable.Radius * 2 ; 
        float dist = perpendicularDistance(v, bonePos[j], bonePos[j+1] - bonePos[j], new Vector2(0,1));

        if ( dist < minDist ) {
          minDist = dist;
          idx = j;
        }
      }      

      boneWeights[ i ].boneIndex0 = idx;
      boneWeights[ i ].boneIndex1 = idx + 1;
      boneWeights[ i ].weight0 = Mathf.Pow( 1.0f/Mathf.Max( ( bonePos[ idx ] - v ).magnitude, 0.01f ), 2 );
      boneWeights[ i ].weight1 = Mathf.Pow( 1.0f/Mathf.Max( ( bonePos[ idx + 1 ] - v ).magnitude, 0.01f ), 2 );
    } );

    m_skinned = new Mesh();
    m_skinned.vertices = verts;
    m_skinned.normals = SourceMesh.normals;
    m_skinned.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
    m_skinned.triangles = SourceMesh.triangles;
    m_skinned.boneWeights = boneWeights;
    m_skinned.bindposes = m_bones.Select( b => b.worldToLocalMatrix * transform.localToWorldMatrix ).ToArray();

    m_renderer.bones = m_bones.ToArray();
    m_renderer.quality = SkinQuality.Bone2;
    m_renderer.sharedMesh = m_skinned;
    if ( Material == null )
      m_renderer.sharedMaterial = new Material( Shader.Find( "Standard" ) );
    else
      m_renderer.sharedMaterial = Material;
    Debug.Log( $"Cable init took {( DateTime.Now - start ).TotalSeconds:F2}s" );

    Simulation.Instance.StepCallbacks.PostStepForward += Post;
  }

  // Update is called once per frame
  void Update()
  {
    if (!Application.isPlaying && SourceMesh != null)
    {      
      for (int i = 0; i < SourceMesh.subMeshCount; i++)
        Graphics.RenderMesh(new RenderParams(Material), SourceMesh, i, transform.localToWorldMatrix);
    }  
  }

  private void OnDrawGizmos()
  {
    if (!SourceMesh)
      return;
    Gizmos.color = Color.clear;
    if (!Application.isPlaying && SourceMesh != null)
      for (int i = 0; i < SourceMesh.subMeshCount; i++)
        Gizmos.DrawMesh(SourceMesh, i, transform.position, transform.rotation, transform.localScale);
  }

  void Post()
  {
    var cableIt = m_Cable.Native.begin();
    var cableEnd = m_Cable.Native.end();

    int i = 0;
    var bounds = new Bounds(m_renderer.transform.InverseTransformPoint(cableIt.getBeginPosition().ToHandedVector3()), new Vector3(m_Cable.Diameter, m_Cable.Diameter, m_Cable.Diameter));
    while ( !cableIt.EqualWith( cableEnd ) ) {
      var startPos = cableIt.getBeginPosition();
      var endPos = cableIt.getEndPosition();

      var b = m_bones[i];
      b.transform.SetPositionAndRotation( startPos.ToHandedVector3(), Quaternion.FromToRotation( Vector3.forward, ( endPos - startPos ).ToHandedVector3() ) );
      cableIt = cableIt.inc();
      bounds.Encapsulate( m_renderer.transform.InverseTransformPoint(b.transform.position));
      i++;
    }
    
    m_renderer.localBounds = bounds;
  }
}
