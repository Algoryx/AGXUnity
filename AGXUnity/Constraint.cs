using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity
{
  /// <summary>
  /// Supported default constraint types.
  /// </summary>
  public enum ConstraintType
  {
    Hinge,
    Prismatic,
    LockJoint,
    CylindricalJoint,
    BallJoint,
    DistanceJoint,
    AngularLockJoint,
    PlaneJoint,
    Unknown
  }

  [AddComponentMenu( "" )]
  public class Constraint : ScriptComponent
  {
    /// <summary>
    /// Constraint solve types.
    /// </summary>
    public enum ESolveType
    {
      Direct,
      Iterative,
      DirectAndIterative
    }

    /// <summary>
    /// Controller type used to find controllers in a constraint. 'Primary'
    /// can be used for all constraints with controllers except Cylindrical joint.
    /// The Cylindrical joint has two of each controller. One along the translational
    /// axis and one along the rotational.
    /// </summary>
    public enum ControllerType
    {
      Primary = 0,
      Translational = 1,
      Rotational = 2
    }

    /// <summary>
    /// Translational degrees of freedom.
    /// </summary>
    public enum TranslationalDof
    {
      X,
      Y,
      Z,
      All
    }

    /// <summary>
    /// Rotational degrees of freedom.
    /// </summary>
    public enum RotationalDof
    {
      X,
      Y,
      Z,
      All
    }

    /// <summary>
    /// Create a new constraint given type and constraint frames.
    /// </summary>
    /// <param name="type">Constraint type.</param>
    /// <param name="referenceFrame">Reference frame.</param>
    /// <param name="connectedFrame">Connected frame.</param>
    /// <returns>Constraint component, added to a new game object - null if unsuccessful.</returns>
    public static Constraint Create( ConstraintType type,
                                     ConstraintFrame referenceFrame,
                                     ConstraintFrame connectedFrame )
    {
      if ( type == ConstraintType.Unknown ) {
        Debug.LogWarning( "Unable to create constraint - unknown constraint type." );
        return null;
      }

      GameObject constraintGameObject = new GameObject( Factory.CreateName( "AGXUnity." + type ) );
      try {
        Constraint constraint = constraintGameObject.AddComponent<Constraint>();
        constraint.Type = type;

        constraint.AttachmentPair.ReferenceFrame = referenceFrame ?? new ConstraintFrame();
        constraint.AttachmentPair.ConnectedFrame = connectedFrame ?? new ConstraintFrame();

        // Creating a temporary native instance of the constraint, including a rigid body and frames.
        // Given this native instance we copy the default configuration.
        using ( var tmpNative = new TemporaryNative( constraint.NativeType, constraint.AttachmentPair ) )
          constraint.TryAddElementaryConstraints( tmpNative.Instance );

        return constraint;
      }
      catch ( System.Exception e ) {
        Debug.LogException( e );
        DestroyImmediate( constraintGameObject );
        return null;
      }
    }

    /// <summary>
    /// Create a new constraint component given constraint type.
    /// </summary>
    /// <param name="type">Constraint type.</param>
    /// <param name="givenAttachmentPair">Optional initial attachment pair. When given,
    ///                                   values and fields will be copied to this objects
    ///                                   attachment pair.</param>
    /// <returns>Constraint component, added to a new game object - null if unsuccessful.</returns>
    public static Constraint Create( ConstraintType type, AttachmentPair givenAttachmentPair = null )
    {
      var instance = Create( type, new ConstraintFrame(), new ConstraintFrame() );
      if ( instance == null )
        return null;

      instance.AttachmentPair.CopyFrom( givenAttachmentPair );

      return instance;
    }

    /// <summary>
    /// Finds constraint type given native instance.
    /// </summary>
    /// <param name="native">Native instance.</param>
    /// <returns>ConstraintType of the native instance.</returns>
    public static ConstraintType FindType( agx.Constraint native )
    {
      return native                      == null ? ConstraintType.Unknown :
             native.asHinge()            != null ? ConstraintType.Hinge :
             native.asPrismatic()        != null ? ConstraintType.Prismatic :
             native.asLockJoint()        != null ? ConstraintType.LockJoint :
             native.asCylindricalJoint() != null ? ConstraintType.CylindricalJoint :
             native.asBallJoint()        != null ? ConstraintType.BallJoint :
             native.asDistanceJoint()    != null ? ConstraintType.DistanceJoint :
             native.asAngularLockJoint() != null ? ConstraintType.AngularLockJoint :
             native.asPlaneJoint()       != null ? ConstraintType.PlaneJoint :
                                                   ConstraintType.Unknown;
    }

    /// <summary>
    /// Converts native solve type to ESolveType.
    /// </summary>
    /// <param name="solveType">Native solve type.</param>
    /// <returns>ESolveType</returns>
    public static ESolveType Convert( agx.Constraint.SolveType solveType )
    {
      return solveType == agx.Constraint.SolveType.DIRECT    ? ESolveType.Direct :
             solveType == agx.Constraint.SolveType.ITERATIVE ? ESolveType.Iterative :
                                                               ESolveType.DirectAndIterative;
    }

    /// <summary>
    /// Converts constraint solve type to native version.
    /// </summary>
    /// <param name="solveType">Constraint solve type.</param>
    /// <returns>Native solve type.</returns>
    public static agx.Constraint.SolveType Convert( ESolveType solveType )
    {
      return solveType == ESolveType.Direct    ? agx.Constraint.SolveType.DIRECT :
             solveType == ESolveType.Iterative ? agx.Constraint.SolveType.ITERATIVE :
                                                 agx.Constraint.SolveType.DIRECT_AND_ITERATIVE;
    }

    [UnityEngine.Serialization.FormerlySerializedAs( "m_attachmentPair" )]
    [SerializeField]
    private ConstraintAttachmentPair m_attachmentPairLegacy = null;

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
        if ( m_attachmentPairComponent == null )
          m_attachmentPairComponent = GetComponent<AttachmentPair>();

        // Creates attachment pair if it doesn't exist.
        if ( m_attachmentPairComponent == null ) {
          // Will add itself as component to our game object.
          m_attachmentPairComponent = AttachmentPair.Create( gameObject );
          m_attachmentPairComponent.CopyFrom( m_attachmentPairLegacy );
          m_attachmentPairLegacy = null;
        }

        return m_attachmentPairComponent;
      }
    }

    /// <summary>
    /// Type of this constraint. Paired with property Type.
    /// </summary>
    [SerializeField]
    private ConstraintType m_type = ConstraintType.Hinge;

    /// <summary>
    /// Type of this constraint.
    /// </summary>
    [HideInInspector]
    public ConstraintType Type
    {
      get { return m_type; }
      private set
      {
        m_type = value;
      }
    }

    /// <summary>
    /// Collision state when the simulation is running.
    /// </summary>
    public enum ECollisionsState
    {
      /// <summary>
      /// Do nothing - preserves the current external state.
      /// </summary>
      KeepExternalState,
      /// <summary>
      /// Disables selected Reference object against selected Connected.
      /// </summary>
      DisableReferenceVsConnected,
      /// <summary>
      /// Disables the rigid bodies. If the second object hasn't got a
      /// rigid body - all child shapes in Connected will be disabled
      /// against the first rigid body.
      /// </summary>
      DisableRigidBody1VsRigidBody2
    }

    /// <summary>
    /// Collisions state when the simulation is running.
    /// </summary>
    [SerializeField]
    private ECollisionsState m_collisionsState = ECollisionsState.KeepExternalState;

    /// <summary>
    /// Collisions state when the simulation is running.
    /// </summary>
    [HideInInspector]
    public ECollisionsState CollisionsState
    {
      get { return m_collisionsState; }
      set { m_collisionsState = value; }
    }

    [SerializeField]
    private ESolveType m_solveType = ESolveType.Direct;

    /// <summary>
    /// Solve type of this constraint.
    /// </summary>
    [HideInInspector]
    public ESolveType SolveType
    {
      get { return m_solveType; }
      set
      {
        m_solveType = value;
        if ( Native != null )
          Native.setSolveType( Convert( m_solveType ) );
      }
    }

    /// <summary>
    /// Draw gizmos flag - paired with DrawGizmosEnable.
    /// </summary>
    [SerializeField]
    private bool m_drawGizmosEnable = true;

    private bool m_isAnimated = false;

    /// <summary>
    /// Enable/disable gizmos drawing of this constraint. Enabled by default.
    /// </summary>
    [HideInInspector]
    public bool DrawGizmosEnable { get { return m_drawGizmosEnable; } set { m_drawGizmosEnable = value; } }

    /// <summary>
    /// Type of the native instance constructed from agxDotNet.dll and current ConstraintType.
    /// </summary>
    public Type NativeType { get { return System.Type.GetType( "agx." + m_type + ", agxDotNet" ); } }

    /// <summary>
    /// Native instance if this constraint is initialized - otherwise null.
    /// </summary>
    public agx.Constraint Native { get; private set; }

    /// <summary>
    /// True if game object is active in hierarchy and this component is enabled.
    /// </summary>
    [HideInInspector]
    public bool IsEnabled { get { return gameObject.activeInHierarchy && enabled; } }

    [SerializeField]
    private bool m_connectedFrameNativeSyncEnabled = false;
    /// <summary>
    /// True to enable synchronization of the connected frame to the native constraint (default: false/disabled).
    /// </summary>
    [HideInInspector]
    public bool ConnectedFrameNativeSyncEnabled { get { return m_connectedFrameNativeSyncEnabled; } set { m_connectedFrameNativeSyncEnabled = value; } }

    /// <summary>
    /// List of elementary constraints in this constraint - controllers and ordinary.
    /// </summary>
    [SerializeField]
    private List<ElementaryConstraint> m_elementaryConstraints = new List<ElementaryConstraint>();

    /// <summary>
    /// Array of elementary constraints in this constraint - controllers and ordinary.
    /// </summary>
    [HideInInspector]
    public ElementaryConstraint[] ElementaryConstraints { get { return m_elementaryConstraints.ToArray(); } }

    /// <summary>
    /// Finds and returns an array of ordinary ElementaryConstraint objects, i.e., the ones
    /// that aren't controllers.
    /// </summary>
    /// <returns>Array of ordinary elementary constraints.</returns>
    public ElementaryConstraint[] GetOrdinaryElementaryConstraints()
    {
      return ( from ec
               in m_elementaryConstraints
               where ec as ElementaryConstraintController == null &&
                    !ec.NativeName.StartsWith( "F" ) // Ignoring friction controller from versions
                                                     // it wasn't implemented in.
               select ec ).ToArray();
    }

    /// <summary>
    /// Finds and returns an array of controller elementary constraints, such as motor, lock, range etc.
    /// </summary>
    /// <returns>Array of controllers - if present.</returns>
    public ElementaryConstraintController[] GetElementaryConstraintControllers()
    {
      return ( from ec
               in m_elementaryConstraints
               where ec is ElementaryConstraintController
               select ec as ElementaryConstraintController ).ToArray();
    }

    /// <summary>
    /// Find controller of given type and dimension. Asking for the controller of a
    /// hinge and a prismatic with <paramref name="controllerType"/> == Primary will
    /// always be valid. The same with <paramref name="controllerType"/> == Rotational
    /// and the prismatic controller will be null, since it's Translational.
    /// </summary>
    /// <typeparam name="T">Type of the controller.</typeparam>
    /// <param name="controllerType">Working dimension of the controller. Primary for "first".</param>
    /// <returns>Controller of given type and working dimension - if present, otherwise null.</returns>
    public T GetController<T>( ControllerType controllerType = ControllerType.Primary ) where T : ElementaryConstraintController
    {
      var controllers = GetElementaryConstraintControllers();
      for ( int i = 0; i < controllers.Length; ++i ) {
        T controller = controllers[ i ].As<T>( controllerType );
        if ( controller != null )
          return controller;
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
      var rowParser = ConstraintUtils.ConstraintRowParser.Create( this );
      var rows = typeof( T ) == typeof( TranslationalDof ) ?
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

    /// <summary>
    /// Set compliance to all ordinary degrees of freedom (not including controllers)
    /// of this constraint.
    /// </summary>
    /// <param name="compliance">New compliance.</param>
    public void SetCompliance( float compliance )
    {
      TraverseRowData( data => data.Compliance = compliance, TranslationalDof.All );
      TraverseRowData( data => data.Compliance = compliance, RotationalDof.All );
    }

    /// <summary>
    /// Set compliance to one or all translational ordinary degrees of freedom
    /// (not including controllers) of this constraint.
    /// </summary>
    /// <param name="compliance">New compliance.</param>
    /// <param name="dof">Specific translational degree of freedom or all.</param>
    public void SetCompliance( float compliance, TranslationalDof dof )
    {
      TraverseRowData( data => data.Compliance = compliance, dof );
    }

    /// <summary>
    /// Set compliance to one or all rotational ordinary degrees of freedom
    /// (not including controllers) of this constraint.
    /// </summary>
    /// <param name="compliance">New compliance.</param>
    /// <param name="dof">Specific rotational degree of freedom or all.</param>
    public void SetCompliance( float compliance, RotationalDof dof )
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
      TraverseRowData( data => data.Damping = damping, TranslationalDof.All );
      TraverseRowData( data => data.Damping = damping, RotationalDof.All );
    }

    /// <summary>
    /// Set damping to one or all translational ordinary degrees of freedom
    /// (not including controllers) of this constraint.
    /// </summary>
    /// <param name="damping">New damping.</param>
    /// <param name="dof">Specific translational degree of freedom or all.</param>
    public void SetDamping( float damping, TranslationalDof dof )
    {
      TraverseRowData( data => data.Damping = damping, dof );
    }

    /// <summary>
    /// Set damping to one or all rotational ordinary degrees of freedom
    /// (not including controllers) of this constraint.
    /// </summary>
    /// <param name="damping">New damping.</param>
    /// <param name="dof">Specific rotational degree of freedom or all.</param>
    public void SetDamping( float damping, RotationalDof dof )
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
      TraverseRowData( data => data.ForceRange = forceRange, TranslationalDof.All );
      TraverseRowData( data => data.ForceRange = forceRange, RotationalDof.All );
    }

    /// <summary>
    /// Set force range to one or all translational ordinary degrees of freedom
    /// (not including controllers) of this constraint.
    /// </summary>
    /// <param name="forceRange">New force range.</param>
    /// <param name="dof">Specific translational degree of freedom or all.</param>
    public void SetForceRange( RangeReal forceRange, TranslationalDof dof )
    {
      TraverseRowData( data => data.ForceRange = forceRange, dof );
    }

    /// <summary>
    /// Set force range to one or all rotational ordinary degrees of freedom
    /// (not including controllers) of this constraint.
    /// </summary>
    /// <param name="forceRange">New force range.</param>
    /// <param name="dof">Specific rotational degree of freedom or all.</param>
    public void SetForceRange( RangeReal forceRange, RotationalDof dof )
    {
      TraverseRowData( data => data.ForceRange = forceRange, dof );
    }

    /// <summary>
    /// Calculates the current angle for given degree of freedom when this
    /// constraint is active.
    /// </summary>
    /// <param name="controllerType">Working dimension (translational or rotational). It's
    ///                              normally enough with Primary if this constraint isn't
    ///                              a CylindricalJoint. If cylindrical - primary == Translational.</param>
    /// <returns>Current angle of the active constraint.</returns>
    public float GetCurrentAngle( ControllerType controllerType = ControllerType.Primary )
    {
      if ( Native != null ) {
        var c1d = agx.Constraint1DOF.safeCast( Native );
        if ( c1d != null )
          return System.Convert.ToSingle( c1d.getAngle() );

        var c2d = agx.Constraint2DOF.safeCast( Native );
        if ( c2d != null )
          return controllerType == ControllerType.Primary || controllerType == ControllerType.Translational ?
                   System.Convert.ToSingle( c2d.getAngle( agx.Constraint2DOF.DOF.FIRST ) ) :
                   System.Convert.ToSingle( c2d.getAngle( agx.Constraint2DOF.DOF.SECOND ) );
      }

      return 0.0f;
    }

    /// <summary>
    /// Calculates the current speed for given degree of freedom when this
    /// constraint is active.
    /// </summary>
    /// <param name="controllerType">Working dimension (translational or rotational). It's
    ///                              normally enough with Primary if this constraint isn't
    ///                              a CylindricalJoint. If cylindrical - primary == Translational.</param>
    /// <returns>Current speed of the active constraint.</returns>
    public float GetCurrentSpeed( ControllerType controllerType = ControllerType.Primary )
    {
      if ( Native != null ) {
        var c1d = agx.Constraint1DOF.safeCast( Native );
        if ( c1d != null )
          return System.Convert.ToSingle( c1d.getCurrentSpeed() );

        var c2d = agx.Constraint2DOF.safeCast( Native );
        if ( c2d != null )
          return controllerType == ControllerType.Primary || controllerType == ControllerType.Translational ?
                   System.Convert.ToSingle( c2d.getCurrentSpeed( agx.Constraint2DOF.DOF.FIRST ) ) :
                   System.Convert.ToSingle( c2d.getCurrentSpeed( agx.Constraint2DOF.DOF.SECOND ) );
      }

      return 0.0f;
    }

    /// <summary>
    /// Transforms this instance from a version where the ElementaryConstraint instances
    /// were ScriptAsset to the new version where the ElementaryConstraint is ScriptComponent.
    /// All values are copied.
    /// </summary>
    /// <returns></returns>
    public bool TransformToComponentVersion()
    {
      if ( m_elementaryConstraints.Count == 0 || GetComponents<ElementaryConstraint>().Length > 0 )
        return false;

      List<ElementaryConstraint> newElementaryConstraints = new List<ElementaryConstraint>();
      foreach ( var old in m_elementaryConstraints )
        newElementaryConstraints.Add( old.FromLegacy( gameObject ) );

      foreach ( var old in m_elementaryConstraints )
        DestroyImmediate( old );
      m_elementaryConstraints.Clear();

      m_elementaryConstraints = newElementaryConstraints;

      return true;
    }

    /// <summary>
    /// Patches primary and secondary elementary constraints to the current version of AGX.
    /// </summary>
    /// <param name="native">If given, the native configuration may be used.</param>
    /// <returns>True if modification were applied - otherwise false.</returns>
    public bool VerifyImplementation()
    {
      if ( Type == ConstraintType.Hinge ) {
        var swing = m_elementaryConstraints.FirstOrDefault( ec => ec.NativeName == "SW" );
        // Already created with swing - hinge is up to date.
        if ( swing != null )
          return false;

        var ecUn = m_elementaryConstraints.FirstOrDefault( ec => ec.NativeName == "D1_UN" );
        var ecVn = m_elementaryConstraints.FirstOrDefault( ec => ec.NativeName == "D1_VN" );
        // Not swing nor dot1's - this is an unknown configuration.
        if ( ecUn == null || ecVn == null ) {
          Debug.LogWarning( "Trying to patch hinge but the elementary constraint configuration is undefined.", this );
          return false;
        }

        using ( var nativeHinge = new TemporaryNative( NativeType ) ) {
          swing = ElementaryConstraint.Create( gameObject, nativeHinge.Instance.getElementaryConstraintGivenName( "SW" ) );
        }

        if ( swing == null ) {
          Debug.LogWarning( "Unable to find elementary constraint \"SW\" in native hinge implementation.", this );
          return false;
        }

        swing.Enable = ecUn.Enable || ecVn.Enable;
        swing.RowData[ 0 ].CopyFrom( ecUn.RowData[ 0 ] );
        swing.RowData[ 1 ].CopyFrom( ecVn.RowData[ 0 ] );

        m_elementaryConstraints.Insert( m_elementaryConstraints.IndexOf( ecUn ), swing );
        m_elementaryConstraints.Remove( ecUn );
        m_elementaryConstraints.Remove( ecVn );

        DestroyImmediate( ecUn );
        DestroyImmediate( ecVn );

        return true;
      }

      return false;
    }

    /// <summary>
    /// Internal method which constructs this constraint given elementary constraints
    /// in the native instance. Throws if an elementary constraint fails to initialize.
    /// </summary>
    /// <param name="native">Native instance.</param>
    /// <param name="onObjectCreated">Optional callback when elementary constraint has been created.</param>
    public void TryAddElementaryConstraints( agx.Constraint native,
                                             Action<UnityEngine.Object> onObjectCreated = null )
    {
      if ( native == null )
        throw new ArgumentNullException( "native", "Native constraint is null." );

      m_elementaryConstraints.Clear();

      for ( uint i = 0; i < native.getNumElementaryConstraints(); ++i ) {
        if ( native.getElementaryConstraint( i ).getName() == "" )
          throw new Exception( "Native elementary constraint doesn't have a name." );

        var ec = ElementaryConstraint.Create( gameObject, native.getElementaryConstraint( i ) );
        if ( ec == null )
          throw new Exception( "Failed to configure elementary constraint with name: " + native.getElementaryConstraint( i ).getName() + "." );

        onObjectCreated?.Invoke( ec );

        m_elementaryConstraints.Add( ec );
      }

      for ( uint i = 0; i < native.getNumSecondaryConstraints(); ++i ) {
        if ( native.getSecondaryConstraint( i ).getName() == "" )
          throw new Exception( "Native secondary constraint doesn't have a name." );

        var sc = ElementaryConstraint.Create( gameObject, native.getSecondaryConstraint( i ) );
        if ( sc == null )
          throw new Exception( "Failed to configure elementary controller constraint with name: " + native.getElementaryConstraint( i ).getName() + "." );

        onObjectCreated?.Invoke( sc );

        m_elementaryConstraints.Add( sc );
      }
    }

    /// <summary>
    /// Assign constraint type given this constraint hasn't been constructed yet.
    /// </summary>
    /// <param name="type">Constraint type.</param>
    /// <param name="force">Force change, i.e., ignore that this constraint has been initialized.</param>
    public void SetType( ConstraintType type, bool force )
    {
      if ( !force && m_elementaryConstraints.Count > 0 ) {
        Debug.LogWarning( "Not possible to change constraint type when the constraint has been constructed. Ignoring new type.", this );
        return;
      }

      Type = type;
    }

    /// <summary>
    /// Change constraint type. Note that all values will be default
    /// when the type has changed.
    /// </summary>
    /// <param name="type">New type of the constraint.</param>
    /// <param name="onObjectCreated">Optional callback when an object has been created.</param>
    /// <param name="destroyObject">
    /// Optional callback to destroy an object - Object.DestroyImmediate
    /// is used by default.
    /// </param>
    public void ChangeType( ConstraintType type,
                            Action<UnityEngine.Object> onObjectCreated = null,
                            Action<UnityEngine.Object> destroyObject = null )
    {
      if ( Native != null ) {
        Debug.LogWarning( "Invalid to change type of an initialized constraint.", this );
        return;
      }

      foreach ( var elementaryConstraint in m_elementaryConstraints ) {
        if ( destroyObject != null )
          destroyObject( elementaryConstraint );
        else
          DestroyImmediate( elementaryConstraint );
      }

      m_elementaryConstraints.Clear();

      SetType( type, true );

      if ( type == ConstraintType.Unknown )
        return;

      using ( var tempNative = new TemporaryNative( NativeType, AttachmentPair ) )
        TryAddElementaryConstraints( tempNative.Instance, onObjectCreated );
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

      if ( Type == ConstraintType.Unknown ) {
        Debug.LogError( "Unable to initialize constraint - constraint type is Unknown.",
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
        Native = (agx.Constraint)Activator.CreateInstance( NativeType,
                                                           new object[]
                                                           {
                                                             rb1.Native,
                                                             f1,
                                                             ( rb2 != null ? rb2.Native : null ),
                                                             f2
                                                           } );

        // Assigning native elementary constraints to our elementary constraint instances.
        foreach ( ElementaryConstraint ec in ElementaryConstraints )
          if ( !ec.OnConstraintInitialize( this ) )
            throw new Exception( "Unable to initialize elementary constraint: " +
                                 ec.NativeName +
                                 " (not present in native constraint). ConstraintType: " + Type );

        bool added = GetSimulation().add( Native );
        Native.setEnable( IsEnabled );

        // Not possible to handle collisions if connected frame parent is null/world.
        if ( CollisionsState != ECollisionsState.KeepExternalState && AttachmentPair.ConnectedObject != null ) {
          string groupName = gameObject.name + "_" + gameObject.GetInstanceID().ToString();
          GameObject go1   = null;
          GameObject go2   = null;
          if ( CollisionsState == ECollisionsState.DisableReferenceVsConnected ) {
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

    private void Reset()
    {
      Type = ConstraintType.Unknown;
    }

    private void OnPreStepForwardUpdate()
    {
      if ( Native == null || !Native.getValid() )
        return;

      SynchronizeNativeFramesWithAttachmentPair();

      if (m_isAnimated) {
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

    private class TemporaryNative : IDisposable
    {
      public agx.Constraint Instance { get { return m_native; } }

      public TemporaryNative( Type nativeType, AttachmentPair attachmentPair = null )
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

        m_native = (agx.Constraint)Activator.CreateInstance( nativeType, new object[] { m_rb1, m_f1, null, m_f2 } );
      }

      public void Dispose()
      {
        m_native.Dispose();
        m_rb1.Dispose();
        m_f1.Dispose();
        m_f2.Dispose();
      }

      private agx.Constraint m_native = null;
      private agx.RigidBody m_rb1 = null;
      private agx.Frame m_f1 = null;
      private agx.Frame m_f2 = null;
    }

    private static Mesh m_gizmosMesh = null;
    public static Mesh GetOrCreateGizmosMesh()
    {
      // Unity crashes before first scene view frame has been rendered on startup
      // if we load resources. Wait some time before we show this gizmo...
      //if ( !Application.isPlaying && Time.realtimeSinceStartup < 30.0f )
      //  return null;

      if ( m_gizmosMesh != null )
        return m_gizmosMesh;

      GameObject tmp = Resources.Load<GameObject>( @"Debug/ConstraintRenderer" );
      MeshFilter[] filters = tmp.GetComponentsInChildren<MeshFilter>();
      CombineInstance[] combine = new CombineInstance[ filters.Length ];

      for ( int i = 0; i < filters.Length; ++i ) {
        combine[ i ].mesh = filters[ i ].sharedMesh;
        combine[ i ].transform = filters[ i ].transform.localToWorldMatrix;
      }

      m_gizmosMesh = new Mesh();
      m_gizmosMesh.CombineMeshes( combine );

      return m_gizmosMesh;
    }

    private static void DrawGizmos( Color color, AttachmentPair attachmentPair, bool selected )
    {
      Gizmos.color = color;
      Gizmos.DrawMesh( GetOrCreateGizmosMesh(),
                       attachmentPair.ReferenceFrame.Position,
                       attachmentPair.ReferenceFrame.Rotation * Quaternion.FromToRotation( Vector3.up, Vector3.forward ),
                       0.3f * Utils.Math.Clamp( Rendering.Spawner.Utils.FindConstantScreenSizeScale( attachmentPair.ReferenceFrame.Position,
                                                                                                     Camera.current ),
                                                0.2f,
                                                2.0f ) * Vector3.one );

      if ( !attachmentPair.Synchronized && selected ) {
        Gizmos.color = Color.red;
        Gizmos.DrawLine( attachmentPair.ReferenceFrame.Position, attachmentPair.ConnectedFrame.Position );
      }
    }

    private void OnDrawGizmos()
    {
      if ( !DrawGizmosEnable || !IsEnabled )
        return;

      DrawGizmos( Color.blue, AttachmentPair, false );
    }

    private void OnDrawGizmosSelected()
    {
      if ( !DrawGizmosEnable || !IsEnabled )
        return;

      DrawGizmos( Color.green, AttachmentPair, true );
    }
  }
}
