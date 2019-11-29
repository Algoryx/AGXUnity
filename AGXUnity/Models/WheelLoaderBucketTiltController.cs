using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AGXUnity.Models
{
  public class WheelLoaderBucketTiltController : ScriptComponent
  {
    [HideInInspector]
    public WheelLoader WheelLoader
    {
      get
      {
        if ( m_wheelLoader == null )
          m_wheelLoader = GetComponent<WheelLoader>();
        return m_wheelLoader;
      }
    }

    protected override bool Initialize()
    {
      if ( WheelLoader == null ) {
        Debug.LogError( "Unable to initialize: AGXUnity.Models.WheelLoader component not found.", this );
        return false;
      }

      m_rangedHinges = new List<Constraint>();
      var hinges = ( from constraint in GetComponentsInChildren<Constraint>()
                     where constraint.Type == ConstraintType.Hinge
                     select constraint ).ToArray();
      foreach ( var hinge in hinges )
        if ( hinge.name.StartsWith( "TrackedRangeHinge" ) )
          m_rangedHinges.Add( hinge );

      return true;
    }

    protected override void OnEnable()
    {
      if ( WheelLoader != null )
        Simulation.Instance.StepCallbacks.PostStepForward += OnPost;
    }

    protected override void OnDisable()
    {
      if ( Simulation.HasInstance )
        Simulation.Instance.StepCallbacks.PostStepForward -= OnPost;
    }

    private void OnPost()
    {
    }

    private void Reset()
    {
      if ( GetComponent<WheelLoader>() == null )
        Debug.LogError( "AGXUnity.Models.WheelLoader component is required." );
    }

    private WheelLoader m_wheelLoader = null;
    private List<Constraint> m_rangedHinges = null;
  }
}
