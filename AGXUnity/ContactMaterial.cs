using System;
using UnityEngine;

namespace AGXUnity
{
  /// <summary>
  /// Contact material object.
  /// </summary>
  public class ContactMaterial : ScriptAsset
  {
    public enum ContactReductionType
    {
      /// <summary>
      /// Contact reduction disabled.
      /// </summary>
      None,
      /// <summary>
      /// Geometry <-> geometry contact reduction.
      /// </summary>
      Geometry,
      /// <summary>
      /// Rigid body <-> rigid body, rigid body <-> geometry or
      /// geometry <-> geometry contact reduction.
      /// </summary>
      All
    }

    public enum ContactReductionLevelType
    {
      /// <summary>
      /// Bin resolution = 3.
      /// </summary>
      Minimal,
      /// <summary>
      /// Bin resolution = 2.
      /// </summary>
      Moderate,
      /// <summary>
      /// Bin resolution = 1.
      /// </summary>
      Aggressive
    }

    /// <summary>
    /// Native instance.
    /// </summary>
    private agx.ContactMaterial m_contactMaterial = null;

    /// <summary>
    /// Get the native instance, if created.
    /// </summary>
    public agx.ContactMaterial Native { get { return m_contactMaterial; } }

    /// <summary>
    /// First material in this contact material, paired with property Material1.
    /// </summary>
    [SerializeField]
    private ShapeMaterial m_material1 = null;

    /// <summary>
    /// Get or set first shape material.
    /// Note that it's not possible to change shape material instance after
    /// this contact material has been initialized.
    /// </summary>
    [AllowRecursiveEditing]
    public ShapeMaterial Material1
    {
      get { return m_material1; }
      set
      {
        m_material1 = value;
      }
    }

    /// <summary>
    /// Second material in this contact material, paired with property Material2.
    /// </summary>
    [SerializeField]
    private ShapeMaterial m_material2 = null;

    /// <summary>
    /// Get or set second shape material.
    /// Note that it's not possible to change shape material instance after
    /// this contact material has been initialized.
    /// </summary>
    [AllowRecursiveEditing]
    public ShapeMaterial Material2
    {
      get { return m_material2; }
      set
      {
        m_material2 = value;
      }
    }

    /// <summary>
    /// Friction model coupled to this contact material, paired with property FrictionModel.
    /// </summary>
    [SerializeField]
    private FrictionModel m_frictionModel = null;

    /// <summary>
    /// Get or set friction model coupled to this contact material.
    /// </summary>
    [AllowRecursiveEditing]
    public FrictionModel FrictionModel
    {
      get { return m_frictionModel; }
      set
      {
        m_frictionModel = value;
        if ( Native != null && m_frictionModel != null && m_frictionModel.Native != null )
          Native.setFrictionModel( m_frictionModel.Native );
      }
    }

    /// <summary>
    /// Young's modulus of this contact material, paired with property YoungsModulus.
    /// </summary>
    [SerializeField]
    private float m_youngsModulus = 4.0E8f;

    /// <summary>
    /// Get or set Young's modulus of this contact material.
    /// </summary>
    [ClampAboveZeroInInspector]
    public float YoungsModulus
    {
      get { return m_youngsModulus; }
      set
      {
        m_youngsModulus = value;
        if ( m_contactMaterial != null )
          m_contactMaterial.setYoungsModulus( m_youngsModulus );
      }
    }

    /// <summary>
    /// Surface viscosity of this contact material, paired with property SurfaceViscosity.
    /// </summary>
    [SerializeField]
    private Vector2 m_surfaceViscosity = new Vector2( 5.0E-9f, 5.0E-9f );

    /// <summary>
    /// Get or set surface viscosity of this contact material.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public Vector2 SurfaceViscosity
    {
      get { return m_surfaceViscosity; }
      set
      {
        m_surfaceViscosity = value;
        if ( Native != null ) {
          Native.setSurfaceViscosity( m_surfaceViscosity.x, agx.ContactMaterial.FrictionDirection.PRIMARY_DIRECTION );
          Native.setSurfaceViscosity( m_surfaceViscosity.y, agx.ContactMaterial.FrictionDirection.SECONDARY_DIRECTION );
        }
      }
    }

    /// <summary>
    /// Friction coefficients of this contact material, paired with property FrictionCoefficients.
    /// </summary>
    [SerializeField]
    private Vector2 m_frictionCoefficients = new Vector2( 0.41667f, 0.41667f );

    /// <summary>
    /// Get or set friction coefficients of this contact material.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public Vector2 FrictionCoefficients
    {
      get { return m_frictionCoefficients; }
      set
      {
        m_frictionCoefficients = value;
        if ( Native != null ) {
          Native.setFrictionCoefficient( m_frictionCoefficients.x, agx.ContactMaterial.FrictionDirection.PRIMARY_DIRECTION );
          Native.setFrictionCoefficient( m_frictionCoefficients.y, agx.ContactMaterial.FrictionDirection.SECONDARY_DIRECTION );
        }
      }
    }

    /// <summary>
    /// Restitution of this contact material, paired with property Restitution.
    /// </summary>
    [SerializeField]
    private float m_restitution = 0.5f;

    /// <summary>
    /// Get or set restitution of this contact material.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float Restitution
    {
      get { return m_restitution; }
      set
      {
        m_restitution = value;
        if ( Native != null )
          Native.setRestitution( m_restitution );
      }
    }

    /// <summary>
    /// Damping of the contact constraint, paired with property Damping.
    /// </summary>
    [SerializeField]
    private float m_damping = 4.5f / 60.0f;

    /// <summary>
    /// Damping of the contact constraint. Default: 4.5 / 60 = 0.075.
    /// </summary>
    public float Damping
    {
      get { return m_damping; }
      set
      {
        m_damping = value;
        if ( Native != null )
          Native.setDamping( m_damping );
      }
    }

    /// <summary>
    /// Adhesive force, paired with property AdhesiveForce.
    /// </summary>
    [SerializeField]
    private float m_adhesiveForce = 0.0f;

    /// <summary>
    /// Adhesive force of the contacts with this contact material.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float AdhesiveForce
    {
      get { return m_adhesiveForce; }
      set
      {
        m_adhesiveForce = value;
        if ( Native != null )
          Native.setAdhesion( m_adhesiveForce, AdhesiveOverlap );
      }
    }

    /// <summary>
    /// Adhesive overlap, paired with property AdhesiveOverlap.
    /// </summary>
    [SerializeField]
    private float m_adhesiveOverlap = 0.0f;

    /// <summary>
    /// Allowed overlap >= 0 from surface for resting contact. At this overlap,
    /// no force is applied. At lower overlap, the adhesion force will work,
    /// at higher overlap, the (usual) contact force.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public float AdhesiveOverlap
    {
      get { return m_adhesiveOverlap; }
      set
      {
        m_adhesiveOverlap = value;
        if ( Native != null )
          Native.setAdhesion( AdhesiveForce, m_adhesiveOverlap );
      }
    }

    /// <summary>
    /// Calculate contact area, paired with property UseContactArea.
    /// </summary>
    [SerializeField]
    private bool m_useContactArea = false;

    /// <summary>
    /// Enable/disable contact area approach of contacts using this contact material.
    /// </summary>
    public bool UseContactArea
    {
      get { return m_useContactArea; }
      set
      {
        m_useContactArea = value;
        if ( Native != null )
          Native.setUseContactAreaApproach( m_useContactArea );
      }
    }

    /// <summary>
    /// Contact reduction mode, paired with property ContactReductionMode.
    /// </summary>
    [SerializeField]
    private ContactReductionType m_contactReductionMode = ContactReductionType.None;

    /// <summary>
    /// Contact reduction mode, default None (disabled).
    /// </summary>
    public ContactReductionType ContactReductionMode
    {
      get { return m_contactReductionMode; }
      set
      {
        m_contactReductionMode = value;
        if ( Native != null )
          Native.setContactReductionMode( (agx.ContactMaterial.ContactReductionMode)m_contactReductionMode );
      }
    }

    /// <summary>
    /// Contact reduction level if contact reduction is enabled, paired with property ContactReductionLevel.
    /// </summary>
    [SerializeField]
    private ContactReductionLevelType m_contactReductionLevel = ContactReductionLevelType.Moderate;

    /// <summary>
    /// Contact reduction level when contact reduction is enabled (ContactReductionMode != None).
    /// </summary>
    public ContactReductionLevelType ContactReductionLevel
    {
      get { return m_contactReductionLevel; }
      set
      {
        m_contactReductionLevel = value;
        if ( Native != null ) {
          var binResolution = m_contactReductionLevel == ContactReductionLevelType.Minimal  ? 3 :
                              m_contactReductionLevel == ContactReductionLevelType.Moderate ? 2 :
                                                                                              1;
          Native.setContactReductionBinResolution( Convert.ToByte( binResolution ) );
        }
      }
    }

    /// <summary>
    /// Wire friction coefficients of this contact material, used by the contact nodes on a wire.
    /// The primary (x) friction coefficient is used along the wire and the secondary (y) is
    /// along the contact edge on the object the wire interacts with.
    /// </summary>
    [SerializeField]
    private Vector2 m_wireFrictionCoefficients = new Vector2( 0.41667f, 0.41667f );

    /// <summary>
    /// Wire friction coefficients of this contact material, used by the contact nodes on a wire.
    /// The primary (x) friction coefficient is used along the wire and the secondary (y) is
    /// along the contact edge on the object the wire interacts with.
    /// </summary>
    [ClampAboveZeroInInspector( true )]
    public Vector2 WireFrictionCoefficients
    {
      get { return m_wireFrictionCoefficients; }
      set
      {
        m_wireFrictionCoefficients = value;
        if ( Native != null ) {
          Native.setWireFrictionCoefficient( m_wireFrictionCoefficients.x, agx.ContactMaterial.FrictionDirection.PRIMARY_DIRECTION );
          Native.setWireFrictionCoefficient( m_wireFrictionCoefficients.y, agx.ContactMaterial.FrictionDirection.SECONDARY_DIRECTION );
        }
      }
    }

    public ContactMaterial RestoreLocalDataFrom( agx.ContactMaterial contactMaterial )
    {
      YoungsModulus         = Convert.ToSingle( contactMaterial.getYoungsModulus() );
      SurfaceViscosity      = new Vector2( Convert.ToSingle( contactMaterial.getSurfaceViscosity( agx.ContactMaterial.FrictionDirection.PRIMARY_DIRECTION ) ),
                                           Convert.ToSingle( contactMaterial.getSurfaceViscosity( agx.ContactMaterial.FrictionDirection.SECONDARY_DIRECTION ) ) );
      FrictionCoefficients  = new Vector2( Convert.ToSingle( contactMaterial.getFrictionCoefficient( agx.ContactMaterial.FrictionDirection.PRIMARY_DIRECTION ) ),
                                           Convert.ToSingle( contactMaterial.getFrictionCoefficient( agx.ContactMaterial.FrictionDirection.SECONDARY_DIRECTION ) ) );
      Restitution           = Convert.ToSingle( contactMaterial.getRestitution() );
      Damping               = Convert.ToSingle( contactMaterial.getDamping() );
      AdhesiveForce         = Convert.ToSingle( contactMaterial.getAdhesion() );
      AdhesiveOverlap       = Convert.ToSingle( contactMaterial.getAdhesiveOverlap() );
      UseContactArea        = contactMaterial.getUseContactAreaApproach();
      ContactReductionMode  = (ContactReductionType)contactMaterial.getContactReductionMode();
      var binResolution     = Convert.ToInt32( contactMaterial.getContactReductionBinResolution() );
      ContactReductionLevel = binResolution == 3 ? ContactReductionLevelType.Minimal :
                              binResolution == 2 ? ContactReductionLevelType.Moderate :
                                                   ContactReductionLevelType.Aggressive;
      WireFrictionCoefficients = new Vector2( Convert.ToSingle( contactMaterial.getWireFrictionCoefficient( agx.ContactMaterial.FrictionDirection.PRIMARY_DIRECTION ) ),
                                              Convert.ToSingle( contactMaterial.getWireFrictionCoefficient( agx.ContactMaterial.FrictionDirection.SECONDARY_DIRECTION ) ) );

      return this;
    }

    public void InitializeOrientedFriction( bool isOriented,
                                            GameObject referenceObject,
                                            FrictionModel.PrimaryDirection primaryDirection )
    {
      if ( !isOriented || referenceObject == null || FrictionModel == null )
        return;

      if ( !Application.isPlaying ) {
        Debug.LogError( "Oriented friction: Invalid to initialize oriented friction in edit mode.", this );
        return;
      }

      if ( GetInitialized<ContactMaterial>() == null )
        return;

      if ( FrictionModel.GetInitialized<FrictionModel>() == null )
        return;

      var rb       = referenceObject.GetComponent<RigidBody>();
      var shape    = rb == null ?
                       referenceObject.GetComponent<Collide.Shape>() :
                       null;
      var observer = rb == null && shape == null ?
                       referenceObject.GetComponent<ObserverFrame>() :
                       null;

      agx.Frame referenceFrame = null;
      if ( rb != null && rb.GetInitialized<RigidBody>() != null )
        referenceFrame = rb.Native.getFrame();
      else if ( shape != null && shape.GetInitialized<Collide.Shape>() != null )
        referenceFrame = shape.NativeGeometry.getFrame();
      else if ( observer != null && observer.GetInitialized<ObserverFrame>() != null )
        referenceFrame = observer.Native.getFrame();

      if ( referenceFrame == null ) {
        Debug.LogWarning( $"Oriented friction: Unable to find reference frame from {referenceObject.name}.", referenceObject );
        return;
      }

      if ( rb != null )
        FrictionModel.InitializeOriented( rb, primaryDirection );
      else
        FrictionModel.InitializeOriented( shape, primaryDirection );
    }

    protected override void Construct()
    {
    }

    protected override bool Initialize()
    {
      if ( Material1 == null || Material2 == null ) {
        Debug.LogWarning( name + ": Trying to create contact material with at least one unreferenced ShapeMaterial.", this );
        return false;
      }

      agx.Material m1 = Material1.GetInitialized<ShapeMaterial>().Native;
      agx.Material m2 = Material2.GetInitialized<ShapeMaterial>().Native;
      agx.ContactMaterial old = GetSimulation().getMaterialManager().getContactMaterial( m1, m2 );
      if ( old != null ) {
        Debug.LogWarning( name + ": Material manager already contains a contact material with this material pair. Ignoring this contact material.", this );
        return false;
      }

      m_contactMaterial = GetSimulation().getMaterialManager().getOrCreateContactMaterial( m1, m2 );

      if ( FrictionModel != null ) {
        m_contactMaterial.setFrictionModel( FrictionModel.GetInitialized<FrictionModel>().Native );
        // When the user changes friction model type (enum = BoxFriction, ScaleBoxFriction etc.)
        // the friction model object will create a new native instance. We'll receive callbacks
        // when this happens so we can assign it to our native contact material.
        FrictionModel.OnNativeInstanceChanged += OnFrictionModelNativeInstanceChanged;
      }

      return true;
    }

    public override void Destroy()
    {
      if ( Simulation.HasInstance )
        GetSimulation().getMaterialManager().remove( m_contactMaterial );
      m_contactMaterial = null;
    }

    /// <summary>
    /// Callback from AGXUnity.FrictionModel when the friction model type has been changed.
    /// </summary>
    /// <param name="frictionModel"></param>
    private void OnFrictionModelNativeInstanceChanged( agx.FrictionModel frictionModel )
    {
      if ( Native != null )
        Native.setFrictionModel( frictionModel );
    }
  }
}
