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

    public GameObject[] Segments
    {
      get
      {
        return m_segments == null ? new GameObject[] {} :
               ( from segmentTransform in m_segments.GetComponentsInChildren<Transform>()
                 where segmentTransform != m_segments.transform
                 select segmentTransform.gameObject ).ToArray();
      }
    }

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

      GameObject instance = GetInstance();

      if ( instance == m_firstSegmentInstance ) {
        Transform top    = instance.transform.GetChild( 0 );
        Transform main   = instance.transform.GetChild( 1 );
        Transform bottom = instance.transform.GetChild( 2 );

        main.localScale                    = new Vector3( width, length, height );
        top.localScale = bottom.localScale = new Vector3( width, width, height );
        top.transform.localPosition        =  0.5f * length * Vector3.up;
        bottom.transform.localPosition     = -0.5f * length * Vector3.up;
      }
      else {
        Transform main = instance.transform.GetChild( 0 );
        Transform top  = instance.transform.GetChild( 1 );

        main.localScale             = new Vector3( width, length, height );
        top.localScale              = new Vector3( width, width, height );
        top.transform.localPosition = new Vector3( 0, 0.5f * length, 0 );
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
  }
}
