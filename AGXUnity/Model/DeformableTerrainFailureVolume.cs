using AGXUnity.Collide;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AGXUnity.Model
{
  [AddComponentMenu( "AGXUnity/Model/Deformable Terrain Failure Volume" )]
  [RequireComponent( typeof( Shape ) )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#soil-failure-volumes" )]
  public class DeformableTerrainFailureVolume : ScriptComponent
  {
    [field: SerializeField]
    public bool AddAllTerrainsOnStart { get; set; } = true;

    [field: SerializeField]
    private List<DeformableTerrainBase> m_terrains = new List<DeformableTerrainBase>();

    public DeformableTerrainBase[] Terrains => m_terrains.ToArray();

    public bool Add( DeformableTerrainBase terrain )
    {
      if ( m_terrains.Contains( terrain ) ) {
        Debug.LogWarning( $"Failure volume '{name}' already contains terrain '{terrain.name}'" );
        return false;
      }
      m_terrains.Add( terrain );
      return true;
    }

    public bool Remove( DeformableTerrainBase terrain )
    {
      if ( !m_terrains.Contains( terrain ) ) {
        Debug.LogWarning( $"Failure volume '{name}' does not contain terrain '{terrain.name}'" );
        return false;
      }
      return m_terrains.Remove( terrain );
    }

    protected override bool Initialize()
    {
      if ( AddAllTerrainsOnStart ) {
#if UNITY_2022_2_OR_NEWER
        m_terrains.AddRange( FindObjectsByType<DeformableTerrainBase>( FindObjectsSortMode.None ).Where( t => !m_terrains.Contains( t ) ) );
#else
        m_terrains.AddRange( FindObjectsOfType<DeformableTerrainBase>().Where( t => !m_terrains.Contains( t ) ) );
#endif
      }
      return base.Initialize();
    }

    protected override void OnEnable()
    {
      Simulation.Instance.StepCallbacks.PreStepForward += TriggerFailure;
      base.OnEnable();
    }

    protected override void OnDisable()
    {
      if ( Simulation.HasInstance )
        Simulation.Instance.StepCallbacks.PreStepForward -= TriggerFailure;
      base.OnDisable();
    }

    private void TriggerFailure()
    {
      var shape = GetComponent<Shape>();

      foreach ( var terr in m_terrains )
        terr.ConvertToDynamicMassInShape( shape );
    }
  }
}
