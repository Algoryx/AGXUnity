using AGXUnity.Collide;
using AGXUnity.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;
using GUI = AGXUnity.Utils.GUI;

namespace AGXUnity.Model
{
  [AddComponentMenu( "AGXUnity/Model/Deformable Terrain" )]
  [RequireComponent( typeof( Terrain ) )]
  [DisallowMultipleComponent]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#deformable-terrain" )]
  public class DeformableTerrain : DeformableTerrainBase
  {
    /// <summary>
    /// Native deformable terrain instance - accessible after this
    /// component has been initialized and is valid.
    /// </summary>
    public agxTerrain.Terrain Native { get; private set; } = null;

    /// <summary>
    /// Unity Terrain component.
    /// </summary>
    public Terrain Terrain
    {
      get
      {
        return m_terrain == null ?
                 m_terrain = GetComponent<Terrain>() :
                 m_terrain;
      }
    }

    /// <summary>
    /// Unity Terrain data.
    /// </summary>
    [HideInInspector]
    public TerrainData TerrainData { get { return Terrain?.terrainData; } }

    [HideInInspector]
    public int TerrainDataResolution { get { return TerrainUtils.TerrainDataResolution( TerrainData ); } }

    [SerializeField]
    private List<DeformableTerrainShovel> m_shovels = new List<DeformableTerrainShovel>();

    [SerializeField]
    private bool m_tempDisplayShovelForces = false;

    [HideInInspector]
    public bool TempDisplayShovelForces
    {
      get { return m_tempDisplayShovelForces; }
      set
      {
        m_tempDisplayShovelForces = value;

        if ( !Application.isPlaying )
          return;

        if ( m_tempDisplayShovelForces &&
             Shovels.Length > 0 &&
             GUIWindowHandler.Instance.GetWindowData( ShowForces ) == null ) {
          var windowSize = new Vector2( 750, 125 );
          GUIWindowHandler.Instance.Show( ShowForces,
                                          windowSize,
                                          new Vector2( Screen.width - windowSize.x - 20, 20 ),
                                          "Shovel forces" );
        }
        else if ( !m_tempDisplayShovelForces && GUIWindowHandler.HasInstance )
          GUIWindowHandler.Instance.Close( ShowForces );
      }
    }

    /// <summary>
    /// Resets heights of the Unity terrain and recreate native instance.
    /// </summary>
    public void ResetHeights()
    {
      ResetTerrainDataHeightsAndTransform();

      var nativeHeightData = TerrainUtils.WriteTerrainDataOffset( Terrain, MaximumDepth );
      transform.position = transform.position + MaximumDepth * Vector3.down;

      Native.setHeights( nativeHeightData.Heights );

      PropertySynchronizer.Synchronize( this );
    }

    /// <summary>
    /// If, e.g., OnDestroy wasn't called to reset the heights of the
    /// terrain this method can recover some data of the previous terrain
    /// height data. This method will subtract MaximumDepth from each
    /// entry in terrain data.
    /// </summary>
    public void PatchTerrainData()
    {
      TerrainUtils.WriteTerrainDataOffset( Terrain, -MaximumDepth );
    }

    protected override bool Initialize()
    {
      // Only printing the errors if something is wrong.
      LicenseManager.LicenseInfo.HasModuleLogError( LicenseInfo.Module.AGXTerrain | LicenseInfo.Module.AGXGranular, this );

      RemoveInvalidShovels( true, true );

      m_initialHeights = TerrainData.GetHeights( 0, 0, TerrainDataResolution, TerrainDataResolution );

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
      ResetTerrainDataHeightsAndTransform();

      if ( Properties != null )
        Properties.Unregister( this );

      if ( Simulation.HasInstance ) {
        GetSimulation().remove( Native );
        Simulation.Instance.StepCallbacks.PostStepForward -= OnPostStepForward;
      }
      Native = null;

      if ( GUIWindowHandler.HasInstance )
        GUIWindowHandler.Instance.Close( ShowForces );

      base.OnDestroy();
    }

    private void InitializeNative()
    {
      var nativeHeightData = TerrainUtils.WriteTerrainDataOffset( Terrain, MaximumDepth );

      transform.position = transform.position + MaximumDepth * Vector3.down;

      Native = new agxTerrain.Terrain( (uint)nativeHeightData.ResolutionX,
                                       (uint)nativeHeightData.ResolutionY,
                                       ElementSize,
                                       nativeHeightData.Heights,
                                       false,
                                       0.0f );

      Native.setTransform( Utils.TerrainUtils.CalculateNativeOffset( transform, TerrainData ) );


      foreach ( var shovel in Shovels )
        Native.add( shovel.GetInitialized<DeformableTerrainShovel>()?.Native );

      GetSimulation().add( Native );
    }

    private void ResetTerrainDataHeightsAndTransform()
    {
      if ( m_initialHeights == null )
        return;

      TerrainData.SetHeights( 0, 0, m_initialHeights );
      transform.position = transform.position + MaximumDepth * Vector3.up;

#if UNITY_EDITOR
      // If the editor is closed during play the modified height
      // data isn't saved, this resolves corrupt heights in such case.
      UnityEditor.EditorUtility.SetDirty( TerrainData );
      UnityEditor.AssetDatabase.SaveAssets();
#endif
    }

    private void OnPostStepForward()
    {
      if ( Native == null )
        return;

      UpdateHeights( Native.getModifiedVertices() );
    }

    private void UpdateHeights( agxTerrain.ModifiedVerticesVector modifiedVertices )
    {
      if ( modifiedVertices.Count == 0 )
        return;

      var scale  = TerrainData.heightmapScale.y;
      var resX   = TerrainDataResolution;
      var resY   = TerrainDataResolution;
      var result = new float[,] { { 0.0f } };
      foreach ( var index in modifiedVertices ) {
        var i = (int)index.x;
        var j = (int)index.y;
        var h = (float)Native.getHeight( index );

        result[ 0, 0 ] = h / scale;

        TerrainData.SetHeightsDelayLOD( resX - i - 1, resY - j - 1, result );
      }

#if UNITY_2019_1_OR_NEWER
      TerrainData.SyncHeightmap();
#else
      Terrain.ApplyDelayedHeightmapModification();
#endif
    }

    private GUIStyle m_textLabelStyle = null;
    private void ShowForces( EventType eventType )
    {
      if ( m_textLabelStyle == null ) {
        m_textLabelStyle = new GUIStyle( GUI.Skin.label );
        m_textLabelStyle.alignment = TextAnchor.MiddleLeft;

        var fonts = Font.GetOSInstalledFontNames();
        foreach ( var font in fonts ) {
          if ( font == "Consolas" ) {
            m_textLabelStyle.font = Font.CreateDynamicFontFromOSFont( font, 24 );
            break;
          }
        }
      }

      var textColor = Color.Lerp( Color.black, Color.white, 1.0f );
      var valueColor = Color.Lerp( Color.green, Color.white, 0.45f );
      Func<string, agx.Vec3, GUIContent> Vec3Content = ( name, v ) =>
      {
        return GUI.MakeLabel( string.Format( "{0} [{1}, {2}, {3}] kN",
                                             GUI.AddColorTag( name, textColor ),
                                             GUI.AddColorTag( v.x.ToString( "0.00" ).PadLeft( 7, ' ' ), valueColor ),
                                             GUI.AddColorTag( v.y.ToString( "0.00" ).PadLeft( 7, ' ' ), valueColor ),
                                             GUI.AddColorTag( v.z.ToString( "0.00" ).PadLeft( 7, ' ' ), valueColor ) ) );
      };

      var shovel = m_shovels[ 0 ].Native;
      var penetrationForce = new agx.Vec3();
      var penetrationTorque = new agx.Vec3();
      Native.getPenetrationForce( shovel, ref penetrationForce, ref penetrationTorque );
      var separationForce = -Native.getSeparationContactForce( shovel );
      var deformerForce = -Native.getDeformationContactForce( shovel );
      var contactForce = -Native.getContactForce( shovel );

      GUILayout.Label( Vec3Content( "Penetration force:", 1.0E-3 * penetrationForce ), m_textLabelStyle );
      GUILayout.Space( 4 );
      GUILayout.Label( Vec3Content( "Separation force: ", 1.0E-3 * separationForce ), m_textLabelStyle );
      GUILayout.Space( 4 );
      GUILayout.Label( Vec3Content( "Deformer force:   ", 1.0E-3 * deformerForce ), m_textLabelStyle );
      GUILayout.Space( 4 );
      GUILayout.Label( Vec3Content( "Contact force:    ", 1.0E-3 * contactForce ), m_textLabelStyle );
    }

    private Terrain m_terrain = null;
    private float[,] m_initialHeights = null;

    // -----------------------------------------------------------------------------------------------------------
    // ------------------------------- Implementation of DeformableTerrainBase -----------------------------------
    // -----------------------------------------------------------------------------------------------------------
    public override float ElementSize { get => TerrainData.size.x / ( TerrainDataResolution - 1 ); }
    public override DeformableTerrainShovel[] Shovels { get { return m_shovels.ToArray(); } }
    public override agx.GranularBodyPtrArray GetParticles() { return Native?.getSoilSimulationInterface()?.getSoilParticles(); }
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

    public override void RemoveInvalidShovels( bool removeDisabled = false, bool warn = false )
    {
      m_shovels.RemoveAll( shovel => shovel == null );
      if ( removeDisabled ) {
        int removed = m_shovels.RemoveAll( shovel => !shovel.isActiveAndEnabled );
        if ( removed > 0 ) {
          if ( warn )
            Debug.LogWarning( $"Removed {removed} disabled shovels from terrain {gameObject.name}." +
                              " Disabled shovels should not be added to the terrain on play and should instead be added manually when enabled during runtime." +
                              " To fix this warning, please remove any disabled shovels from the terrain." );
          else
            Debug.Log( $"Removed {removed} disabled shovels from terrain {gameObject.name}." );
        }
      }
    }
    public override void ConvertToDynamicMassInShape( Shape failureVolume )
    {
      if ( !IsNativeNull() )
        Native.convertToDynamicMassInShape( failureVolume.GetInitialized<Shape>().NativeShape );
    }

    public override void SetHeights( int xstart, int ystart, float[,] heights )
    {
      int height = heights.GetLength(0);
      int width = heights.GetLength(1);
      int resolution = TerrainDataResolution;

      if ( xstart + width >= resolution || xstart < 0 || ystart + height >= resolution || ystart < 0 )
        throw new ArgumentOutOfRangeException( "", $"Provided height patch with start ({xstart},{ystart}) and size ({width},{height}) extends outside of the terrain bounds [0,{TerrainDataResolution - 1}]" );

      float scale = TerrainData.size.y;
      float depthOffset = 0;
      if ( Native != null )
        depthOffset = MaximumDepth;

      for ( int y = 0; y < height; y++ ) {
        for ( int x = 0; x < width; x++ ) {
          float value = heights[ y, x ] + depthOffset;
          heights[ y, x ] = value / scale;

          agx.Vec2i idx = new agx.Vec2i( resolution - 1 - x - xstart, resolution - 1 - y - ystart);
          Native?.setHeight( idx, value );
        }
      }

      TerrainData.SetHeights( xstart, ystart, heights );
    }
    public override void SetHeight( int x, int y, float height )
    {
      if ( x >= TerrainDataResolution || x < 0 || y >= TerrainDataResolution || y < 0 )
        throw new ArgumentOutOfRangeException( "(x, y)", $"Indices ({x},{y}) is outside of the terrain bounds [0,{TerrainDataResolution - 1}]" );

      if ( Native != null )
        height += MaximumDepth;

      agx.Vec2i idx = new agx.Vec2i( TerrainDataResolution - 1 - x, TerrainDataResolution - 1 - y );
      Native?.setHeight( idx, height );

      TerrainData.SetHeights( x, y, new float[,] { { height / TerrainData.size.y } } );
    }
    public override float[,] GetHeights( int xstart, int ystart, int width, int height )
    {
      if ( width <= 0 || height <= 0 )
        throw new ArgumentOutOfRangeException( "width, height", $"Width and height ({width} / {height}) must be greater than 0" );

      int resolution = TerrainDataResolution;

      if ( xstart + width >= resolution || xstart < 0 || ystart + height >= resolution || ystart < 0 )
        throw new ArgumentOutOfRangeException( "", $"Requested height patch with start ({xstart},{ystart}) and size ({width},{height}) extends outside of the terrain bounds [0,{TerrainDataResolution - 1}]" );

      if ( Native == null )
        return TerrainData.GetHeights( xstart, ystart, width, height );

      float [,] heights = new float[height,width];
      for ( int y = 0; y < height; y++ ) {
        for ( int x = 0; x < width; x++ ) {
          agx.Vec2i idx = new agx.Vec2i( resolution - 1 - x - xstart, resolution - 1 - y - ystart);
          heights[ y, x ] = (float)Native.getHeight( idx ) - MaximumDepth;
        }
      }
      return heights;
    }
    public override float GetHeight( int x, int y )
    {
      if ( x >= TerrainDataResolution || x < 0 || y >= TerrainDataResolution || y < 0 )
        throw new ArgumentOutOfRangeException( "(x, y)", $"Indices ({x},{y}) is outside of the terrain bounds [0,{TerrainDataResolution - 1}]" );

      if ( Native == null )
        return TerrainData.GetHeight( x, y );

      agx.Vec2i idx = new agx.Vec2i( TerrainDataResolution - 1 - x, TerrainDataResolution - 1 - y );
      return (float)Native.getHeight( idx ) - MaximumDepth;
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
