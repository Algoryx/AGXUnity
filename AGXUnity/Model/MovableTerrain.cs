using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.Model
{
  [AddComponentMenu( "AGXUnity/Model/Movable Terrain" )]
  [RequireComponent( typeof( MeshFilter ) )]
  [RequireComponent( typeof( MeshRenderer ) )]
  [DisallowMultipleComponent]
  public class MovableTerrain : AGXUnity.Collide.Shape, ITerrain
  {
    /// <summary>
    /// Native deformable terrain instance - accessible after this
    /// component has been initialized and is valid.
    /// </summary>
    public agxTerrain.Terrain Native { get; private set; } = null;

    /// <summary>
    /// Unity Terrain component.
    /// </summary>
    public MeshFilter TerrainMesh
    {
      get
      {
        return m_terrain == null ?
                 m_terrain = GetComponent<MeshFilter>() :
                 m_terrain;
      }
    }

    [SerializeField]
    private List<DeformableTerrainShovel> m_shovels = new List<DeformableTerrainShovel>();

    /// <summary>
    /// Shovels associated to this terrain.
    /// </summary>
    [HideInInspector]
    public DeformableTerrainShovel[] Shovels { get { return m_shovels.ToArray(); } }

    [SerializeField]
    private DeformableTerrainMaterial m_terrainMaterial = null;

    /// <summary>
    /// Terrain material associated to this terrain.
    /// </summary>
    [AllowRecursiveEditing]
    public DeformableTerrainMaterial TerrainMaterial
    {
      get { return m_terrainMaterial; }
      set
      {
        m_terrainMaterial = value;

        if ( Native != null ) {
          if ( m_terrainMaterial != null )
            Native.setTerrainMaterial( m_terrainMaterial.GetInitialized<DeformableTerrainMaterial>().Native );
          else
            Native.setTerrainMaterial( DeformableTerrainMaterial.CreateNative( "dirt_1" ) );
        }
      }
    }

    [SerializeField]
    private DeformableTerrainProperties m_properties = null;

    /// <summary>
    /// Terrain properties associated to this terrain.
    /// </summary>
    [AllowRecursiveEditing]
    public DeformableTerrainProperties Properties
    {
      get { return m_properties; }
      set
      {
        if ( Native != null && m_properties != null )
          m_properties.Unregister( this );

        m_properties = value;

        if ( Native != null && m_properties != null )
          m_properties.Register( this );
      }
    }

    [SerializeField]
    private float m_maximumDepth = 20.0f;

    /// <summary>
    /// Maximum depth, it's not possible to dig deeper than this value.
    /// This game object will be moved down MaximumDepth and MaximumDepth
    /// will be added to the heights.
    /// </summary>
    [IgnoreSynchronization]
    [ClampAboveZeroInInspector( true )]
    public float MaximumDepth
    {
      get { return m_maximumDepth; }
      set
      {
        if ( Native != null ) {
          Debug.LogWarning( "DeformableTerrain MaximumDepth: Value is used during initialization" +
                            " and cannot be changed when the terrain has been initialized.", this );
          return;
        }
        m_maximumDepth = value;
      }
    }

    [SerializeField]
    private float m_elementSize = 0.2f;

    /// <summary>
    ///  The size of each underlying tile in the terrain, in meters.
    /// </summary>
    [ClampAboveZeroInInspector]
    public float ElementSize
    {
      get => m_elementSize;
      set
      {
        m_elementSize = value;
        SetupMesh();
      }
    }

    [SerializeField]
    private int m_width = 10;
    /// <summary>
    /// The width of the terrain in number of elements.
    /// </summary>
    public int Width
    {
      get => m_width;
      set
      {
        m_width = value;
        SetupMesh();
      }
    }

    [SerializeField]
    private int m_height = 10;
    /// <summary>
    /// The height of the terrain in number of elements.
    /// </summary>
    public int Height
    {
      get => m_height;
      set
      {
        m_height = value;
        SetupMesh();
      }
    }

    /// <summary>
    /// Associate shovel instance to this terrain.
    /// </summary>
    /// <param name="shovel">Shovel instance to add.</param>
    /// <returns>True if added, false if null or already added.</returns>
    public bool Add( DeformableTerrainShovel shovel )
    {
      if ( shovel == null || m_shovels.Contains( shovel ) )
        return false;

      m_shovels.Add( shovel );

      // Initialize shovel if we're initialized.
      if ( Native != null )
        Native.add( shovel.GetInitialized<DeformableTerrainShovel>().Native );

      return true;
    }

    /// <summary>
    /// Disassociate shovel instance to this terrain.
    /// </summary>
    /// <param name="shovel">Shovel instance to remove.</param>
    /// <returns>True if removed, false if null or not associated to this terrain.</returns>
    public bool Remove( DeformableTerrainShovel shovel )
    {
      if ( shovel == null || !m_shovels.Contains( shovel ) )
        return false;

      if ( Native != null )
        Native.remove( shovel.Native );

      return m_shovels.Remove( shovel );
    }

    /// <summary>
    /// Find if shovel has been associated to this terrain.
    /// </summary>
    /// <param name="shovel">Shovel instance to check.</param>
    /// <returns>True if associated, otherwise false.</returns>
    public bool Contains( DeformableTerrainShovel shovel )
    {
      return shovel != null && m_shovels.Contains( shovel );
    }

    /// <summary>
    /// Verifies so that all added shovels still exists. Shovels that
    /// has been deleted are removed.
    /// </summary>
    public void RemoveInvalidShovels()
    {
      m_shovels.RemoveAll( shovel => shovel == null );
    }

    protected override void OnEnable()
    {
      SetNativeEnable( true );
    }

    protected override bool Initialize()
    {
      // Only printing the errors if something is wrong.
      LicenseManager.LicenseInfo.HasModuleLogError( LicenseInfo.Module.AGXTerrain | LicenseInfo.Module.AGXGranular, this );

      RemoveInvalidShovels();

      InitializeNative();

      Simulation.Instance.StepCallbacks.PostStepForward += OnPostStepForward;

      // Native terrain may change the number of PPGS iterations to default (25).
      // Override if we have solver settings set to the simulation.
      if ( Simulation.Instance.SolverSettings != null )
        GetSimulation().getSolver().setNumPPGSRestingIterations( (ulong)Simulation.Instance.SolverSettings.PpgsRestingIterations );

      SetNativeEnable( isActiveAndEnabled );

      return true;
    }

    protected override void OnDisable()
    {
      SetNativeEnable( false );
    }

    protected override void OnDestroy()
    {
      if ( Properties != null )
        Properties.Unregister( this );

      if ( Simulation.HasInstance ) {
        GetSimulation().remove( Native );
        Simulation.Instance.StepCallbacks.PostStepForward -= OnPostStepForward;
      }
      Native = null;

      base.OnDestroy();
    }

    private void SetNativeEnable( bool enable )
    {
      if ( Native == null )
        return;

      if ( Native.getEnable() == enable )
        return;

      Native.setEnable( enable );
      Native.getGeometry().setEnable( enable );
    }

    private void InitializeNative()
    {

      var heights = new agx.RealVector((int)(Width * Height));
      heights.Set( new double[ Width * Height ] );

      Native = new agxTerrain.Terrain( (uint)Width,
                                       (uint)Height,
                                       ElementSize,
                                       heights,
                                       false,
                                       MaximumDepth );

      foreach ( var shovel in Shovels )
        Native.add( shovel.GetInitialized<DeformableTerrainShovel>()?.Native );

      GetSimulation().add( Native );

      m_geometry = Native.getGeometry();
      SetupMesh();
    }

    private void OnPostStepForward()
    {
      if ( Native == null )
        return;

      UpdateHeights( Native.getModifiedVertices() );
    }

    private void SetupMesh()
    {
      if ( Width * Height == 0 )
        return;
      if ( TerrainMesh.sharedMesh == null ) {
        TerrainMesh.sharedMesh = new Mesh();
        TerrainMesh.sharedMesh.name = "Terrain mesh";
        TerrainMesh.sharedMesh.MarkDynamic();
      }

      // Create a grid of vertices matching that of the undelying heightfield.
      var vertices = new Vector3[Width * Height];
      var uvs = new Vector2[Width * Height];
      var indices = new int[ ( Width - 1 ) * 6 * ( Height - 1 ) ];
      int i = 0;
      for ( var y = 0; y < Height; y++ ) {
        for ( var x = 0; x < Width; x++ ) {
          vertices[ y * Width + x ].x = ( x - Width / 2 ) * ElementSize;
          vertices[ y * Width + x ].z = ( y - Height / 2 ) * ElementSize;

          uvs[ y * Width + x ].x = ( x - Width / 2 ) * ElementSize * 0.5f;
          uvs[ y * Width + x ].y = ( y - Height / 2 ) * ElementSize * 0.5f;

          if ( x != Width - 1 && y != Height - 1 ) {
            indices[ i++ ] = y * Width + x;
            indices[ i++ ] = ( y + 1 ) * Width + x;
            indices[ i++ ] = ( y + 1 ) * Width + ( x + 1 );

            indices[ i++ ] = y * Width + x;
            indices[ i++ ] = ( y + 1 ) * Width + ( x + 1 );
            indices[ i++ ] = y * Width + ( x + 1 );
          }
        }
      }
      TerrainMesh.sharedMesh.Clear();
      TerrainMesh.sharedMesh.vertices = vertices;
      TerrainMesh.sharedMesh.uv = uvs;
      TerrainMesh.sharedMesh.SetIndices( indices, MeshTopology.Triangles, 0 );
      TerrainMesh.sharedMesh.RecalculateNormals();
    }

    private void UpdateHeights( agxTerrain.ModifiedVerticesVector modifiedVertices )
    {
      if ( modifiedVertices.Count == 0 )
        return;

      var vertices = TerrainMesh.mesh.vertices;

      foreach ( var mod in modifiedVertices ) {
        int idx = (int)(mod.y * Width + mod.x) + 1;

        float height = (float)Native.getHeight( mod );
        vertices[ Width * Height - idx ].y = height;
      }

      TerrainMesh.mesh.vertices = vertices;
      TerrainMesh.mesh.RecalculateNormals();
    }

    /// <summary>
    /// Transforms the native terrain to align with unity's coordinates, this operation performs a rotation from the Z-axis to the Y-axis as well
    /// as a conditional translation to account for positioning differences based on the evenness of the terrain dimensions
    /// </summary>
    /// <returns>An affine matrix representing the tranformation to apply to the terrain</returns>
    public override agx.AffineMatrix4x4 GetNativeGeometryOffset()
    {
      double offset = ElementSize*0.5;
      return
        agx.AffineMatrix4x4.translate( new agx.Vec3( Width % 2 == 0 ? offset : 0.0, Height % 2 == 0 ? offset : 0.0, 0.0 ) ) *
        agx.AffineMatrix4x4.rotate( agx.Vec3.Z_AXIS(), agx.Vec3.Y_AXIS() );
    }

    protected override agxCollide.Geometry CreateNative()
    {
      var heights = new agx.RealVector(new double[ Width * Height ]);
      var terr = new agxTerrain.Terrain((uint)Width,
                                        (uint)Height,
                                        ElementSize,
                                        heights,
                                        false,
                                        MaximumDepth );

      return terr.getGeometry();
    }

    public override Vector3 GetScale()
    {
      return new Vector3( 1, 1, 1 );
    }

    public agx.GranularBodyPtrArray GetParticles()
    {
      if ( Native == null ) return null;
      return Native.getSoilSimulationInterface().getSoilParticles();
    }

    public agxTerrain.TerrainProperties GetProperties()
    {
      return Native?.getProperties();
    }

    public void OnPropertiesUpdated()
    {
    }

    private MeshFilter m_terrain = null;
  }
}
