﻿using AGXUnity.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity
{
  /// <summary>
  /// Base of controllers and object of ordinary elementary constraints.
  /// </summary>
  [Serializable]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#constraint" )]
  public class ElementaryConstraintNew : IPropertySynchronizable
  {
    /// <summary>
    /// Create instance given temporary native elementary constraint.
    /// </summary>
    /// <param name="tmpEc">Temporary elementary constraint.</param>
    /// <returns>New instance, as similar as possible, to the given native elementary constraint.</returns>
    public static ElementaryConstraintNew Create( GameObject gameObject, agx.ElementaryConstraint tmpEc )
    {
      if ( tmpEc == null )
        return null;

      ElementaryConstraintNew elementaryConstraint = null;

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
        elementaryConstraint = System.Activator.CreateInstance( Type.GetType( "AGXUnity." + controllerType.Name + "New" ) ) as ElementaryConstraintNew;
      }
      // This is an ordinary elementary constraint.
      else
        elementaryConstraint = new ElementaryConstraintNew();

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
    public ElementaryConstraintNew FromLegacy( GameObject gameObject )
    {
      ElementaryConstraintNew target = System.Activator.CreateInstance( GetType() ) as ElementaryConstraintNew;
      target.Construct( this );

      return target;
    }

    /// <summary>
    /// Name of the native instance in the constraint. This is the
    /// link to our native instance as long as we have access to
    /// the native constraint.
    /// </summary>
    [field: SerializeField]
    [HideInInspector]
    public string NativeName { get; private set; }

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
    public int NumRows => RowData.Length;

    /// <summary>
    /// Data (compliance, damping etc.) for each row in this elementary constraint.
    /// </summary>
    [field: SerializeField]
    public ElementaryConstraintRowDataNew[] RowData { get; private set; }

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

      return Initialize();
    }

    public void CopyFrom( ElementaryConstraintNew source )
    {
      Construct( source );
    }

    [Obsolete]
    public void MigrateInternalData(AGXUnity.Deprecated.ElementaryConstraint ec)
    {
      NativeName = ec.NativeName;
      List<ElementaryConstraintRowDataNew> newRDs = new List<ElementaryConstraintRowDataNew>();
      foreach ( var row in ec.RowData ) {
        var newRowData = new ElementaryConstraintRowDataNew(this, row.Row);
        newRowData.Compliance = row.Compliance;
        newRowData.Damping = row.Damping;
        newRowData.ForceRange = row.ForceRange;
        newRDs.Add( newRowData );
      }
      RowData = newRDs.ToArray();
    }

    protected virtual void Construct( agx.ElementaryConstraint tmpEc )
    {
      NativeName = tmpEc.getName();
      m_enable = tmpEc.getEnable();
      RowData = new ElementaryConstraintRowDataNew[ tmpEc.getNumRows() ];
      for ( uint i = 0; i < tmpEc.getNumRows(); ++i )
        RowData[ i ] = new ElementaryConstraintRowDataNew( this, Convert.ToInt32( i ), tmpEc );
    }

    protected virtual void Construct( ElementaryConstraintNew source )
    {
      NativeName = source.NativeName;
      m_enable = source.m_enable;
      RowData = new ElementaryConstraintRowDataNew[ source.NumRows ];
      for ( int i = 0; i < source.NumRows; ++i )
        RowData[ i ] = new ElementaryConstraintRowDataNew( this, source.RowData[ i ] );
    }

    protected bool Initialize()
    {
      if ( Native == null )
        return false;

      // Manually synchronizing data for native row coupling.
      foreach ( ElementaryConstraintRowDataNew data in RowData ) {
        data.ElementaryConstraint = this;
        Utils.PropertySynchronizer.Synchronize( data );
      }

      Utils.PropertySynchronizer.Synchronize( this );

      return true;
    }
  }
}
