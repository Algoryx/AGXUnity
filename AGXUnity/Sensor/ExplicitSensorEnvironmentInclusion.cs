using UnityEngine;

namespace AGXUnity.Sensor
{
  // TODO: Explicit inclusions seem to require one time step to pass before the become active.

  /// <summary>
  /// This component allow fine grained control over whether a specific gameobject (or gameobject hierarchy) is
  /// included in the sensor environment.
  /// </summary>
  [DisallowMultipleComponent]
  [AddComponentMenu( "AGXUnity/Sensors/Explicit Sensor Inclusion" )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#sensor-environment-explicit-control" )]
  public class ExplicitSensorEnvironmentInclusion : ScriptComponent
  {
    [Tooltip( "When enabled, the affected gameobjects will be force-included in the sensor environment. When disabled, they will be force-excluded" )]
    [field: SerializeField]
    public bool Include { get; set; } = true;

    [Tooltip( "When enabled, this component will apply its inclusion rules to all children, stopping if another component is present further down the hierarchy" )]
    [field: SerializeField]
    public bool PropagateToChildrenRecusively { get; set; } = true;

    /// <summary>
    /// Finds the closest instance of the component up in the hierarchy given a starting gameobject
    /// </summary>
    /// <param name="gameObject">The object at which point in the hierarchy the search starts.</param>
    /// <returns>The first instance of the component up in the hierarchy or null if there is no such component</returns>
    public static ExplicitSensorEnvironmentInclusion FindClosest( GameObject gameObject )
    {
      var current = gameObject;

      while ( current != null ) {
        if ( current.TryGetComponent<ExplicitSensorEnvironmentInclusion>( out var inclusion ) ) {
          if ( current == gameObject || inclusion.PropagateToChildrenRecusively )
            return inclusion;
        }
        current = current.transform?.parent?.gameObject;
      }
      return null;
    }
  }
}
