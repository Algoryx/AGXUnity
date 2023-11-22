using System;
using System.Linq;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity
{
  [AddComponentMenu( "" )]
  public class PickHandler : UniqueGameObject<PickHandler>
  {
    [HideInInspector]
    public static Color ReferenceSphereColor { get { return Color.HSVToRGB( 0.02f, 0.78f, 0.95f ); } }

    [HideInInspector]
    public static Color ConnectedSphereColor { get { return Color.HSVToRGB( 0.02f, 0.78f, 0.95f ); } }

    [HideInInspector]
    public static Color ConnectingCylinderColor { get { return Color.HSVToRGB( 0.02f, 0.78f, 0.95f ); } }

    public static float FindDistanceFromCamera( Camera camera, Vector3 worldPoint )
    {
      return camera.WorldToViewportPoint( worldPoint ).z;
    }

    public static void SetComplianceDamping( Constraint constraint )
    {
      if ( constraint == null )
        return;

      RigidBody rb1 = constraint.AttachmentPair.ReferenceObject.GetComponentInParent<RigidBody>();
      if ( rb1 == null )
        return;

      float mass    = rb1.MassProperties.Mass.Value;
      float distVal = Vector3.SqrMagnitude( constraint.AttachmentPair.ReferenceFrame.Position - constraint.AttachmentPair.ConnectedFrame.Position ) + 0.1f;
      distVal       = distVal > 1.5f ? distVal * distVal : distVal;

      float translationalCompliance = 1.0E-3f / ( distVal * Mathf.Max( mass, 1.0f ) );
      float rotationalCompliance    = 1.0E-10f / ( Mathf.Max( mass, 1.0f ) );
      float damping                 = 10.0f / 60.0f;

      var rowParser = ConstraintUtils.ConstraintRowParser.Create( constraint );
      if ( rowParser == null )
        return;

      foreach ( var translationalRow in rowParser.TranslationalRows ) {
        if ( translationalRow == null )
          continue;

        translationalRow.RowData.Compliance = translationalCompliance;
        translationalRow.RowData.Damping = damping;
      }

      foreach ( var rotationalRow in rowParser.RotationalRows ) {
        if ( rotationalRow == null )
          continue;

        rotationalRow.RowData.Compliance = rotationalCompliance;
        rotationalRow.RowData.Damping = damping;
      }
    }

    [Flags]
    public enum DofTypes
    {
      Translation = 1 << 0,
      Rotation    = 1 << 1
    }

    public enum MouseButton
    {
      None   = -1,
      Left   =  0,
      Right  =  1,
      Middle =  2
    }

    [SerializeField]
    private KeyCode m_triggerKey = KeyCode.A;
    public KeyCode TriggerKey { get { return m_triggerKey; } set { m_triggerKey = value; } }

    [SerializeField]
    private MouseButton m_ballJointMouseButton = MouseButton.Left;
    public MouseButton BallJointMouseButton
    {
      get { return m_ballJointMouseButton; }
      set
      {
        m_ballJointMouseButton = OnMouseButton( m_ballJointMouseButton, value );
      }
    }

    [SerializeField]
    private MouseButton m_lockJointMouseButton = MouseButton.Middle;
    public MouseButton LockJointMouseButton
    {
      get { return m_lockJointMouseButton; }
      set
      {
        m_lockJointMouseButton = OnMouseButton( m_lockJointMouseButton, value );
      }
    }

    [SerializeField]
    private MouseButton m_angularLockJointMouseButton = MouseButton.Right;
    public MouseButton AngularLockJointMouseButton
    {
      get { return m_angularLockJointMouseButton; }
      set
      {
        m_angularLockJointMouseButton = OnMouseButton( m_angularLockJointMouseButton, value );
      }
    }

    [SerializeField]
    private GameObject m_mainCamera = null;
    public GameObject MainCamera
    {
      get { return m_mainCamera; }
      set { m_mainCamera = value; }
    }

    public DofTypes MouseButtonToDofTypes( MouseButton button )
    {
      return button == BallJointMouseButton ?
               DofTypes.Translation :
             button == LockJointMouseButton ?
               DofTypes.Translation | DofTypes.Rotation :
             button == AngularLockJointMouseButton ?
               DofTypes.Rotation :
               DofTypes.Translation;
    }

    [HideInInspector]
    public GameObject ConstraintGameObject { get; private set; }

    [HideInInspector]
    public Constraint Constraint { get { return ConstraintGameObject != null ? ConstraintGameObject.GetComponent<Constraint>() : null; } }

    private class MouseButtonState
    {
      private bool[] m_isDown = new bool[ 3 ];
      private Action<MouseButton>[] m_buttonUpListeners = new Action<MouseButton>[ 3 ] { null, null, null };

      public MouseButton ButtonDown { get { return (MouseButton)Array.IndexOf( m_isDown, true ); } }

      public void Update( Event current, bool swallowEvent )
      {
        if ( !current.isMouse )
          return;

        if ( current.type != EventType.MouseDown && current.type != EventType.MouseUp )
          return;

        if ( current.type == EventType.MouseDown )
          m_isDown[ current.button ] = true;
        else {
          m_isDown[ current.button ] = false;
          if ( m_buttonUpListeners[ current.button ] != null )
            m_buttonUpListeners[ current.button ]( (MouseButton)current.button );
          m_buttonUpListeners[ current.button ] = null;
        }

        if ( swallowEvent )
          current.Use();
      }

      public void Use( MouseButton button, Action<MouseButton> onButtonUp )
      {
        m_buttonUpListeners[ (int)button ] = onButtonUp;
      }
    }

    private MouseButtonState m_mouseButtonState = new MouseButtonState();
    private float m_distanceFromCamera = -1f;
    private Camera m_camera = null;
    private agxCollide.Geometry m_lineGeometry = null;
    private agxCollide.Line m_lineShape = null;

    protected override void OnEnable()
    {
      Simulation.Instance.StepCallbacks.PreStepForward += OnPreStepForwardCallback;
      Simulation.Instance.StepCallbacks.SimulationPre  += OnSimulationPre;
      if ( MainCamera != null )
        m_camera = MainCamera.GetComponent<Camera>();

      m_lineShape = new agxCollide.Line( new agx.Vec3(), new agx.Vec3( 0, 0, 1 ) );
      m_lineGeometry = new agxCollide.Geometry( m_lineShape );
      m_lineGeometry.setEnable( false );
      m_lineGeometry.setSensor( true );
      GetSimulation().add( m_lineGeometry );
    }

    protected override void OnDisable()
    {
      if ( Simulation.HasInstance ) {
        Simulation.Instance.StepCallbacks.PreStepForward -= OnPreStepForwardCallback;
        Simulation.Instance.StepCallbacks.SimulationPre  -= OnSimulationPre;

        GetSimulation().remove( m_lineGeometry );
      }

      if ( ConstraintGameObject != null )
        Destroy( ConstraintGameObject );

      ConstraintGameObject = null;
      m_distanceFromCamera = -1f;
      m_camera             = null;
      m_lineShape          = null;
      m_lineGeometry       = null;
    }

    private void OnGUI()
    {
      m_mouseButtonState.Update( Event.current, Input.GetKey( TriggerKey ) );
    }

    private void OnSimulationPre()
    {
      if ( ConstraintGameObject == null && m_lineGeometry.isEnabled() ) {
        var geometryContacts = new agxCollide.GeometryContactPtrVector();
        GetSimulation().getSpace().getGeometryContacts( geometryContacts, m_lineGeometry );
        var closestGeometryContact = geometryContacts.FirstOrDefault( gc => gc.points().size() > 0 );
        if ( closestGeometryContact != null ) {
          var ray = m_camera.ScreenPointToRay( Input.mousePosition );
          var rayHandedOrigin = ray.origin.ToHandedVec3();
          var closestDistance2 = rayHandedOrigin.distance2( closestGeometryContact.points().at( 0 ).point );
          for ( int i = 1; i < geometryContacts.Count; ++i ) {
            var gc = geometryContacts[ i ];
            var points = gc.points();
            if ( gc == closestGeometryContact || points.size() == 0 )
              continue;

            var point = points.at( 0u );
            var distance2 = rayHandedOrigin.distance2( point.point );
            if ( distance2 < closestDistance2 ) {
              closestDistance2 = distance2;
              closestGeometryContact = gc;
            }
          }

          var raycastBody = closestGeometryContact.rigidBody( 0 );
          RigidBody body = null;
          if ( raycastBody != null && raycastBody.getMotionControl() == agx.RigidBody.MotionControl.DYNAMICS ) {
            var bodies = FindObjectsOfType<RigidBody>();
            for ( int i = 0; body == null && i < bodies.Length; ++i )
              if ( bodies[ i ].Native != null && bodies[ i ].Native.getUuid().str() == raycastBody.getUuid().str() )
                body = bodies[ i ];
          }

          ConstraintGameObject = TryCreateConstraint( closestGeometryContact.points().at( 0 ).point.ToHandedVector3(),
                                                      body,
                                                      MouseButtonToDofTypes( m_mouseButtonState.ButtonDown ),
                                                      "PickHandlerConstraint" );

          if ( ConstraintGameObject != null ) {
            ConstraintGameObject.AddComponent<Rendering.PickHandlerRenderer>();
            Constraint.DrawGizmosEnable = false;
            m_distanceFromCamera = FindDistanceFromCamera( m_camera,
                                                           Constraint.AttachmentPair.ReferenceFrame.Position );
          }

          m_lineGeometry.setEnable( false );
        }
      }
    }

    private void OnPreStepForwardCallback()
    {
      // Trigger key + mouse button down and we enable the line geometry
      // here - similar to preCollide. In pre-step event if the line
      // geometry is enabled, collect geometry contacts with our line
      // and create constraint with the closest point.

      if ( m_camera == null )
        return;

      if ( ConstraintGameObject == null && Input.GetKey( TriggerKey ) && m_mouseButtonState.ButtonDown != MouseButton.None ) {
        var ray = m_camera.ScreenPointToRay( Input.mousePosition );
        m_lineShape.set( ray.origin.ToHandedVec3(), ray.GetPoint( 5000.0f ).ToHandedVec3() );
        m_lineGeometry.setEnable( true );

        m_mouseButtonState.Use( m_mouseButtonState.ButtonDown, buttonUp =>
        {
          if ( ConstraintGameObject != null ) {
            DestroyImmediate( ConstraintGameObject );
            ConstraintGameObject = null;
            m_distanceFromCamera = -1f;
          }
          m_lineGeometry.setEnable( false );
        } );
      }

      if ( ConstraintGameObject != null ) {
        Constraint.AttachmentPair.ConnectedFrame.Position = m_camera.ScreenToWorldPoint( new Vector3( Input.mousePosition.x,
                                                                                                      Input.mousePosition.y,
                                                                                                      m_distanceFromCamera ) );

        SetComplianceDamping( Constraint );

        ConstraintGameObject.GetComponent<Rendering.PickHandlerRenderer>().ThisMethodIsntAllowedToBeNamedUpdateByUnity( Constraint, m_camera );
      }
    }

    private MouseButton OnMouseButton( MouseButton oldValue, MouseButton newValue )
    {
      if ( oldValue == newValue )
        return newValue;

      if ( newValue == m_ballJointMouseButton )
        m_ballJointMouseButton = oldValue;
      else if ( newValue == m_lockJointMouseButton )
        m_lockJointMouseButton = oldValue;
      else if ( newValue == m_angularLockJointMouseButton )
        m_angularLockJointMouseButton = oldValue;

      return newValue;
    }

    private GameObject TryCreateConstraint( Vector3 worldPoint, RigidBody rb, DofTypes constrainedDofs, string gameObjectName )
    {
      if ( rb == null )
        return null;

      var constraintType = constrainedDofs == DofTypes.Translation ?
                             ConstraintType.BallJoint :
                           constrainedDofs == DofTypes.Rotation ?
                             ConstraintType.AngularLockJoint :
                           ( constrainedDofs & DofTypes.Translation ) != 0 && ( constrainedDofs & DofTypes.Rotation ) != 0 ?
                             ConstraintType.LockJoint :
                             ConstraintType.BallJoint;

      var constraintGameObject = Factory.Create( constraintType );
      constraintGameObject.name = gameObjectName;

      var constraint = constraintGameObject.GetComponent<Constraint>();
      constraint.ConnectedFrameNativeSyncEnabled = true;
      constraint.AttachmentPair.ReferenceObject = rb.gameObject;
      constraint.AttachmentPair.ReferenceFrame.Position = worldPoint;

      constraint.AttachmentPair.ConnectedObject = null;
      constraint.AttachmentPair.ConnectedFrame.Position = worldPoint;

      constraint.AttachmentPair.Synchronized = false;

      return constraintGameObject;
    }
  }
}
