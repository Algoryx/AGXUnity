using System;
using UnityEngine;

namespace AGXUnity
{
  /// <summary>
  /// Shape material script asset.
  /// </summary>
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#shape-material" )]
  public class ShapeMaterial : ScriptAsset
  {
    /// <summary>
    /// Native instance.
    /// </summary>
    private agx.Material m_material = null;

    /// <summary>
    /// Get native instance, if initialized.
    /// </summary>
    public agx.Material Native { get { return m_material; } }

    /// <summary>
    /// Density of this material, paired with property Density.
    /// Default value: 1000.
    /// </summary>
    [SerializeField]
    private float m_density = 1000.0f;

    /// <summary>
    /// Get or set density of this material.
    /// Default value: 1000.
    /// </summary>
    [ClampAboveZeroInInspector]
    public float Density
    {
      get { return m_density; }
      set
      {
        m_density = value;
        if ( Native != null )
          Native.getBulkMaterial().setDensity( m_density );
      }
    }

    /// <summary>
    /// Young's modulus stretch for wires.
    /// Default value: 1.0E10
    /// </summary>
    [SerializeField]
    private float m_youngsWireStretch = 1.0E10f;

    /// <summary>
    /// Get or set Young's modulus stretch for wires.
    /// Default value: 1.0E10
    /// </summary>
    [ClampAboveZeroInInspector]
    public float YoungsWireStretch
    {
      get { return m_youngsWireStretch; }
      set
      {
        m_youngsWireStretch = value;
        if ( Native != null )
          Native.getWireMaterial().setYoungsModulusStretch( m_youngsWireStretch );
      }
    }

    /// <summary>
    /// Young's modulus bend of this material.
    /// Default value: 1.0E9
    /// </summary>
    [SerializeField]
    private float m_youngsWireBend = 1.0E9f;

    /// <summary>
    /// Get or set Young's modulus bend of this material.
    /// Default value: 1.0E9
    /// </summary>
    [ClampAboveZeroInInspector]
    public float YoungsWireBend
    {
      get { return m_youngsWireBend; }
      set
      {
        m_youngsWireBend = value;
        if ( Native != null )
          Native.getWireMaterial().setYoungsModulusBend( m_youngsWireBend );
      }
    }

    /// <summary>
    /// Creates temporary native instance to be added to native
    /// geometries to calculate mass and inertia.
    /// </summary>
    /// <returns></returns>
    public agx.Material CreateTemporaryNative()
    {
      // Use current if we haven't been destroyed.
      if ( Native != null )
        return Native;

      agx.Material tmpMaterial = new agx.Material( name );
      m_material = tmpMaterial;
      Utils.PropertySynchronizer.Synchronize( this );
      m_material = null;
      return tmpMaterial;
    }

    public ShapeMaterial RestoreLocalDataFrom( agx.Material native )
    {
      Density           = Convert.ToSingle( native.getBulkMaterial().getDensity() );
      YoungsWireStretch = Convert.ToSingle( native.getWireMaterial().getYoungsModulusStretch() );
      YoungsWireBend    = Convert.ToSingle( native.getWireMaterial().getYoungsModulusBend() );

      return this;
    }

    protected override void Construct()
    {
    }

    protected override bool Initialize()
    {
      m_material = new agx.Material( name );

      return true;
    }

    public override void Destroy()
    {
      m_material = null;
    }

    /// <summary>
    /// Default material.
    /// </summary>
    private static ShapeMaterial m_defaultMaterial = null;

    /// <summary>
    /// Default material.
    /// </summary>
    [HideInInspector]
    public static ShapeMaterial Default
    {
      get
      {
        if ( m_defaultMaterial == null ) {
          m_defaultMaterial = ScriptableObject.CreateInstance<ShapeMaterial>();
          m_defaultMaterial.name = "DefaultMaterial";
        }
        return m_defaultMaterial;
      }
    }
  }
}
