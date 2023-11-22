using AGXUnity.Utils;
using UnityEngine;

namespace AGXUnity.Collide
{
  /// <summary>
  /// Infinite plane object - probably not completely working.
  /// </summary>
  [AddComponentMenu( "AGXUnity/Shapes/Plane" )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#plane" )]
  public sealed class Plane : Shape
  {
    /// <summary>
    /// Returns native plane if created.
    /// </summary>
    public agxCollide.Plane Native { get { return NativeShape?.asPlane(); } }

    /// <summary>
    /// Debug rendering scale is one since size isn't a thing for planes.
    /// </summary>
    public override Vector3 GetScale()
    {
      return new Vector3( 1, 1, 1 );
    }

    /// <summary>
    /// Creates native plane object given current transform up vector.
    /// </summary>
    /// <returns></returns>
    protected override agxCollide.Geometry CreateNative()
    {
      return new agxCollide.Geometry( new agxCollide.Plane( agx.Vec3.Y_AXIS(), 0 ),
                                      GetNativeGeometryOffset() );
    }
  }
}
