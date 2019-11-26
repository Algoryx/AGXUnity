using System;
using System.Collections.Generic;
using UnityEngine;
using AGXUnity.Utils;

using GUI = AGXUnity.Utils.GUI;

namespace AGXUnity
{
  [AddComponentMenu( "AGXUnity/Deformable Terrain" )]
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
    public Terrain Terrain { get { return m_terrain ?? ( m_terrain = GetComponent<Terrain>() ); } }

    /// <summary>
    /// Unity Terrain data.
    /// </summary>
    [HideInInspector]
    public TerrainData TerrainData { get { return Terrain?.terrainData; } }

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
            Native.loadLibraryMaterial( agxTerrain.TerrainMaterialLibrary.MaterialPreset.DIRT_1 );
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
    private bool m_tempDisplayShovelForces = false;
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

    protected override bool Initialize()
    {
      RemoveInvalidShovels();

      var maxDepth = 20.0f;

      m_initialHeights = TerrainData.GetHeights( 0, 0, TerrainData.heightmapWidth, TerrainData.heightmapHeight );

      var nativeHeightData = TerrainUtils.FindHeights( TerrainData );
      var elementSize = TerrainData.size.x / Convert.ToSingle( nativeHeightData.ResolutionX - 1 );

      var tmp = new float[,] { { 0.0f } };
      for ( int i = 0; i < nativeHeightData.Heights.Count; ++i ) {
        var newHeight = nativeHeightData.Heights[ i ] += maxDepth;

        var vertexX = i % nativeHeightData.ResolutionX;
        var vertexY = i / nativeHeightData.ResolutionY;

        tmp[ 0, 0 ] = (float)newHeight / TerrainData.heightmapScale.y;
        TerrainData.SetHeightsDelayLOD( TerrainData.heightmapWidth - vertexX - 1,
                                        TerrainData.heightmapHeight - vertexY - 1,
                                        tmp );
      }
#if UNITY_2019_1_OR_NEWER
      TerrainData.SyncHeightmap();
#else
      Terrain.ApplyDelayedHeightmapModification();
#endif

      transform.position = transform.position + maxDepth * Vector3.down;

      Debug.Log( "Resolution:   " + nativeHeightData.ResolutionX + ", " + nativeHeightData.ResolutionY );
      Debug.Log( "Element size: " + elementSize );

      Native = new agxTerrain.Terrain( (uint)nativeHeightData.ResolutionX,
                                       (uint)nativeHeightData.ResolutionY,
                                       elementSize,
                                       nativeHeightData.Heights,
                                       false,
                                       0.0f );

      Native.setTransform( Utils.TerrainUtils.CalculateNativeOffset( transform, TerrainData ) );

      foreach ( var shovel in Shovels )
        Native.add( shovel.GetInitialized<DeformableTerrainShovel>()?.Native );

      GetSimulation().add( Native );

      Simulation.Instance.StepCallbacks.PostStepForward += OnPostStepForward;

      return true;
    }

    protected override void OnDestroy()
    {
      var maxDepth = 20.0f;

      TerrainData.SetHeights( 0, 0, m_initialHeights );
      transform.position = transform.position + maxDepth * Vector3.up;

      if ( Properties != null )
        Properties.Unregister( this );

      if ( GetSimulation() != null ) {
        GetSimulation().remove( Native );
        Simulation.Instance.StepCallbacks.PostStepForward -= OnPostStepForward;
      }
      Native = null;

      if ( GUIWindowHandler.HasInstance )
        GUIWindowHandler.Instance.Close( ShowForces );

      base.OnDestroy();
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
      var resX   = TerrainData.heightmapWidth;
      var resY   = TerrainData.heightmapHeight;
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
