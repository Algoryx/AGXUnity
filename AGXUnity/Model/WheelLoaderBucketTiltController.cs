using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity.Model
{
  [AddComponentMenu( "AGXUnity/Model/Wheel Loader Bucket Tilt Controller" )]
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

    [HideInInspector]
    public ObserverFrame RefObserver
    {
      get
      {
        if ( m_refObserver == null ) {
          var allTransforms = GetComponentsInChildren<Transform>();
          var t = allTransforms.FirstOrDefault( child => child.name == "FrontBodyObserver" );
          if ( t != null )
            m_refObserver = t.GetComponent<ObserverFrame>();
        }
        return m_refObserver;
      }
    }

    [HideInInspector]
    public Transform BucketTransform
    {
      get
      {
        if ( m_bucketTransform == null )
          m_bucketTransform = transform.Find( "Bucket" );
        return m_bucketTransform;
      }
    }

    [HideInInspector]
    public Vector3 BucketForward
    {
      get { return Vector3.forward; }
    }

    [HideInInspector]
    public Vector3 BucketForwardWorld
    {
      get
      {
        return BucketTransform.TransformDirection( BucketForward );
      }
    }

    [HideInInspector]
    public Vector3 RefForward
    {
      get { return Vector3.forward; }
    }

    [HideInInspector]
    public Vector3 RefForwardWorld
    {
      get
      {
        return RefObserver.transform.TransformDirection( RefForward );
      }
    }

    [HideInInspector]
    public Vector3 RefUp
    {
      get { return Vector3.up; }
    }

    [HideInInspector]
    public Vector3 RefUpWorld
    {
      get { return RefObserver.transform.TransformDirection( RefUp ); }
    }

    [ClampAboveZeroInInspector( true )]
    public float AngleDiffToTiltDistanceScale = 2.5f / Mathf.PI;

    public float CurrentAngle
    {
      get
      {
        return Mathf.Sign( Vector3.Dot( BucketForwardWorld, RefUpWorld ) ) *
               Mathf.Acos( Mathf.Clamp( Vector3.Dot( BucketForwardWorld, RefForwardWorld ), -1.0f, 1.0f ) );
      }
    }

    public float TargetAngle { get; private set; } = 0.0f;

    public bool IsStateTiltControlOverride
    {
      get
      {
        if ( m_rangedHinges == null || m_rangedHinges.Count == 0 )
          return false;
        foreach ( var c in m_rangedHinges ) {
          var h = agx.Constraint1DOF.safeCast( c.Native );
          h.getAttachmentPair().transform();
          if ( h.getRange1D().isActive() )
            return true;
        }
        return false;
      }
    }

    protected override bool Initialize()
    {
      if ( WheelLoader == null ) {
        Debug.LogError( "Unable to initialize: AGXUnity.Model.WheelLoader component not found.", this );
        return false;
      }

      if ( RefObserver == null ) {
        Debug.LogError( "Unable to initialize: Reference observer frame wasn't found.", this );
        return false;
      }

      if ( BucketTransform == null ) {
        Debug.LogError( "Unable to initialize: Bucket transform wasn't found.", this );
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
      if ( WheelLoader != null && RefObserver != null && BucketTransform != null ) {
        Simulation.Instance.StepCallbacks.PostStepForward += OnPost;
        TargetAngle = CurrentAngle;
      }
    }

    protected override void OnDisable()
    {
      if ( Simulation.HasInstance )
        Simulation.Instance.StepCallbacks.PostStepForward -= OnPost;
    }

    private void OnPost()
    {
      var tiltPrismatic = WheelLoader.TiltPrismatic;

      // Update target angle:
      //   - Driver active.
      //   - Override state active, at least one tracked range is active.
      if ( tiltPrismatic.GetController<TargetSpeedController>().Enable )
        TargetAngle = CurrentAngle;
      else {
        var diff = 0.0f;
        if ( IsStateTiltControlOverride )
          TargetAngle = CurrentAngle;
        else
          diff = ( CurrentAngle - TargetAngle ) * AngleDiffToTiltDistanceScale;
        tiltPrismatic.GetController<LockController>().Position = tiltPrismatic.GetCurrentAngle() + diff;
      }
    }

    private void Reset()
    {
      if ( GetComponent<WheelLoader>() == null )
        Debug.LogError( "AGXUnity.Model.WheelLoader component is required." );
    }

    private WheelLoader m_wheelLoader = null;
    private ObserverFrame m_refObserver = null;
    private Transform m_bucketTransform = null;
    private List<Constraint> m_rangedHinges = null;
  }
}
