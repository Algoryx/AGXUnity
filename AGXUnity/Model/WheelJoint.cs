using AGXUnity.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AGXUnity.Model
{
  [AddComponentMenu( "AGXUnity/Constraints/Wheel Joint" )]
  public class WheelJoint : ScriptComponent
  {
    /// <summary>
    /// Controller dimensions of the wheel joing used to find controllers in the constraint. 
    /// - 'Steering' finds the controllers around the steering axle. 
    /// - 'Wheel' finds the controllers around the wheel rotation axis. 
    /// - 'Suspension' finds the controller along the suspension axis.
    /// </summary>
    public enum WheelDimension
    {
      Steering = 0,
      Wheel = 1,
      Suspension = 2
    }

    /// <summary>
    /// Attachment pair of this constraint, holding parent objects and transforms.
    /// Paired with property AttachmentPair.
    /// </summary>
    [SerializeField]
    private AttachmentPair m_attachmentPairComponent = null;

    /// <summary>
    /// Attachment pair of this constraint, holding parent objects and transforms.
    /// </summary>
    [HideInInspector]
    public AttachmentPair AttachmentPair
    {
      get
      {
        // Creates attachment pair if it doesn't exist.
        if ( m_attachmentPairComponent == null ) {
          // Will add itself as component to our game object.
          m_attachmentPairComponent = AttachmentPair.Create( gameObject );
        }

        return m_attachmentPairComponent;
      }
    }

    /// <summary>
    /// Collisions state when the simulation is running.
    /// </summary>
    [HideInInspector]
    [field: SerializeField]
    public Constraint.ECollisionsState CollisionsState { get; set; } = Constraint.ECollisionsState.DisableRigidBody1VsRigidBody2;

    [SerializeField]
    private Constraint.ESolveType m_solveType = Constraint.ESolveType.Direct;

    /// <summary>
    /// Solve type of this constraint.
    /// </summary>
    [HideInInspector]
    public Constraint.ESolveType SolveType
    {
      get => m_solveType;
      set
      {
        m_solveType = value;
        if ( Native != null )
          Native.setSolveType( Constraint.Convert( m_solveType ) );
      }
    }

    private bool m_isAnimated = false;

    /// <summary>
    /// Enable/disable gizmos drawing of this constraint. Enabled by default.
    /// </summary>
    [HideInInspector]
    [field: SerializeField]
    public bool DrawGizmosEnable { get; set; } = true;

    /// <summary>
    /// Native instance if this constraint is initialized - otherwise null.
    /// </summary>
    public agxVehicle.WheelJoint Native { get; private set; }

    /// <summary>
    /// True to enable synchronization of the connected frame to the native constraint (default: false/disabled).
    /// </summary>
    [HideInInspector]
    [field: SerializeField]
    public bool ConnectedFrameNativeSyncEnabled { get; set; } = false;

    /// <summary>
    /// List of elementary constraints in this constraint - controllers and ordinary.
    /// </summary>
    [SerializeReference]
    private List<ElementaryConstraint> m_elementaryConstraintsNew = new List<ElementaryConstraint>();

    /// <summary>
    /// Array of elementary constraints in this constraint - controllers and ordinary.
    /// </summary>
    [HideInInspector]
    public ElementaryConstraint[] ElementaryConstraints => m_elementaryConstraintsNew.ToArray();

    /// <summary>
    /// Finds and returns an array of ordinary ElementaryConstraint objects, i.e., the ones
    /// that aren't controllers.
    /// </summary>
    /// <returns>Array of ordinary elementary constraints.</returns>
    public ElementaryConstraint[] GetOrdinaryElementaryConstraints()
    {
      return ( from ec
               in m_elementaryConstraintsNew
               where ec as ElementaryConstraintController == null &&
                    !ec.NativeName.StartsWith( "F" ) && // Ignoring friction controller from versions
                                                        // it wasn't implemented in.
                    ec.NativeName != "CL" &&  // Ignoring ConeLimit until it is implemented 
                                              // TODO: Implement ConeLimit secondary constraint
                    ec.NativeName != "TR"     // Ignoring TwistRange until it is implemented 
                                              // TODO: Implement ConeLimit secondary constraint
               select ec ).ToArray();
    }

    /// <summary>
    /// Finds and returns an array of controller elementary constraints, such as motor, lock, range etc.
    /// </summary>
    /// <returns>Array of controllers - if present.</returns>
    public ElementaryConstraintController[] GetElementaryConstraintControllers()
    {
      return ( from ec
               in m_elementaryConstraintsNew
               where ec is ElementaryConstraintController
               select ec as ElementaryConstraintController ).ToArray();
    }

    /// <summary>
    /// Find controller of given type and dimension. 
    /// </summary>
    /// <typeparam name="T">Type of the controller.</typeparam>
    /// <param name="dimension">Working dimension of the controller.</param>
    /// <returns>Controller of given type and working dimension - if present, otherwise null.</returns>
    public T GetController<T>( WheelDimension dimension ) where T : ElementaryConstraintController
    {
      var controllers = GetElementaryConstraintControllers();
      var dimName = dimension switch
      {
        WheelDimension.Steering => "St",
        WheelDimension.Suspension => "Su",
        WheelDimension.Wheel => "Wh",
        _ => "N/A"
      };
      for ( int i = 0; i < controllers.Length; ++i ) {
        if ( controllers[ i ].NativeName.EndsWith( dimName ) && controllers[ i ] is T casted )
          return casted;
      }

      return null;
    }

    /// <summary>
    /// Traverse ElementaryConstraintRowData instances of this constraints
    /// ordinary elementary constraints.
    /// </summary>
    /// <typeparam name="T">Either TranslationalDof or RotationalDof.</typeparam>
    /// <param name="callback">Callback for each valid row data instance.</param>
    /// <param name="value">Enum value X, Y, Z or All.</param>
    public void TraverseRowData<T>( Action<ElementaryConstraintRowData> callback, T value )
      where T : struct
    {
      var rowParser = ConstraintUtils.ConstraintRowParser.Create( GetOrdinaryElementaryConstraints() );
      var rows = typeof( T ) == typeof( Constraint.TranslationalDof ) ?
                   rowParser.TranslationalRows :
                   rowParser.RotationalRows;

      // Dof.All
      if ( System.Convert.ToInt32( value ) > 2 ) {
        foreach ( var data in rows )
          if ( data != null )
            callback( data.RowData );
      }
      else {
        var data = rows[ System.Convert.ToInt32( value ) ];
        if ( data != null )
          callback( data.RowData );
      }
    }

    private void Reset()
    {
      using ( var tmpNative = new TemporaryNative() )
        TryAddElementaryConstraints( tmpNative.Instance );
    }

    /// <summary>
    /// Set compliance to all ordinary degrees of freedom (not including controllers)
    /// of this constraint.
    /// </summary>
    /// <param name="compliance">New compliance.</param>
    public void SetCompliance( float compliance )
    {
      TraverseRowData( data => data.Compliance = compliance, Constraint.TranslationalDof.All );
      TraverseRowData( data => data.Compliance = compliance, Constraint.RotationalDof.All );
    }

    /// <summary>
    /// Set compliance to one or all translational ordinary degrees of freedom
    /// (not including controllers) of this constraint.
    /// </summary>
    /// <param name="compliance">New compliance.</param>
    /// <param name="dof">Specific translational degree of freedom or all.</param>
    public void SetCompliance( float compliance, Constraint.TranslationalDof dof )
    {
      TraverseRowData( data => data.Compliance = compliance, dof );
    }

    /// <summary>
    /// Set compliance to one or all rotational ordinary degrees of freedom
    /// (not including controllers) of this constraint.
    /// </summary>
    /// <param name="compliance">New compliance.</param>
    /// <param name="dof">Specific rotational degree of freedom or all.</param>
    public void SetCompliance( float compliance, Constraint.RotationalDof dof )
    {
      TraverseRowData( data => data.Compliance = compliance, dof );
    }

    /// <summary>
    /// Set damping to all ordinary degrees of freedom (not including controllers)
    /// of this constraint.
    /// </summary>
    /// <param name="damping">New damping.</param>
    public void SetDamping( float damping )
    {
      TraverseRowData( data => data.Damping = damping, Constraint.TranslationalDof.All );
      TraverseRowData( data => data.Damping = damping, Constraint.RotationalDof.All );
    }

    /// <summary>
    /// Set damping to one or all translational ordinary degrees of freedom
    /// (not including controllers) of this constraint.
    /// </summary>
    /// <param name="damping">New damping.</param>
    /// <param name="dof">Specific translational degree of freedom or all.</param>
    public void SetDamping( float damping, Constraint.TranslationalDof dof )
    {
      TraverseRowData( data => data.Damping = damping, dof );
    }

    /// <summary>
    /// Set damping to one or all rotational ordinary degrees of freedom
    /// (not including controllers) of this constraint.
    /// </summary>
    /// <param name="damping">New damping.</param>
    /// <param name="dof">Specific rotational degree of freedom or all.</param>
    public void SetDamping( float damping, Constraint.RotationalDof dof )
    {
      TraverseRowData( data => data.Damping = damping, dof );
    }

    /// <summary>
    /// Set force range to all ordinary degrees of freedom (not including controllers)
    /// of this constraint.
    /// </summary>
    /// <param name="forceRange">New force range.</param>
    public void SetForceRange( RangeReal forceRange )
    {
      TraverseRowData( data => data.ForceRange = forceRange, Constraint.TranslationalDof.All );
      TraverseRowData( data => data.ForceRange = forceRange, Constraint.RotationalDof.All );
    }

    /// <summary>
    /// Set force range to one or all translational ordinary degrees of freedom
    /// (not including controllers) of this constraint.
    /// </summary>
    /// <param name="forceRange">New force range.</param>
    /// <param name="dof">Specific translational degree of freedom or all.</param>
    public void SetForceRange( RangeReal forceRange, Constraint.TranslationalDof dof )
    {
      TraverseRowData( data => data.ForceRange = forceRange, dof );
    }

    /// <summary>
    /// Set force range to one or all rotational ordinary degrees of freedom
    /// (not including controllers) of this constraint.
    /// </summary>
    /// <param name="forceRange">New force range.</param>
    /// <param name="dof">Specific rotational degree of freedom or all.</param>
    public void SetForceRange( RangeReal forceRange, Constraint.RotationalDof dof )
    {
      TraverseRowData( data => data.ForceRange = forceRange, dof );
    }

    /// <summary>
    /// Internal method which constructs this constraint given elementary constraints
    /// in the native instance. Throws if an elementary constraint fails to initialize.
    /// </summary>
    /// <param name="native">Native instance.</param>
    /// <param name="onObjectCreated">Optional callback when elementary constraint has been created.</param>
    public void TryAddElementaryConstraints( agx.Constraint native,
                                             Action<object> onObjectCreated = null )
    {
      if ( native == null )
        throw new ArgumentNullException( "native", "Native constraint is null." );

      // Remove old elementary constraints
      m_elementaryConstraintsNew.Clear();

      for ( uint i = 0; i < native.getNumElementaryConstraints(); ++i ) {
        if ( native.getElementaryConstraint( i ).getName() == "" )
          throw new Exception( "Native elementary constraint doesn't have a name." );

        var ec = ElementaryConstraint.Create( gameObject, native.getElementaryConstraint( i ) );
        if ( ec == null )
          throw new Exception( "Failed to configure elementary constraint with name: " + native.getElementaryConstraint( i ).getName() + "." );

        onObjectCreated?.Invoke( ec );

        m_elementaryConstraintsNew.Add( ec );
      }

      for ( uint i = 0; i < native.getNumSecondaryConstraints(); ++i ) {
        if ( native.getSecondaryConstraint( i ).getName() == "" )
          throw new Exception( "Native secondary constraint doesn't have a name." );

        var sc = ElementaryConstraint.Create( gameObject, native.getSecondaryConstraint( i ) );
        if ( sc == null )
          throw new Exception( "Failed to configure elementary controller constraint with name: " + native.getElementaryConstraint( i ).getName() + "." );

        onObjectCreated?.Invoke( sc );

        m_elementaryConstraintsNew.Add( sc );
      }
    }

    /// <summary>
    /// Creates native instance and adds it to the simulation if this constraint
    /// is properly configured.
    /// </summary>
    /// <returns>True if successful.</returns>
    protected override bool Initialize()
    {
      if ( AttachmentPair.ReferenceObject == null ) {
        Debug.LogError( "Unable to initialize constraint - reference object " +
                        "must be valid and contain a rigid body component.",
                        this );
        return false;
      }

      // Synchronize frames to make sure connected frame is up to date.
      AttachmentPair.Synchronize();

      // TODO: Disabling rigid body game object (activeSelf == false) and will not be
      //       able to create native body (since State == Constructed and not Awake).
      //       Do: GetComponentInParent<RigidBody>( true <- include inactive ) and wait
      //           for the body to become active?
      //       E.g., rb.AwaitInitialize += ThisConstraintInitialize.
      RigidBody rb1 = AttachmentPair.ReferenceObject.GetInitializedComponentInParent<RigidBody>();
      if ( rb1 == null ) {
        Debug.LogError( "Unable to initialize constraint - reference object must " +
                        "contain a rigid body component.",
                        AttachmentPair.ReferenceObject );
        return false;
      }

      // Native constraint frames.
      agx.Frame f1 = new agx.Frame();
      agx.Frame f2 = new agx.Frame();

      // Note that the native constraint want 'f1' given in rigid body frame, and that
      // 'ReferenceFrame' may be relative to any object in the children of the body.
      f1.setLocalTranslate( AttachmentPair.ReferenceFrame.CalculateLocalPosition( rb1.gameObject ).ToHandedVec3() );
      f1.setLocalRotate( AttachmentPair.ReferenceFrame.CalculateLocalRotation( rb1.gameObject ).ToHandedQuat() );

      RigidBody rb2 = AttachmentPair.ConnectedObject != null ?
                        AttachmentPair.ConnectedObject.GetInitializedComponentInParent<RigidBody>() :
                        null;
      if ( rb1 == rb2 ) {
        Debug.LogError( "Unable to initialize constraint - reference and connected " +
                        "rigid body is the same instance.",
                        this );
        return false;
      }

      if ( rb2 != null ) {
        // Note that the native constraint want 'f2' given in rigid body frame, and that
        // 'ReferenceFrame' may be relative to any object in the children of the body.
        f2.setLocalTranslate( AttachmentPair.ConnectedFrame.CalculateLocalPosition( rb2.gameObject ).ToHandedVec3() );
        f2.setLocalRotate( AttachmentPair.ConnectedFrame.CalculateLocalRotation( rb2.gameObject ).ToHandedQuat() );
      }
      else {
        f2.setLocalTranslate( AttachmentPair.ConnectedFrame.Position.ToHandedVec3() );
        f2.setLocalRotate( AttachmentPair.ConnectedFrame.Rotation.ToHandedQuat() );
      }

      try {
        Native = new agxVehicle.WheelJoint( rb1.Native, f1, ( rb2 != null ? rb2.Native : null ), f2 );

        // Assigning native elementary constraints to our elementary constraint instances.
        foreach ( ElementaryConstraint ec in ElementaryConstraints )
          if ( !ec.OnConstraintInitialize( this ) )
            throw new Exception( "Unable to initialize elementary constraint: " +
                                 ec.NativeName +
                                 " (not present in native wheel joint)." );

        bool added = GetSimulation().add( Native );
        Native.setEnable( isActiveAndEnabled );

        // Not possible to handle collisions if connected frame parent is null/world.
        if ( CollisionsState != Constraint.ECollisionsState.KeepExternalState && AttachmentPair.ConnectedObject != null ) {
          string groupName = gameObject.name + "_" + gameObject.GetInstanceID().ToString();
          GameObject go1   = null;
          GameObject go2   = null;
          if ( CollisionsState == Constraint.ECollisionsState.DisableReferenceVsConnected ) {
            go1 = AttachmentPair.ReferenceObject;
            go2 = AttachmentPair.ConnectedObject;
          }
          else {
            go1 = rb1.gameObject;
            go2 = rb2 != null ?
                    rb2.gameObject :
                    AttachmentPair.ConnectedObject;
          }

          go1.GetOrCreateComponent<CollisionGroups>().GetInitialized<CollisionGroups>().AddGroup( groupName, false );
          // Propagate to children if rb2 is null, which means
          // that go2 could be some static structure.
          go2.GetOrCreateComponent<CollisionGroups>().GetInitialized<CollisionGroups>().AddGroup( groupName, rb2 == null );
          CollisionGroupsManager.Instance.GetInitialized<CollisionGroupsManager>().SetEnablePair( groupName, groupName, false );
        }

        Native.setName( name );

        bool valid = added && Native.getValid();
        Simulation.Instance.StepCallbacks.PreSynchronizeTransforms += OnPreStepForwardUpdate;

        // It's not possible to check which properties an animator
        // is controlling, for now we update all properties in the
        // controllers if we have an animator.
        m_isAnimated = GetComponent<Animator>() != null;

        return valid;
      }
      catch ( System.Exception e ) {
        Debug.LogException( e, gameObject );
        return false;
      }
    }

    protected override void OnEnable()
    {
      if ( Native != null && !Native.getEnable() )
        Native.setEnable( true );

      // It's not possible to check which properties an animator
      // is controlling, for now we update all properties in the
      // controllers if we have an animator.
      m_isAnimated = GetComponent<Animator>() != null;
    }

    protected override void OnDisable()
    {
      if ( Native != null && Native.getEnable() )
        Native.setEnable( false );
    }

    protected override void OnDestroy()
    {
      if ( Simulation.HasInstance ) {
        Simulation.Instance.StepCallbacks.PreSynchronizeTransforms -= OnPreStepForwardUpdate;
        GetSimulation().remove( Native );
      }

      Native = null;

      base.OnDestroy();
    }

    private void OnPreStepForwardUpdate()
    {
      if ( Native == null || !Native.getValid() )
        return;

      SynchronizeNativeFramesWithAttachmentPair();

      if ( m_isAnimated ) {
        var controllers = GetElementaryConstraintControllers();
        for ( int i = 0; i < controllers.Length; ++i )
          PropertySynchronizer.Synchronize( controllers[ i ] );
      }
    }

    private void SynchronizeNativeFramesWithAttachmentPair()
    {
      // NOTE: It's not possible to update the constraint frames given the current
      //       transforms since the actual constraint direction will change with the
      //       violation.
      //RigidBody rb1 = AttachmentPair.ReferenceObject.GetComponentInParent<RigidBody>();
      //if ( rb1 == null )
      //  return;
      //
      //agx.Frame f1 = Native.getAttachment( 0 ).getFrame();
      //f1.setLocalTranslate( AttachmentPair.ReferenceFrame.CalculateLocalPosition( rb1.gameObject ).ToHandedVec3() );
      //f1.setLocalRotate( AttachmentPair.ReferenceFrame.CalculateLocalRotation( rb1.gameObject ).ToHandedQuat() );

      if ( ConnectedFrameNativeSyncEnabled ) {
        RigidBody rb2 = AttachmentPair.ConnectedObject != null ? AttachmentPair.ConnectedObject.GetComponentInParent<RigidBody>() : null;
        agx.Frame f2 = Native.getAttachment( 1 ).getFrame();

        if ( rb2 != null ) {
          f2.setLocalTranslate( AttachmentPair.ConnectedFrame.CalculateLocalPosition( rb2.gameObject ).ToHandedVec3() );
          f2.setLocalRotate( AttachmentPair.ConnectedFrame.CalculateLocalRotation( rb2.gameObject ).ToHandedQuat() );
        }
        else {
          f2.setLocalTranslate( AttachmentPair.ConnectedFrame.Position.ToHandedVec3() );
          f2.setLocalRotate( AttachmentPair.ConnectedFrame.Rotation.ToHandedQuat() );
        }
      }
    }

    /// <summary>
    /// Calculates the current angle for given degree of freedom when this
    /// constraint is active.
    /// </summary>
    /// <param name="controllerType">Working dimension (translational or rotational). It's
    ///                              normally enough with Primary if this constraint isn't
    ///                              a CylindricalJoint. If cylindrical - primary == Translational.</param>
    /// <returns>Current angle of the active constraint.</returns>
    public float GetCurrentAngle( WheelDimension controllerType = WheelDimension.Steering )
    {
      if ( Native != null )
        return System.Convert.ToSingle( Native.getAngle( (agxVehicle.WheelJoint.SecondaryConstraint)controllerType ) );

      return 0.0f;
    }

    public Vector3 SteeringAxis
    {
      get
      {
        if ( Native == null ) {
          var frame = AttachmentPair.ConnectedFrame;
          if ( AttachmentPair.Synchronized )
            frame = AttachmentPair.ReferenceFrame;
          return frame.Rotation * Vector3.forward;
        }
        else
          return Native.getSteeringAxle().ToHandedVector3();
      }
    }

    public Vector3 WheelAttachmentPoint
    {
      get
      {
        if ( Native == null ) {
          var frame = AttachmentPair.ConnectedFrame;
          if ( AttachmentPair.Synchronized )
            frame = AttachmentPair.ReferenceFrame;
          return frame.Position;
        }
        else
          return Native.getAttachmentPair().getAttachment2().get( agx.Attachment.Transformed.ANCHOR_POS ).ToHandedVector3();
      }
    }

    public Vector3 WheelAxis
    {
      get
      {
        if ( Native == null ) {
          var frame = AttachmentPair.ConnectedFrame;
          if ( AttachmentPair.Synchronized )
            frame = AttachmentPair.ReferenceFrame;
          return frame.Rotation * Vector3.up;
        }
        else
          return Native.getAttachmentPair().getAttachment1().get( agx.Attachment.Transformed.V ).ToHandedVector3();
      }
    }

    private class TemporaryNative : IDisposable
    {
      public agxVehicle.WheelJoint Instance => m_native;

      public TemporaryNative( AttachmentPair attachmentPair = null )
      {
        m_rb1 = new agx.RigidBody();
        m_f1 = new agx.Frame();
        m_f2 = new agx.Frame();

        if ( attachmentPair != null ) {
          // Some constraints, e.g., Distance Joints depends on the constraint angle during
          // creation so we feed the frames with the world transform of the reference and
          // connecting frame.
          attachmentPair.Synchronize();

          m_f1.setLocalTranslate( attachmentPair.ReferenceFrame.Position.ToHandedVec3() );
          m_f1.setLocalRotate( attachmentPair.ReferenceFrame.Rotation.ToHandedQuat() );

          m_f2.setLocalTranslate( attachmentPair.ConnectedFrame.Position.ToHandedVec3() );
          m_f2.setLocalRotate( attachmentPair.ConnectedFrame.Rotation.ToHandedQuat() );
        }

        m_native = new agxVehicle.WheelJoint( m_rb1, m_f1, null, m_f2 );
      }

      public void Dispose()
      {
        m_native.Dispose();
        m_rb1.Dispose();
        m_f1.Dispose();
        m_f2.Dispose();
      }

      private agxVehicle.WheelJoint m_native = null;
      private agx.RigidBody m_rb1 = null;
      private agx.Frame m_f1 = null;
      private agx.Frame m_f2 = null;
    }

    private static Mesh m_gizmosMesh = null;

    [HideInInspector]
    public static Mesh GizmosMesh
    {
      get
      {
        if ( m_gizmosMesh == null )
          m_gizmosMesh = Resources.Load<Mesh>( @"Debug/Models/arrow" );
        return m_gizmosMesh;
      }
    }

    private void DrawGizmos( Color color, bool selected )
    {
      if ( !DrawGizmosEnable || !isActiveAndEnabled )
        return;

      Vector3 pos = WheelAttachmentPoint;
      Gizmos.color = color;
      var scale = 0.3f *
        Utils.Math.Clamp(
          Rendering.Spawner.Utils.FindConstantScreenSizeScale( pos, Camera.current ),
          0.2f,
          2.0f );
      Gizmos.DrawMesh( GizmosMesh,
                       pos,
                       AttachmentPair.ReferenceFrame.Rotation,
                       scale * Vector3.one );

      if ( !AttachmentPair.Synchronized && selected ) {
        Gizmos.color = Color.red;
        Gizmos.DrawLine( AttachmentPair.ReferenceFrame.Position, AttachmentPair.ConnectedFrame.Position );
      }

      Vector3 up = SteeringAxis;

      up = up / 9 * scale * 2;
      var top = pos + up * 9;
      var right = Vector3.Cross(up, Camera.current.transform.forward).normalized * scale;

      var points = new List<Vector3>();

      points.Add( pos );
      points.Add( pos+up );
      for ( int i = 1; i < 4; i++ ) {
        points.Add( pos+up * ( i * 2 ) + right/5 );
        points.Add( pos+up * ( i * 2 + 1 ) - right/5 );
      }
      points.Add( top - up );
      points.Add( top );

      Gizmos.DrawLineStrip( points.ToArray(), false );
    }

    private void OnDrawGizmos() => DrawGizmos( Color.blue, false );
    private void OnDrawGizmosSelected() => DrawGizmos( Color.green, true );

  }
}
