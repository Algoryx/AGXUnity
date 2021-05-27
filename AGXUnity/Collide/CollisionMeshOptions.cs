using UnityEngine;

namespace AGXUnity.Collide
{
  [DoNotGenerateCustomEditor]
  public class CollisionMeshOptions : ScriptableObject
  {
    public enum MeshMode
    {
      Trimesh,
      Convex,
      ConvexDecomposition
    }

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
