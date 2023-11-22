using System;
using System.Linq;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity.Rendering
{
  [Serializable]
  public class SegmentSpawner
  {
    [SerializeField]
    private GameObject m_segmentInstance = null;
    [NonSerialized]
    private GameObject m_segments = null;
    [SerializeField]
    private int m_counter = 0;
    [SerializeField]
    private string m_prefabObjectPath = string.Empty;

    [SerializeField]
    private string m_separateFirstObjectPrefabPath = string.Empty;
    [SerializeField]
    private GameObject m_firstSegmentInstance = null;

    [SerializeField]
    private ScriptComponent m_parentComponent = null;

    [SerializeField]
    private Material m_material = null;

    public Material Material
    {
      get { return m_material ?? DefaultMaterial; }
      set
      {
        m_material = value;

        Action<GameObject> assignMaterial = ( go ) =>
        {
          MeshRenderer[] renderers = go.GetComponentsInChildren<MeshRenderer>();
          foreach ( var renderer in renderers )
            renderer.sharedMaterial = m_material;
        };

        if ( m_firstSegmentInstance != null )
          assignMaterial( m_firstSegmentInstance );
        if ( m_segmentInstance != null )
          assignMaterial( m_segmentInstance );

        if ( m_segments != null ) {
          GameObject.DestroyImmediate( m_segments );
          m_segments = null;
        }
      }
    }

    private Material m_defaultMaterial = null;
    public Material DefaultMaterial
    {
      get
      {
        if ( m_defaultMaterial != null )
          return m_defaultMaterial;

        GameObject go = Resources.Load<GameObject>( m_prefabObjectPath );
        if ( go == null )
          return null;

        MeshRenderer renderer = go.GetComponentInChildren<MeshRenderer>();
        if ( renderer != null )
          m_defaultMaterial = renderer.sharedMaterial;

        return m_defaultMaterial;
      }
    }

    public bool IsValid => m_parentComponent != null;

    public SegmentSpawner( ScriptComponent parentComponent, string prefabObjectPath, string separateFirstObjectPath = "" )
    {
      m_parentComponent = parentComponent;
      m_prefabObjectPath = prefabObjectPath;
      if ( separateFirstObjectPath != "" )
        m_separateFirstObjectPrefabPath = separateFirstObjectPath;
    }

    public void Initialize( GameObject parent )
    {
    }

    public void Destroy()
    {
      if ( m_segments == null )
        return;

      GameObject.DestroyImmediate( m_segments );

      m_firstSegmentInstance = null;
      m_segmentInstance = null;
      m_segments = null;
    }

    public void Begin()
    {
      m_counter = 0;
    }

    public GameObject CreateSegment( Vector3 start, Vector3 end, float radius )
    {
      return CreateSegment( start, end, 2f * radius, 2f * radius );
    }

    public GameObject CreateSegment( Vector3 start, Vector3 end, float width, float height )
    {
      Vector3 startToEnd = end - start;
      float length = startToEnd.magnitude;
      startToEnd /= length;
      if ( length < 0.0001f )
        return null;

      var instance     = GetInstance();
      Transform top    = null;
      Transform main   = null;
      Transform bottom = null;

      var topBottomScale = new Vector3( width, width, height );
      var mainScale      = new Vector3( width, length, height );
      var halfLengthUp   = 0.5f * length * Vector3.up;
      if ( m_firstSegmentInstance != null && m_counter == 1 ) {
        top    = instance.transform.GetChild( 0 );
        main   = instance.transform.GetChild( 1 );
        bottom = instance.transform.GetChild( 2 );
      }
      else {
        main = instance.transform.GetChild( 0 );
        top  = instance.transform.GetChild( 1 );
      }

      top.localPosition = halfLengthUp;
      top.localRotation = Quaternion.identity;
      top.localScale    = topBottomScale;

      main.localPosition = Vector3.zero;
      main.localRotation = Quaternion.identity;
      main.localScale    = mainScale;

      if ( bottom != null ) {
        bottom.localPosition = -halfLengthUp;
        bottom.localRotation = Quaternion.FromToRotation( Vector3.up, Vector3.down );
        bottom.localScale    = topBottomScale;
      }

      instance.transform.rotation = Quaternion.FromToRotation( Vector3.up, startToEnd );
      instance.transform.position = start + 0.5f * length * startToEnd;

      return instance;
    }

    public void End()
    {
      DestroyFrom( m_counter );
    }

    private GameObject GetInstance()
    {
      Action<GameObject, Material> setMaterialFunc = ( go, material ) =>
      {
        MeshRenderer[] renderers = go.GetComponentsInChildren<MeshRenderer>();
        foreach ( var renderer in renderers )
          renderer.sharedMaterial = material;
      };

      // Moving create parent m_segments from Initialize to here because the
      // editor will delete it when the user presses play then stop then "Undo".
      if ( m_segments == null ) {
        m_segments = RuntimeObjects.GetOrCreateRoot( m_parentComponent );
        if ( m_segments == null )
          return null;

        if ( m_segments.transform.childCount > 0 ) {
          if ( m_separateFirstObjectPrefabPath != string.Empty )
            m_firstSegmentInstance = m_segments.transform.GetChild( 0 ).gameObject;
          if ( m_firstSegmentInstance != null && m_segments.transform.childCount > 1 )
            m_segmentInstance = m_segments.transform.GetChild( 1 ).gameObject;
          else if ( m_firstSegmentInstance == null )
            m_segmentInstance = m_segments.transform.GetChild( 0 ).gameObject;
        }
      }

      if ( m_separateFirstObjectPrefabPath != string.Empty ) {
        if ( m_firstSegmentInstance == null ) {
          m_firstSegmentInstance = PrefabLoader.Instantiate<GameObject>( m_separateFirstObjectPrefabPath );
          m_firstSegmentInstance.hideFlags = HideFlags.DontSaveInEditor;
          m_firstSegmentInstance.transform.hideFlags = HideFlags.DontSaveInEditor;
          setMaterialFunc( m_firstSegmentInstance, Material );
          AddSelectionProxy( m_firstSegmentInstance );
          Add( m_firstSegmentInstance );
        }
      }

      // Push back new segment if there aren't enough segments already created.
      int currentChildCount = m_segments.transform.childCount;
      if ( m_counter == currentChildCount ) {
        if ( m_segmentInstance == null ) {
          m_segmentInstance = PrefabLoader.Instantiate<GameObject>( m_prefabObjectPath );
          m_segmentInstance.hideFlags = HideFlags.DontSaveInEditor;
          setMaterialFunc( m_segmentInstance, Material );
          AddSelectionProxy( m_segmentInstance );
          Add( m_segmentInstance );
        }
        else
          Add( GameObject.Instantiate( m_segmentInstance ) );
      }
      else if ( m_counter > currentChildCount )
        throw new Exception( "Internal counter is not in sync. Counter = " + m_counter + ", #children = " + currentChildCount );

      return m_segments.transform.GetChild( m_counter++ ).gameObject;
    }

    private void DestroyFrom( int index )
    {
      if ( m_segments == null )
        return;

      index = Mathf.Max( 0, index );
      while ( m_segments.transform.childCount > index )
        GameObject.DestroyImmediate( m_segments.transform.GetChild( m_segments.transform.childCount - 1 ).gameObject );
    }

    private void Add( GameObject child )
    {
      child.transform.parent = m_segments.transform;
    }

    private void AddSelectionProxy( GameObject instance )
    {
      instance.GetOrCreateComponent<OnSelectionProxy>().Component = m_parentComponent;
      foreach ( Transform child in instance.transform )
        child.gameObject.GetOrCreateComponent<OnSelectionProxy>().Component = m_parentComponent;
    }

    public void DrawGizmos( Vector3[] points, float radius, Color color )
    {
      if ( m_gizmosMeshes == null || m_gizmosMeshes.Length == 0 ) {
        var resource = Resources.Load<GameObject>( m_separateFirstObjectPrefabPath );
        m_gizmosMeshes = new Mesh[]
        {
          resource?.transform.GetChild( 0 ).GetComponent<MeshFilter>()?.sharedMesh,
          resource?.transform.GetChild( 1 ).GetComponent<MeshFilter>()?.sharedMesh,
          resource?.transform.GetChild( 2 ).GetComponent<MeshFilter>()?.sharedMesh
        };

        if ( Array.Find( m_gizmosMeshes, mesh => mesh == null ) )
          m_gizmosMeshes = new Mesh[] { };
      }

      if ( m_gizmosMeshes.Length != 3 || points.Length < 2 )
        return;

      var diameter    = 2.0f * radius;
      var worldMatrix = Matrix4x4.identity;
      Gizmos.color    = color;
      for ( int i = 1; i < points.Length; ++i ) {
        var begin      = points[ i - 1 ];
        var end        = points[ i ];
        var beginToEnd = end - begin;
        var length     = beginToEnd.magnitude;
        if ( length < 1.0E-4f )
          continue;

        beginToEnd /= length;

        worldMatrix = Matrix4x4.TRS( begin + 0.5f * length * beginToEnd,
                                     Quaternion.FromToRotation( Vector3.up, beginToEnd ),
                                     Vector3.one );
        Action<int, Vector3, Quaternion, Vector3> drawMesh = ( index,
                                                               localPosition,
                                                               localRotation,
                                                               localScale ) =>
        {
          Gizmos.matrix = worldMatrix * Matrix4x4.TRS( localPosition,
                                                       localRotation,
                                                       localScale );
          Gizmos.DrawWireMesh( m_gizmosMeshes[ index ] );
        };

        var halfLengthUp     = 0.5f * length * Vector3.up;
        var sphericalScale   = diameter * Vector3.one;
        var cylindricalScale = new Vector3( diameter, length, diameter );
        if ( i == 1 ) {
          drawMesh( 0,
                    halfLengthUp,
                    Quaternion.identity,
                    sphericalScale );
          drawMesh( 1,
                    Vector3.zero,
                    Quaternion.identity,
                    cylindricalScale );
          drawMesh( 2,
                    -halfLengthUp,
                    Quaternion.FromToRotation( Vector3.up, Vector3.down ),
                    sphericalScale );
        }
        else {
          drawMesh( 1,
                    Vector3.zero,
                    Quaternion.identity,
                    cylindricalScale );
          drawMesh( 2,
                    halfLengthUp,
                    Quaternion.identity,
                    sphericalScale );
        }
      }
    }

    [NonSerialized]
    private static Mesh[] m_gizmosMeshes = new Mesh[] { };
  }
}
