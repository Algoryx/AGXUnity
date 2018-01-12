using UnityEngine;

namespace AGXUnity
{
  /// <summary>
  /// Base class of controllers (such as motor, lock etc.).
  /// </summary>
  [HideInInspector]
  public class ElementaryConstraintController : ElementaryConstraint
  {
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
