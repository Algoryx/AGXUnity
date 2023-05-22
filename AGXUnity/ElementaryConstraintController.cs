using UnityEngine;

namespace AGXUnity
{
  /// <summary>
  /// Base class of controllers (such as motor, lock etc.).
  /// </summary>
  [AddComponentMenu( "" )]
  [HideInInspector]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#controllers" )]
  public class ElementaryConstraintController : ElementaryConstraint
  {
    /// <summary>
    /// Get/set the compliance of this controller.
    /// </summary>
    [HideInInspector]
    public float Compliance
    {
      get { return RowData[ 0 ].Compliance; }
      set { RowData[ 0 ].Compliance = value; }
    }

    /// <summary>
    /// Get/set the damping of this controller (ignored for nonholonomic controllers).
    /// </summary>
    [HideInInspector]
    public float Damping
    {
      get { return RowData[ 0 ].Damping; }
      set { RowData[ 0 ].Damping = value; }
    }

    /// <summary>
    /// Get/set force range of this controller.
    /// </summary>
    [HideInInspector]
    public RangeReal ForceRange
    {
      get { return RowData[ 0 ].ForceRange; }
      set { RowData[ 0 ].ForceRange = value; }
    }

    public T As<T>( Constraint.ControllerType controllerType ) where T : ElementaryConstraintController
    {
      bool typeMatch = GetType() == typeof( T );
      return typeMatch && IsControllerTypeMatch( controllerType ) ?
               this as T :
               null;
    }

    public Constraint.ControllerType GetControllerType()
    {
      return IsControllerTypeMatch( Constraint.ControllerType.Translational ) ?
               Constraint.ControllerType.Translational :
               Constraint.ControllerType.Rotational;
    }

    private bool IsControllerTypeMatch( Constraint.ControllerType controllerType )
    {
      return controllerType == Constraint.ControllerType.Primary ||
             ( controllerType == Constraint.ControllerType.Translational && NativeName.EndsWith( "T" ) ) ||
             ( controllerType == Constraint.ControllerType.Rotational && NativeName.EndsWith( "R" ) );
    }
  }
}
