using AGXUnity.Collide;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AGXUnity.Model
{
  [AddComponentMenu( "AGXUnity/Model/Deformable Terrain Failure Volume" )]
  [RequireComponent(typeof(Shape))]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#soil-failure-volumes" )]
  public class DeformableTerrainFailureVolume : ScriptComponent
  {
    public bool AddAllTerrainsOnStart { get; set; } = true;

    private HashSet<DeformableTerrainBase> m_terrains = new HashSet<DeformableTerrainBase>();

    public DeformableTerrainBase[] Terrains => m_terrains.ToArray();

    public bool Add( DeformableTerrainBase terrain )
    {
      return m_terrains.Add( terrain );
    }

    public bool Remove( DeformableTerrainBase terrain )
    {
      return m_terrains.Remove( terrain );
    }

    protected override bool Initialize()
    {
      if ( AddAllTerrainsOnStart )
        m_terrains.UnionWith( FindObjectsOfType<DeformableTerrainBase>() );
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
        Simulation.Instance.StepCallbacks.PreStepForward += TriggerFailure;
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