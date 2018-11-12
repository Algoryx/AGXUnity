using System;
using System.Collections.Generic;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity
{
  public class PickHandler : UniqueGameObject<PickHandler>
  {
    [HideInInspector]
    public static Color ReferenceSphereColor { get { return Color.HSVToRGB( 0.02f, 0.78f, 0.95f ); } }

    [HideInInspector]
    public static Color ConnectedSphereColor { get { return Color.HSVToRGB( 0.02f, 0.78f, 0.95f ); } }

    [HideInInspector]
    public static Color ConnectingCylinderColor { get { return Color.HSVToRGB( 0.02f, 0.78f, 0.95f ); } }

    public static GameObject TryCreateConstraint( Ray ray, GameObject gameObject, DofTypes constrainedDofs, string gameObjectName )
    {
      if ( gameObject == null || gameObject.GetComponentInParent<RigidBody>() == null )
        return null;

      var hit = Raycast.Test( gameObject, ray );
      if ( !hit.Triangle.Valid )
        return null;

      ConstraintType constraintType = constrainedDofs == DofTypes.Translation ?
                                        ConstraintType.BallJoint :
                                      constrainedDofs == DofTypes.Rotation ?
                                        ConstraintType.AngularLockJoint :
                                      ( constrainedDofs & DofTypes.Translation ) != 0 && ( constrainedDofs & DofTypes.Rotation ) != 0 ?
                                        ConstraintType.LockJoint :
                                        ConstraintType.BallJoint;

      GameObject constraintGameObject = Factory.Create( constraintType );
      constraintGameObject.name       = gameObjectName;

      Constraint constraint                             = constraintGameObject.GetComponent<Constraint>();
      constraint.ConnectedFrameNativeSyncEnabled        = true;
      constraint.AttachmentPair.ReferenceObject         = hit.Triangle.Target;
      constraint.AttachmentPair.ReferenceFrame.Position = hit.Triangle.Point;

      constraint.AttachmentPair.ConnectedObject         = null;
      constraint.AttachmentPair.ConnectedFrame.Position = hit.Triangle.Point;

      constraint.AttachmentPair.Synchronized            = false;

      return constraintGameObject;
    }

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

    protected override void OnEnable()
    {
      Simulation.Instance.StepCallbacks.PreStepForward += OnPreStepForwardCallback;
      if ( MainCamera != null )
        m_camera = MainCamera.GetComponent<Camera>();
    }

    protected override void OnDisable()
    {
      if ( Simulation.HasInstance )
        Simulation.Instance.StepCallbacks.PreStepForward -= OnPreStepForwardCallback;

      if ( ConstraintGameObject != null )
        Destroy( ConstraintGameObject );
      ConstraintGameObject = null;
      m_distanceFromCamera = -1f;
      m_camera = null;
    }

    private void OnGUI()
    {
      m_mouseButtonState.Update( Event.current, Input.GetKey( TriggerKey ) );
    }

    private void OnPreStepForwardCallback()
    {
      if ( m_camera == null )
        return;

      if ( ConstraintGameObject == null && Input.GetKey( TriggerKey ) && m_mouseButtonState.ButtonDown != MouseButton.None ) {
        Ray ray = m_camera.ScreenPointToRay( Input.mousePosition );
        List<BroadPhaseResult> results = FindRayBoundingVolumeOverlaps( ray );
        if ( results.Count > 0 ) {
          ConstraintGameObject = TryCreateConstraint( ray,
                                                      results[ 0 ].GameObject,
                                                      MouseButtonToDofTypes( m_mouseButtonState.ButtonDown ),
                                                      "PickHandlerConstraint" );
          if ( ConstraintGameObject != null ) {
            ConstraintGameObject.AddComponent<Rendering.PickHandlerRenderer>();
            Constraint.DrawGizmosEnable = false;
            m_distanceFromCamera = FindDistanceFromCamera( m_camera,
                                                           Constraint.AttachmentPair.ReferenceFrame.Position );
          }
        }

        m_mouseButtonState.Use( m_mouseButtonState.ButtonDown, buttonUp =>
        {
          if ( ConstraintGameObject != null ) {
            DestroyImmediate( ConstraintGameObject );
            ConstraintGameObject = null;
            m_distanceFromCamera = -1f;
          }
        } );
      }

      if ( ConstraintGameObject != null ) {
        Constraint.AttachmentPair.ConnectedFrame.Position = m_camera.ScreenToWorldPoint( new Vector3( Input.mousePosition.x,
                                                                                                      Input.mousePosition.y,
                                                                                                      m_distanceFromCamera ) );

        SetComplianceDamping( Constraint );

        ConstraintGameObject.GetComponent<Rendering.PickHandlerRenderer>().ThisMethodIsntAllowedToBeNamedUpdateByUnity( Constraint );
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

    class BroadPhaseResult
    {
      public GameObject GameObject = null;
      public float Distance = float.MaxValue;
    }

    private List<BroadPhaseResult> FindRayBoundingVolumeOverlaps( Ray worldRay )
    {
      List<BroadPhaseResult> result = new List<BroadPhaseResult>();

      // Testing shapes - only if we have a DebugRenderManager instance so that the
      // user only picks "visual" objects.
      if ( Rendering.DebugRenderManager.HasInstance ) {
        MeshFilter[] shapeFilters = Rendering.DebugRenderManager.Instance.GetComponentsInChildren<MeshFilter>();
        for ( int i = 0; i < shapeFilters.Length; ++i ) {
          BroadPhaseResult bpr = TestFilter( worldRay, shapeFilters[ i ] );
          if ( bpr != null && bpr.GameObject.GetComponent<OnSelectionProxy>() != null ) {
            bpr.GameObject = bpr.GameObject.GetComponent<OnSelectionProxy>().Target;
            result.Add( bpr );
          }
        }
      }

      // Testing mesh filters in rigid bodies.
      RigidBody[] bodies = FindObjectsOfType<RigidBody>();
      for ( int i = 0; i < bodies.Length; ++i ) {
        MeshFilter[] rbFilters = bodies[ i ].GetComponentsInChildren<MeshFilter>();
        for ( int j = 0; j < rbFilters.Length; ++j ) {
          BroadPhaseResult bpr = TestFilter( worldRay, rbFilters[ j ] );
          if ( bpr != null )
            result.Add( bpr );
        }
      }

      result.Sort( ( bfr1, bfr2 ) => { return bfr1.Distance < bfr2.Distance ? -1 : 1; } );

      return result;
    }

    private BroadPhaseResult TestFilter( Ray worldRay, MeshFilter filter )
    {
      // Filter mesh bounds are object oriented (local to vertices).
      // Transform world ray to filter space to be able to test against the OOBB.
      Ray localRay = new Ray( filter.transform.InverseTransformPoint( worldRay.origin ), filter.transform.InverseTransformVector( worldRay.direction ).normalized );

      float distanceLocal;
      if ( filter.sharedMesh.bounds.IntersectRay( localRay, out distanceLocal ) ) {
        // The test is in local space so we have to transform the intersection point
        // to world in order to get the wanted/expected distance.
        Vector3 worldHitPoint = filter.transform.TransformPoint( localRay.GetPoint( distanceLocal ) );
        return new BroadPhaseResult() { GameObject = filter.gameObject, Distance = ( worldHitPoint - worldRay.origin ).magnitude };
      }

      return null;
    }
  }
}
