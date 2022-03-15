using System;
using System.Collections.Generic;
using UnityEngine;
using AGXUnity.Utils;

using GUI = AGXUnity.Utils.GUI;

namespace AGXUnity.Model
{
  [AddComponentMenu( "AGXUnity/Model/Deformable Terrain" )]
  [RequireComponent(typeof( Terrain ))]
  [DisallowMultipleComponent]
  public class DeformableTerrain : ScriptComponent
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

    /// <summary>
    /// Shovels associated to this terrain.
    /// </summary>
    [HideInInspector]
    public DeformableTerrainShovel[] Shovels { get { return m_shovels.ToArray(); } }

    [SerializeField]
    private ShapeMaterial m_material = null;

    /// <summary>
    /// Shape material associated to this terrain.
    /// </summary>
    [AllowRecursiveEditing]
    public ShapeMaterial Material
    {
      get { return m_material; }
      set
      {
        m_material = value;
        if ( Native != null ) {
          if ( m_material != null && m_material.Native == null )
            m_material.GetInitialized<ShapeMaterial>();
          if ( m_material != null )
            Native.setMaterial( m_material.Native );

          // TODO: When m_material is null here it means "use default" but
          //       it's currently not possible to understand which parameters
          //       that has been set in e.g., Terrain::loadLibraryMaterial.
        }
      }
    }

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

    public float ElementSize
    {
      get
      {
        return TerrainData.size.x / ( TerrainDataResolution - 1 );
      }
    }

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

    /// <summary>
    /// Resets heights of the Unity terrain and recreate native instance.
    /// </summary>
    public void ResetHeights()
    {
      if ( Native != null && Simulation.HasInstance ) {
        GetSimulation().remove( Native );
        Native = null;
      }

      ResetTerrainDataHeightsAndTransform();

      InitializeNative();

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
      WriteTerrainDataOffset( Terrain, -MaximumDepth );
    }

    protected override bool Initialize()
    {
      // Only printing the errors if something is wrong.
      LicenseManager.LicenseInfo.HasModuleLogError( LicenseInfo.Module.AGXTerrain | LicenseInfo.Module.AGXGranular, this );

      RemoveInvalidShovels();

      m_initialHeights = TerrainData.GetHeights( 0, 0, TerrainDataResolution, TerrainDataResolution );

      InitializeNative();

      Simulation.Instance.StepCallbacks.PostStepForward += OnPostStepForward;

      // Native terrain may change the number of PPGS iterations to default (25).
      // Override if we have solver settings set to the simulation.
      if ( Simulation.Instance.SolverSettings != null )
        GetSimulation().getSolver().setNumPPGSRestingIterations( (ulong)Simulation.Instance.SolverSettings.PpgsRestingIterations );

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
      var nativeHeightData = WriteTerrainDataOffset( Terrain, MaximumDepth );

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

    /// <summary>
    /// Writes <paramref name="offset"/> to <paramref name="terrain"/> height data.
    /// </summary>
    /// <param name="terrainData">Terrain to modify.</param>
    /// <param name="offset">Height offset.</param>
    private static TerrainUtils.NativeHeights WriteTerrainDataOffset( Terrain terrain, float offset )
    {
      var terrainData        = terrain.terrainData;
      var nativeHeightData   = TerrainUtils.FindHeights( terrainData );
      var tmp                = new float[,] { { 0.0f } };
      var dataMaxHeight      = terrainData.size.y;
      var maxClampedHeight   = -1.0f;
      for ( int i = 0; i < nativeHeightData.Heights.Count; ++i ) {
        var newHeight = nativeHeightData.Heights[ i ] += offset;

        var vertexX = i % nativeHeightData.ResolutionX;
        var vertexY = i / nativeHeightData.ResolutionY;

        tmp[ 0, 0 ] = (float)newHeight / terrainData.heightmapScale.y;
        if ( newHeight > dataMaxHeight )
          maxClampedHeight = System.Math.Max( maxClampedHeight, (float)newHeight );

        terrainData.SetHeightsDelayLOD( TerrainUtils.TerrainDataResolution( terrainData ) - vertexX - 1,
                                        TerrainUtils.TerrainDataResolution( terrainData ) - vertexY - 1,
                                        tmp );
      }

      if ( maxClampedHeight > 0.0f ) {
        Debug.LogWarning( "Terrain heights were clamped: UnityEngine.TerrainData max height = " +
                          dataMaxHeight +
                          " and AGXUnity.Model.DeformableTerrain.MaximumDepth = " +
                          offset +
                          ". Resolve this by increasing max height and lower the terrain or decrease Maximum Depth.", terrain );
      }

#if UNITY_2019_1_OR_NEWER
      terrainData.SyncHeightmap();
#else
      terrain.ApplyDelayedHeightmapModification();
#endif

      return nativeHeightData;
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
  }
}
