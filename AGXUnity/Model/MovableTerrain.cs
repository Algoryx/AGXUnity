using AGXUnity.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.Model
{
  public abstract class MovableAdapter : DeformableTerrainBase
  {
    public sealed override float ElementSize { get => ElementSizeGetter; }
    protected abstract float ElementSizeGetter { get; }
  }

  [AddComponentMenu( "AGXUnity/Model/Movable Terrain" )]
  [RequireComponent( typeof( MeshFilter ) )]
  [RequireComponent( typeof( MeshRenderer ) )]
  [DisallowMultipleComponent]
  public class MovableTerrain : MovableAdapter
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

    [HideInInspector]
    public RigidBody RigidBody
    {
      get
      {
        Component obj = this;
        while(obj != null ) {
          var rb = obj.GetComponent<RigidBody>();
          if ( rb != null ) return rb;
          obj = obj.transform.parent;
        }
        return null;
      }
    }

    [SerializeField]
    private List<DeformableTerrainShovel> m_shovels = new List<DeformableTerrainShovel>();

    [SerializeField]
    private float m_elementSize = 0.2f;

    /// <summary>
    ///  The size of each underlying tile in the terrain, in meters.
    /// </summary>
    [ClampAboveZeroInInspector]
    public new float ElementSize
    {
      get => m_elementSize;
      set
      {
        m_elementSize = value;
        SetupMesh();
      }
    }

    [SerializeField]
    private int m_width = 21;
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
    private int m_height = 21;
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

      SetEnable( isActiveAndEnabled );

      return true;
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

      var rb = RigidBody;
      if ( rb != null )
        RigidBody.GetInitialized<RigidBody>().Native.add( Native.getGeometry(), GetTerrainOffset() );
      else
        Native.setTransform( GetTerrainOffset() * new agx.AffineMatrix4x4( transform.rotation.ToHandedQuat(),
                                                                           transform.position.ToHandedVec3() ) );
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

          uvs[ y * Width + x ].x = ( x - Width / 2 ) * ElementSize;
          uvs[ y * Width + x ].y = ( y - Height / 2 ) * ElementSize;

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
      m_terrainVertices = vertices;
      TerrainMesh.sharedMesh.uv = uvs;
      TerrainMesh.sharedMesh.SetIndices( indices, MeshTopology.Triangles, 0 );
      TerrainMesh.sharedMesh.RecalculateNormals();
    }

    private void UpdateHeights( agxTerrain.ModifiedVerticesVector modifiedVertices )
    {
      if ( modifiedVertices.Count == 0 )
        return;

      for ( int i = 0; i < modifiedVertices.Count; i++ ) {
        var mod = modifiedVertices[i];
        int idx = (int)(mod.y * Width + mod.x) + 1;

        float height = (float)Native.getHeight( mod );
        m_terrainVertices[ Width * Height - idx ].y = height;
      }

      TerrainMesh.mesh.vertices = m_terrainVertices;
      TerrainMesh.mesh.RecalculateNormals();
    }

    private agx.AffineMatrix4x4 GetTerrainOffset()
    {
      double offset = ElementSize*0.5;
      agx.AffineMatrix4x4 terrainOffset =
        agx.AffineMatrix4x4.translate( new agx.Vec3( Width % 2 == 0 ? offset : 0.0, Height % 2 == 0 ? offset : 0.0, 0.0 ) ) *
        agx.AffineMatrix4x4.rotate( agx.Vec3.Z_AXIS(), agx.Vec3.Y_AXIS() );

      var rb = RigidBody;
      if ( rb == null )
        return terrainOffset;
      // Using the world position of the shape - which includes scaling etc.
      var shapeInWorld = new agx.AffineMatrix4x4( transform.rotation.ToHandedQuat(),
                                                  transform.position.ToHandedVec3() );
      var rbInWorld    = new agx.AffineMatrix4x4( rb.transform.rotation.ToHandedQuat(),
                                                  rb.transform.position.ToHandedVec3() );
      return terrainOffset * shapeInWorld * rbInWorld.inverse();
    }


    private Vector3[] m_terrainVertices = null;
    private MeshFilter m_terrain = null;

    // -----------------------------------------------------------------------------------------------------------
    // ------------------------------- Implementation of DeformableTerrainBase -----------------------------------
    // -----------------------------------------------------------------------------------------------------------

    protected override float ElementSizeGetter => ElementSize;
    public override DeformableTerrainShovel[] Shovels => m_shovels.ToArray();
    public override agx.GranularBodyPtrArray GetParticles() { return Native?.getSoilSimulationInterface().getSoilParticles(); }
    public override agxTerrain.SoilSimulationInterface GetSoilSimulationInterface() { return Native?.getSoilSimulationInterface(); }
    public override agxTerrain.TerrainProperties GetProperties() { return Native?.getProperties(); }

    public override bool Add( DeformableTerrainShovel shovel )
    {
      if ( shovel == null || m_shovels.Contains( shovel ) )
        return false;

      m_shovels.Add( shovel );

      // Initialize shovel if we're initialized.
      if ( Native != null )
        Native.add( shovel.GetInitialized<DeformableTerrainShovel>().Native );

      return true;
    }

    public override bool Remove( DeformableTerrainShovel shovel )
    {
      if ( shovel == null || !m_shovels.Contains( shovel ) )
        return false;

      if ( Native != null )
        Native.remove( shovel.Native );

      return m_shovels.Remove( shovel );
    }
    public override bool Contains( DeformableTerrainShovel shovel )
    {
      return shovel != null && m_shovels.Contains( shovel );
    }

    public override void RemoveInvalidShovels()
    {
      m_shovels.RemoveAll( shovel => shovel == null );
    }

    public override void ConvertToDynamicMassInShape( Collide.Shape failureVolume )
    {
      if ( Native != null )
        Native.convertToDynamicMassInShape( failureVolume.GetInitialized<Collide.Shape>().NativeShape );
    }

    protected override bool IsNativeNull() { return Native == null; }
    protected override void SetShapeMaterial( agx.Material material, agxTerrain.Terrain.MaterialType type ) { Native.setMaterial( material, type ); }
    protected override void SetTerrainMaterial( agxTerrain.TerrainMaterial material ) { Native.setTerrainMaterial( material ); }
    protected override void SetEnable( bool enable )
    {
      if ( Native == null )
        return;

      if ( Native.getEnable() == enable )
        return;

      Native.setEnable( enable );
      Native.getGeometry().setEnable( enable );
    }


  }
}