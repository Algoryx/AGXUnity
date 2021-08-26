using UnityEngine;

namespace AGXUnity.Collide
{
  [DoNotGenerateCustomEditor]
  [System.Serializable]
  public class CollisionMeshOptions
  {
    /// <summary>
    /// Collection of supported mesh modes.
    /// </summary>
    public enum MeshMode
    {
      /// <summary>
      /// Concave triangle mesh is the default mode.
      /// </summary>
      Trimesh,
      /// <summary>
      /// Mesh will be treated as a convex.
      /// </summary>
      Convex,
      /// <summary>
      /// Concave meshes will be divided into several convex meshes.
      /// </summary>
      ConvexDecomposition
    }

    /// <summary>
    /// Get or set mode of the mesh. Note that Apply has to
    /// be called before the change to take effect.
    /// </summary>
    public MeshMode Mode
    {
      get { return m_mode; }
      set
      {
        if ( m_mode == value )
          return;

        m_mode = value;
      }
    }

    /// <summary>
    /// Enable/disable triangle reduction of the mesh data.
    /// Note that Apply has to be called before the change to take effect.
    /// </summary>
    [InspectorGroupBegin( Name = "Vertex Reduction" )]
    public bool ReductionEnabled
    {
      get { return m_reductionEnabled; }
      set
      {
        if ( m_reductionEnabled == value )
          return;

        m_reductionEnabled = value;
      }
    }

    /// <summary>
    /// Reduction ratio ranging from [0.02, 0.98] where 1.0 is
    /// no reduction and 0 is maximum reduction.
    /// Note that Apply has to be called before the change to take effect.
    /// </summary>
    [FloatSliderInInspector(0.02f, 0.98f)]
    public float ReductionRatio
    {
      get { return m_reductionRatio; }
      set
      {
        if ( Utils.Math.Approximately( m_reductionRatio, value ) )
          return;

        m_reductionRatio = Utils.Math.Clamp( value, 0.02f, 0.98f );
      }
    }

    /// <summary>
    /// Lower is faster and higher is better decimation. Valid range is [0.01,..].
    /// Default: 7.0
    /// </summary>
    public float ReductionAggressiveness
    {
      get { return m_reductionAggressiveness; }
      set
      {
        if ( Utils.Math.Approximately( m_reductionAggressiveness, value ) )
          return;

        m_reductionAggressiveness = Utils.Math.ClampAbove( value, 1.0E-2f );
      }
    }

    /// <summary>
    /// Convex decomposition - resolution parameter [20, 400]. Default: 50.
    /// </summary>
    [InspectorGroupBegin( Name = "Convex Decomposition" )]
    public int ElementResolutionPerAxis
    {
      get { return m_elementResolutionPerAxis; }
      set
      {
        if ( m_elementResolutionPerAxis == value )
          return;

        m_elementResolutionPerAxis = System.Math.Max( value, 1 );
      }
    }

    /// <summary>
    /// Reset all values to default.
    /// </summary>
    public void ResetToDesfault()
    {
      m_mode = MeshMode.Trimesh;
      m_reductionEnabled = false;
      m_reductionRatio = 0.5f;
      m_reductionAggressiveness = 7.0f;
      m_elementResolutionPerAxis = 50;
    }


    [SerializeField]
    private MeshMode m_mode = MeshMode.Trimesh;

    [SerializeField]
    private bool m_reductionEnabled = false;

    [SerializeField]
    private float m_reductionRatio = 0.5f;

    [SerializeField]
    private float m_reductionAggressiveness = 7.0f;

    [SerializeField]
    private int m_elementResolutionPerAxis = 50;
  }
}
