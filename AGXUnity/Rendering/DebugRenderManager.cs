using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity.Rendering
{
  /// <summary>
  /// Debug render manager singleton object, managing debug render data.
  /// This object is active in editor.
  /// </summary>
  [AddComponentMenu( "" )]
  [ExecuteInEditMode]
  public class DebugRenderManager : UniqueGameObject<DebugRenderManager>
  {
    public struct ContactData
    {
      public Vector3 Point;
      public Vector3 Normal;
    }

    /// <summary>
    /// BaseEditor.cs is calling this method when the editor receives
    /// an OnDestroy call and the application isn't playing. This
    /// behavior is assumed to be a "select -> delete".
    /// </summary>
    /// <param name="gameObject"></param>
    public static void OnEditorDestroy()
    {
      if ( !HasInstance )
        return;

      List<GameObject> gameObjectsToDestroy = new List<GameObject>();
      foreach ( var node in Instance.Children ) {
        OnSelectionProxy selectionProxy = node.GetComponent<OnSelectionProxy>();
        if ( selectionProxy != null )
          gameObjectsToDestroy.Add( selectionProxy.gameObject );
      }

      while ( gameObjectsToDestroy.Count > 0 ) {
        DestroyImmediate( gameObjectsToDestroy[ gameObjectsToDestroy.Count - 1 ] );
        gameObjectsToDestroy.RemoveAt( gameObjectsToDestroy.Count - 1 );
      }
    }

    /// <summary>
    /// Callback from Collide.Shape when the size of a shape has been changed.
    /// </summary>
    /// <param name="shape"></param>
    public static void SynchronizeScale( Collide.Shape shape )
    {
      if ( !IsActiveForSynchronize )
        return;

      Instance.SynchronizeScaleIfNodeExist( shape );
    }

    /// <summary>
    /// Use when Collide.Mesh source objects is updated.
    /// </summary>
    public static void HandleMeshSource( Collide.Mesh mesh )
    {
      if ( !IsActiveForSynchronize )
        return;

      Instance.SynchronizeShape( mesh );
    }

    /// <summary>
    /// Called after Simulation.StepForward from shapes without rigid bodies.
    /// </summary>
    public static void OnPostSynchronizeTransforms( Collide.Shape shape )
    {
      if ( !IsActiveForSynchronize )
        return;

      Instance.SynchronizeShape( shape );
    }

    /// <summary>
    /// Callback from Shape.OnDisable to catch and find disabled shapes,
    /// disabling debug render node.
    /// </summary>
    public static void OnShapeDisable( Collide.Shape shape )
    {
      if ( !IsActiveForSynchronize )
        return;

      var data = shape.GetComponent<ShapeDebugRenderData>();
      if ( data.Node != null )
        data.Node.SetActive( false );
    }

    /// <summary>
    /// Called after Simulation.StepForward from bodies to synchronize debug rendering of the shapes.
    /// </summary>
    public static void OnPostSynchronizeTransforms( RigidBody rb )
    {
      if ( rb == null || !IsActiveForSynchronize )
        return;

      foreach ( var shape in rb.Shapes )
        Instance.SynchronizeShape( shape );
    }

    /// <summary>
    /// Callback from Simulation when a full update has been executed.
    /// This method collects contact data if "render contacts" is enabled.
    /// </summary>
    /// <param name="simulation">Simulation instance.</param>
    public static void OnActiveSimulationPostStep( agxSDK.Simulation simulation )
    {
      if ( !IsActiveForSynchronize )
        return;

      Instance.OnSimulationPostStep( simulation );
    }

    /// <summary>
    /// Debug render shapes enabled toggle.
    /// </summary>
    [SerializeField]
    private bool m_renderShapes = true;

    /// <summary>
    /// Toggle to enable/disable debug rendering of shapes.
    /// </summary>
    [HideInInspector]
    public bool RenderShapes
    {
      get { return m_renderShapes; }
      set
      {
        if ( !enabled )
          return;

        m_renderShapes = value;
        SetShapesVisible( m_renderShapes );
        UpdateIsActiveForSynchronize();
      }
    }

    /// <summary>
    /// Material used by the shapes.
    /// </summary>
    [SerializeField]
    private Material m_shapeRenderMaterial = null;

    /// <summary>
    /// Instance of shape debug render material used by all debug rendered shapes.
    /// </summary>
    [HideInInspector]
    public Material ShapeRenderMaterial
    {
      get
      {
        if ( m_shapeRenderMaterial == null )
          m_shapeRenderMaterial = PrefabLoader.Instantiate<Material>( "Materials/DebugRendererMaterial" );
        return m_shapeRenderMaterial;
      }
      set
      {
        if ( m_shapeRenderMaterial == value )
          return;

        m_shapeRenderMaterial = value;
        var renderers = GetComponentsInChildren<Renderer>();
        foreach ( var renderer in renderers )
          renderer.sharedMaterial = m_shapeRenderMaterial;
      }
    }

    /// <summary>
    /// Render contacts toggle.
    /// </summary>
    [SerializeField]
    private bool m_renderContacts = true;

    /// <summary>
    /// Toggle to enable/disable debug rendering of contacts.
    /// </summary>
    [HideInInspector]
    public bool RenderContacts
    {
      get { return m_renderContacts; }
      set
      {
        if ( !enabled )
          return;

        m_renderContacts = value;
        if ( !m_renderContacts )
          m_contactList.Clear();
      }
    }

    /// <summary>
    /// Color of the rendered contact points.
    /// </summary>
    [HideInInspector]
    public Color ContactColor = new Color( 0.75f, 0.25f, 0.25f, 1.0f );

    [HideInInspector]
    public float ContactScale = 0.2f;

    /// <summary>
    /// Visualizes shapes and visuals in bodies with different colors (wire frame gizmos).
    /// </summary>
    [HideInInspector]
    public bool ColorizeBodies = false;

    /// <summary>
    /// Highlights the shape or visual the mouse is currently hovering in the scene view.
    /// </summary>
    [HideInInspector]
    public bool HighlightMouseOverObject = false;

    /// <summary>
    /// Toggle to include debug rendering in build or not. Default "do not include in build".
    /// </summary>
    [SerializeField]
    private bool m_includeInBuild = false;

    /// <summary>
    /// Enable/disable debug rendering in builds. Default false ("do not include in build").
    /// </summary>
    [HideInInspector]
    public bool IncludeInBuild
    {
      get { return m_includeInBuild; }
      set
      {
        m_includeInBuild = value;
        if ( m_includeInBuild )
          gameObject.hideFlags = HideFlags.None;
        else
          gameObject.hideFlags = HideFlags.DontSaveInBuild;

        transform.hideFlags = gameObject.hideFlags | HideFlags.NotEditable;
      }
    }

    [System.NonSerialized]
    private GameObject m_root = null;

    [HideInInspector]
    public GameObject Root
    {
      get
      {
        if ( m_root == null )
          m_root = RuntimeObjects.GetOrCreateRoot( this );
        return m_root;
      }
    }

    private IEnumerable<GameObject> Children
    {
      get
      {
        if ( Root == null )
          yield break;

        foreach ( Transform child in Root.transform )
          yield return child.gameObject;
      }
    }

    /// <summary>
    /// Contact list built after each simulation step if debug render of contacts is enabled.
    /// </summary>
    private List<ContactData> m_contactList = new List<ContactData>();

    /// <summary>
    /// Get current contact points.
    /// </summary>
    public IEnumerable<ContactData> ContactList { get { return m_contactList; } }

    protected override bool Initialize()
    {
      gameObject.hideFlags = HideFlags.None;

      return base.Initialize();
    }

    protected override void OnEnable()
    {
      SetShapesVisible( RenderShapes );

      base.OnEnable();

      UpdateIsActiveForSynchronize();
    }

    protected override void OnDisable()
    {
      RuntimeObjects.RegisterAsPotentiallyDeleted( this );

      if ( !RuntimeObjects.IsDestroyed )
        SetShapesVisible( false );

      base.OnDisable();

      UpdateIsActiveForSynchronize();
    }

    protected void Update()
    {
      UpdateIsActiveForSynchronize();

      // When the application is playing we rely on callbacks
      // from the objects when they've synchronized their
      // transforms.
      if ( Application.isPlaying )
        return;

      // Shapes with inactive game objects will be updated below when we're
      // traversing all children.
      FindObjectsOfType<Collide.Shape>().ToList().ForEach(
        shape => SynchronizeShape( shape )
      );

      FindObjectsOfType<Constraint>().ToList().ForEach(
        constraint => constraint.AttachmentPair.Synchronize()
      );

      List<GameObject> gameObjectsToDestroy = new List<GameObject>();
      foreach ( var node in Children ) {
        OnSelectionProxy proxy = node.GetComponent<OnSelectionProxy>();

        if ( proxy == null )
          continue;

        if ( proxy.Target == null )
          gameObjectsToDestroy.Add( node );
        // FindObjectsOfType will not include the Shape if its game object is inactive.
        // We're handling that shape here instead.
        else if ( !proxy.Target.activeInHierarchy && proxy.Component is Collide.Shape )
          SynchronizeShape( proxy.Component as Collide.Shape );
      }

      while ( gameObjectsToDestroy.Count > 0 ) {
        DestroyImmediate( gameObjectsToDestroy.Last() );
        gameObjectsToDestroy.RemoveAt( gameObjectsToDestroy.Count - 1 );
      }
    }

    [HideInInspector]
    public static bool IsActiveForSynchronize { get; private set; } = false;

    private bool UpdateIsActiveForSynchronize()
    {
      return ( IsActiveForSynchronize = gameObject.activeInHierarchy && enabled );
    }

    private void SynchronizeShape( Collide.Shape shape )
    {
      var data = shape.gameObject.GetOrCreateComponent<ShapeDebugRenderData>();
      bool shapeEnabled = shape.IsEnabledInHierarchy;

      if ( data.hideFlags != HideFlags.HideInInspector )
        data.hideFlags = HideFlags.HideInInspector;

      // Do not create debug render data if the shape is inactive.
      if ( !shapeEnabled && data.Node == null )
        return;

      data.Synchronize( this );
      if ( data.Node != null && ( RenderShapes && shapeEnabled ) != data.Node.activeSelf )
        data.Node.SetActive( RenderShapes && shapeEnabled );
    }

    private void SynchronizeScaleIfNodeExist( Collide.Shape shape )
    {
      var data = shape.GetComponent<ShapeDebugRenderData>();
      if ( data != null )
        data.SynchronizeScale( shape );
    }

    private void SetShapesVisible( bool visible )
    {
      foreach ( var child in Children )
        child.SetActive( visible );
    }

    private void OnSimulationPostStep( agxSDK.Simulation simulation )
    {
      if ( simulation == null )
        return;

      // Only collect data for contacts if they are enabled
      if ( m_renderContacts ) {
        var gcs = simulation.getSpace().getGeometryContacts();

        m_contactList.Clear();
        m_contactList.Capacity = System.Math.Max( m_contactList.Capacity, 4 * gcs.Count );

        for ( int i = 0; i < gcs.Count; ++i ) {
          var gc = gcs[ i ];
          if ( gc.isEnabled() ) {
            var contactPoints = gc.points();
            for ( uint j = 0; j < contactPoints.size(); ++j ) {
              var contactPoint = contactPoints.at( j );
              if ( contactPoint.enabled ) {
                m_contactList.Add( new ContactData()
                {
                  Point = contactPoint.point.ToHandedVector3(),
                  Normal = contactPoint.normal.ToHandedVector3()
                } );
              }
              contactPoint.ReturnToPool();
            }
            contactPoints.ReturnToPool();
          }
          gc.ReturnToPool();
        }
      }
    }
  }
}
