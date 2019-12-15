using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity
{
  /// <summary>
  /// Friction model object with friction model type and solve type.
  /// </summary>
  public class FrictionModel : ScriptAsset
  {
    public enum ESolveType
    {
      Direct = 1,
      Iterative = 2,
      Split = 3,
      DirectAndIterative = 4
    }

    public enum EType
    {
      IterativeProjectedFriction = 0,
      ScaleBoxFriction,
      BoxFriction
    }

    public enum PrimaryDirection
    {
      X,
      Y,
      Z
    }

    /// <summary>
    /// Converts ESolveType to agx.FrictionModel.SolveType.
    /// </summary>
    /// <param name="solveType">Input ESolveType.</param>
    /// <returns>Output agx.FrictionModel.SolveType.</returns>
    public static agx.FrictionModel.SolveType Convert( ESolveType solveType )
    {
      if ( solveType == ESolveType.Direct )
        return agx.FrictionModel.SolveType.DIRECT;
      else if ( solveType == ESolveType.Iterative )
        return agx.FrictionModel.SolveType.ITERATIVE;
      else if ( solveType == ESolveType.Split )
        return agx.FrictionModel.SolveType.SPLIT;
      else if ( solveType == ESolveType.DirectAndIterative )
        return agx.FrictionModel.SolveType.DIRECT_AND_ITERATIVE;

      return agx.FrictionModel.SolveType.NOT_DEFINED;
    }

    /// <summary>
    /// Converts native solve type to ESolveType.
    /// </summary>
    /// <param name="solveType">Native solve type.</param>
    /// <returns>ESolveType given native solve type.</returns>
    public static ESolveType Convert( agx.FrictionModel.SolveType solveType )
    {
      if ( solveType == agx.FrictionModel.SolveType.DIRECT )
        return ESolveType.Direct;
      else if ( solveType == agx.FrictionModel.SolveType.ITERATIVE )
        return ESolveType.Iterative;
      else if ( solveType == agx.FrictionModel.SolveType.DIRECT_AND_ITERATIVE )
        return ESolveType.DirectAndIterative;

      return ESolveType.Split;
    }

    public static Vector3 Convert( PrimaryDirection primaryDirection )
    {
      return primaryDirection == PrimaryDirection.X ?
               Vector3.right :
             primaryDirection == PrimaryDirection.Y ?
               Vector3.up :
               Vector3.forward;
    }

    /// <summary>
    /// Finds friction model type given native instance.
    /// </summary>
    /// <param name="native">Native friction model.</param>
    /// <returns>Native friction model type.</returns>
    public static EType FindType( agx.FrictionModel native )
    {
      if ( native == null || native.asIterativeProjectedConeFriction() != null )
        return EType.IterativeProjectedFriction;
      else if ( native.asBoxFrictionModel() != null )
        return EType.BoxFriction;

      return EType.ScaleBoxFriction;
    }

    /// <summary>
    /// Get native instance, if created.
    /// </summary>
    public agx.FrictionModel Native { get; private set; } = null;

    /// <summary>
    /// Solve type, paired with property SolveType.
    /// </summary>
    [SerializeField]
    private ESolveType m_solveType = ESolveType.Split;

    /// <summary>
    /// Get or set solve type of this friction model.
    /// </summary>
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
    /// Delegate when friction model type is changed and a new native
    /// instance is created.
    /// </summary>
    /// <param name="newFrictionModel">New native instance.</param>
    public delegate void OnNativeInstanceChangedDelegate( agx.FrictionModel newFrictionModel );

    /// <summary>
    /// On native instance changed event.
    /// </summary>
    public event OnNativeInstanceChangedDelegate OnNativeInstanceChanged = delegate { };

    /// <summary>
    /// Friction model type, paired with property Type.
    /// </summary>
    [SerializeField]
    private EType m_type = EType.IterativeProjectedFriction;

    /// <summary>
    /// Get or set friction model type.
    /// </summary>
    public EType Type
    {
      get { return m_type; }
      set
      {
        if ( m_type == value )
          return;

        m_type = value;

        if ( Native == null )
          return;

        Native = CreateNative( Type, SolveType );

        OnNativeInstanceChanged( Native );
      }
    }

    /// <summary>
    /// Create native friction model given solve type and friction model type.
    /// </summary>
    /// <param name="type">Friction model type.</param>
    /// <param name="solveType">Solve type.</param>
    /// <returns>New native instance.</returns>
    public agx.FrictionModel CreateNative( EType type,
                                                  ESolveType solveType )
    {
      agx.Frame referenceFrame = null;
      if ( m_orientedFrictionReferenceObject != null ) {
        referenceFrame = m_orientedFrictionReferenceObject.GetComponent<RigidBody>() != null ?
                           m_orientedFrictionReferenceObject.GetComponent<RigidBody>().Native.getFrame() :
                         m_orientedFrictionReferenceObject.GetComponent<Collide.Shape>() != null ?
                           m_orientedFrictionReferenceObject.GetComponent<Collide.Shape>().NativeGeometry.getFrame() :
                           null;
      }

      agx.FrictionModel frictionModel = null;
      if ( type == EType.IterativeProjectedFriction )
        frictionModel = referenceFrame != null ?
                          new agx.OrientedIterativeProjectedConeFrictionModel( referenceFrame,
                                                                               Convert( m_orientedFrctionPrimaryDirection ).ToHandedVec3(),
                                                                               Convert( solveType ) ) :
                          new agx.IterativeProjectedConeFriction( Convert( solveType ) );
      else if ( type == EType.ScaleBoxFriction )
        frictionModel = referenceFrame != null ?
                          new agx.OrientedScaleBoxFrictionModel( referenceFrame,
                                                                 Convert( m_orientedFrctionPrimaryDirection ).ToHandedVec3(),
                                                                 Convert( solveType ) ) :
                          new agx.ScaleBoxFrictionModel( Convert( solveType ) );
      else if ( type == EType.BoxFriction )
        frictionModel = referenceFrame != null ?
                          new agx.OrientedBoxFrictionModel( referenceFrame,
                                                            Convert( m_orientedFrctionPrimaryDirection ).ToHandedVec3(),
                                                            Convert( solveType ) ) :
                          new agx.BoxFrictionModel( Convert( solveType ) );

      return frictionModel;
    }

    public FrictionModel RestoreLocalDataFrom( agx.FrictionModel native )
    {
      SolveType = Convert( native.getSolveType() );
      Type      = FindType( native );

      return this;
    }

    public void InitializeOriented( ScriptComponent referenceComponent, PrimaryDirection primaryDirection )
    {
      m_orientedFrictionReferenceObject = referenceComponent;
      m_orientedFrctionPrimaryDirection = primaryDirection;
      Native = CreateNative( Type, SolveType );
    }

    protected override void Construct()
    {
      m_type      = EType.IterativeProjectedFriction;
      m_solveType = ESolveType.Split;
    }

    protected override bool Initialize()
    {
      if ( Native != null )
        return true;

      Native = CreateNative( Type, SolveType );

      return true;
    }

    public override void Destroy()
    {
      Native = null;
    }

    private ScriptComponent m_orientedFrictionReferenceObject = null;
    private PrimaryDirection m_orientedFrctionPrimaryDirection = PrimaryDirection.X;
  }
}
