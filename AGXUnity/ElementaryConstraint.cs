using System;
using UnityEngine;

namespace AGXUnity
{
  /// <summary>
  /// Base of controllers and object of ordinary elementary constraints.
  /// </summary>
  [HideInInspector]
  [AddComponentMenu( "" )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#constraint" )]
  public class ElementaryConstraint : ScriptComponent
  {
    /// <summary>
    /// Create instance given temporary native elementary constraint.
    /// </summary>
    /// <param name="tmpEc">Temporary elementary constraint.</param>
    /// <returns>New instance, as similar as possible, to the given native elementary constraint.</returns>
    public static ElementaryConstraint Create( GameObject gameObject, agx.ElementaryConstraint tmpEc )
    {
      if ( tmpEc == null )
        return null;

      ElementaryConstraint elementaryConstraint = null;

      // It's possible to know the type of controllers. We're basically not
      // interested in knowing the type of the ordinary ones.
      Type controllerType = null;
      if ( agx.RangeController.safeCast( tmpEc ) != null )
        controllerType = agx.RangeController.safeCast( tmpEc ).GetType();
      else if ( agx.TargetSpeedController.safeCast( tmpEc ) != null )
        controllerType = agx.TargetSpeedController.safeCast( tmpEc ).GetType();
      else if ( agx.LockController.safeCast( tmpEc ) != null )
        controllerType = agx.LockController.safeCast( tmpEc ).GetType();
      else if ( agx.ScrewController.safeCast( tmpEc ) != null )
        controllerType = agx.ScrewController.safeCast( tmpEc ).GetType();
      else if ( agx.ElectricMotorController.safeCast( tmpEc ) != null )
        controllerType = agx.ElectricMotorController.safeCast( tmpEc ).GetType();
      else if ( agx.FrictionController.safeCast( tmpEc ) != null )
        controllerType = agx.FrictionController.safeCast( tmpEc ).GetType();

      // This is a controller, instantiate the controller.
      if ( controllerType != null ) {
        elementaryConstraint = gameObject.AddComponent( Type.GetType( "AGXUnity." + controllerType.Name ) ) as ElementaryConstraint;
      }
      // This is an ordinary elementary constraint.
      else
        elementaryConstraint = gameObject.AddComponent<ElementaryConstraint>();

      // Copies data from the native instance.
      elementaryConstraint.Construct( tmpEc );

      return elementaryConstraint;
    }

    /// <summary>
    /// Takes this legacy elementary constraint, creates a new instance (added to gameObject) and
    /// copies all values/objects to the new instance.
    /// </summary>
    /// <param name="gameObject">Game object to add the new version of the elementary constraint to.</param>
    /// <returns>New added elementary constraint instance.</returns>
    public ElementaryConstraint FromLegacy( GameObject gameObject )
    {
      ElementaryConstraint target = gameObject.AddComponent( GetType() ) as ElementaryConstraint;
      target.Construct( this );

      return target;
    }

    /// <summary>
    /// Name of the native instance in the constraint. This is the
    /// link to our native instance as long as we have access to
    /// the native constraint.
    /// </summary>
    [SerializeField]
    private string m_nativeName = string.Empty;

    /// <summary>
    /// Name of the native instance in the constraint.
    /// </summary>
    [HideInInspector]
    public string NativeName { get { return m_nativeName; } }

    /// <summary>
    /// Enable flag. Paired with property Enable.
    /// </summary>
    [SerializeField]
    private bool m_enable = true;

    /// <summary>
    /// Enable flag.
    /// </summary>
    [InspectorPriority( 10 )]
    public bool Enable
    {
      get { return m_enable; }
      set
      {
        m_enable = value;
        if ( Native != null )
          Native.setEnable( m_enable );
      }
    }

    /// <summary>
    /// Number of rows in this elementary constraint.
    /// </summary>
    [HideInInspector]
    public int NumRows { get { return m_rowData.Length; } }

    /// <summary>
    /// Data (compliance, damping etc.) for each row in this elementary constraint.
    /// Paired with property RowData.
    /// </summary>
    [SerializeField]
    private ElementaryConstraintRowData[] m_rowData = null;

    /// <summary>
    /// Data (compliance, damping etc.) for each row in this elementary constraint.
    /// </summary>
    public ElementaryConstraintRowData[] RowData { get { return m_rowData; } }

    /// <summary>
    /// Native instance of this elementary constraint. Only set when the
    /// constraint is initialized and is simulating.
    /// </summary>
    public agx.ElementaryConstraint Native { get; private set; }

    /// <summary>
    /// Callback from Constraint when it's being initialized.
    /// </summary>
    /// <param name="constraint">Constraint object this elementary constraint is part of.</param>
    /// <returns>True if successful.</returns>
    public virtual bool OnConstraintInitialize( Constraint constraint )
    {
      Native = constraint.Native.getElementaryConstraintGivenName( NativeName ) ??
               constraint.Native.getSecondaryConstraintGivenName( NativeName );

      return GetInitialized<ElementaryConstraint>() != null;
    }

    public void CopyFrom( ElementaryConstraint source )
    {
      Construct( source );
    }

    protected virtual void Construct( agx.ElementaryConstraint tmpEc )
    {
      m_nativeName = tmpEc.getName();
      m_enable = tmpEc.getEnable();
      m_rowData = new ElementaryConstraintRowData[ tmpEc.getNumRows() ];
      for ( uint i = 0; i < tmpEc.getNumRows(); ++i )
        m_rowData[ i ] = new ElementaryConstraintRowData( this, Convert.ToInt32( i ), tmpEc );
    }

    protected virtual void Construct( ElementaryConstraint source )
    {
      m_nativeName = source.m_nativeName;
      m_enable = source.m_enable;
      m_rowData = new ElementaryConstraintRowData[ source.NumRows ];
      for ( int i = 0; i < source.NumRows; ++i )
        m_rowData[ i ] = new ElementaryConstraintRowData( this, source.m_rowData[ i ] );
    }

    protected override bool Initialize()
    {
      if ( Native == null )
        return false;

      // Manually synchronizing data for native row coupling.
      foreach ( ElementaryConstraintRowData data in m_rowData )
        Utils.PropertySynchronizer.Synchronize( data );

      Utils.PropertySynchronizer.Synchronize( this );

      return true;
    }

    protected override void OnDestroy()
    {
      Native = null;

      base.OnDestroy();
    }

    protected virtual void Reset()
    {
      hideFlags |= HideFlags.HideInInspector;
    }
  }
}
