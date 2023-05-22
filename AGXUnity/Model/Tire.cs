using System.Linq;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity.Model
{
  [DoNotGenerateCustomEditor]
  [AddComponentMenu( "" )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#tire" )]
  public class Tire : ScriptComponent
  {
    /// <summary>
    /// Estimates radius given shapes and rendering meshes in the game object.
    /// The maximum radius found is returned - 0 if nothing was found.
    /// </summary>
    /// <param name="gameObject">Game object to estimate radius of.</param>
    /// <param name="ignoreMeshFilterWhenShapeHasRadius">
    /// False to always include mesh filter bounds even when shapes has radius,
    /// true to exclude mesh filters when shapes have radius.
    /// </param>
    /// <returns>Radius > 0 if radius was found, otherwise 0.</returns>
    public static float FindRadius( GameObject gameObject, bool ignoreMeshFilterWhenShapeHasRadius = false )
    {
      if ( gameObject == null )
        return 0.0f;

      var shapes       = RigidBody.FindShapes( gameObject );
      var radiusShapes = FindRadius( shapes );
      if ( radiusShapes > 0.0f && ignoreMeshFilterWhenShapeHasRadius )
        return radiusShapes;

      // If the shapes mesh filters can represent the radius we're good,
      // but when the user selects [Visual] as parent we don't have any
      // information from the shapes since it's empty.
      var meshFilters = Collide.Shape.FindMeshFilters( shapes );
      if ( meshFilters.Length == 0 )
        meshFilters = gameObject.GetComponentsInChildren<MeshFilter>();
      var radiusMeshes = FindRadius( meshFilters,
                                     FindRotationAxisWorld( shapes,
                                                            meshFilters ) );
      // Unsure how reliable shape radius is and it seems that
      // the maximum of the two gives most accurate result. E.g.,
      // the tire has a primitive cylinder encapsulating the whole
      // wheel while the rim probably don't have one encapsulating
      // the rim.
      return Mathf.Max( radiusShapes, radiusMeshes );
    }

    /// <summary>
    /// Estimates radius given shapes and rendering meshes in the rigid
    /// body. The maximum radius found is returned - 0 if nothing was found.
    /// </summary>
    /// <param name="rb">Tire/rim rigid body.</param>
    /// <param name="ignoreMeshFilterWhenShapeHasRadius">
    /// False to always include mesh filter bounds even when shapes has radius,
    /// true to exclude mesh filters when shapes have radius.
    /// </param>
    /// <returns>Radius > 0 if radius was found, otherwise 0.</returns>
    public static float FindRadius( RigidBody rb, bool ignoreMeshFilterWhenShapeHasRadius = false )
    {
      if ( rb == null )
        return 0.0f;

      return FindRadius( rb.gameObject, ignoreMeshFilterWhenShapeHasRadius );
    }

    /// <summary>
    /// Searches for "Radius" property in shapes and returns the maximum and
    /// 0.0 if nothing was found.
    /// </summary>
    /// <param name="shapes">Array of shapes.</param>
    /// <returns>Maximum value of "Radius" property in <paramref name="shapes"/>.</returns>
    public static float FindRadius( Collide.Shape[] shapes )
    {
      float maxRadius = 0.0f;
      foreach ( var shape in shapes ) {
        var radiusProperty = GetRadiusProperty( shape.GetType() );
        if ( radiusProperty == null )
          continue;
        maxRadius = Mathf.Max( maxRadius, (float)radiusProperty.GetGetMethod().Invoke( shape, new object[] { } ) );
      }
      return maxRadius;
    }

    /// <summary>
    /// Finds radius of cylinder-like shapes, given mesh bounds and a rotation axis.
    /// </summary>
    /// <param name="filters">Mesh filters.</param>
    /// <param name="rotationAxisWorld">Rotation axis given in world frame.</param>
    /// <returns>Maximum value of extents orthogonal to the given rotation axis.</returns>
    public static float FindRadius( MeshFilter[] filters, Vector3 rotationAxisWorld )
    {
      float maxRadius = 0.0f;
      foreach ( var filter in filters ) {
        var localRotationAxis = filter.transform.InverseTransformDirection( rotationAxisWorld );
        var localExtents      = Vector3.Scale( filter.sharedMesh.bounds.extents, filter.transform.localScale );
        // We're interested in where the rotation axis isn't pointing,
        // i.e., minimum values since we're assuming cylinder-like shape.
        var rotationAxisMaxDirIndex = localRotationAxis.MaxIndex();
        maxRadius = Mathf.Max( maxRadius,
                               localExtents[ (rotationAxisMaxDirIndex + 1) % 3 ],
                               localExtents[ (rotationAxisMaxDirIndex + 2) % 3 ] );
      }
      return maxRadius;
    }

    public static Vector3 FindRotationAxisWorld( GameObject gameObject )
    {
      var shapes = RigidBody.FindShapes( gameObject );
      var meshFilters = Collide.Shape.FindMeshFilters( shapes );
      if ( meshFilters.Length == 0 )
        meshFilters = gameObject.GetComponentsInChildren<MeshFilter>();
      return FindRotationAxisWorld( shapes, meshFilters );
    }

    /// <summary>
    /// Estimates world rotation axis given a set of shapes and/or mesh filters.
    /// Vector3.zero is returned if the rotation axis wasn't possible to extract.
    /// </summary>
    /// <param name="shapes">Set of shapes.</param>
    /// <param name="meshFilters">Set of mesh filters.</param>
    /// <returns>World rotation axis if found - otherwise Vector3.zero.</returns>
    public static Vector3 FindRotationAxisWorld( Collide.Shape[] shapes,
                                                 MeshFilter[] meshFilters )
    {
      // Assuming any shape type with Radius property is defined
      // with its rotation axis along y.
      foreach ( var shape in shapes )
        if ( GetRadiusProperty( shape.GetType() ) != null )
          return shape.transform.TransformDirection( Vector3.up );

      MeshFilter bestFilter    = null;
      float maxExtent          = 0.0f;
      foreach ( var filter in meshFilters ) {
        var boundsExtents = filter.sharedMesh.bounds.extents;
        // 1. Max and middle value should be approximately the same.
        // 2. Min value is "much" less than the middle value.
        if ( boundsExtents.MaxValue() < 0.95f * boundsExtents.MiddleValue() &&
             boundsExtents.MinValue() < 0.85f * boundsExtents.MiddleValue() ) {
          if ( boundsExtents.MaxValue() > maxExtent ) {
            maxExtent = boundsExtents.MaxValue();
            bestFilter = filter;
          }
        }
      }

      var result = Vector3.zero;
      if ( bestFilter != null ) {
        var localAxis = Vector3.zero;
        localAxis[ bestFilter.sharedMesh.bounds.extents.MinIndex() ] = 1.0f;
        result = bestFilter.transform.TransformDirection( localAxis );
      }

      return result;
    }

    /// <summary>
    /// Finds native tire transform defining the rotation axis.
    /// </summary>
    /// <param name="rb">Tire rigid body.</param>
    /// <returns>Native transform defining the rotation axis.</returns>
    public static agx.AffineMatrix4x4 FindNativeTransform( RigidBody rb )
    {
      if ( rb == null || rb.Native == null ) {
        Debug.LogError( "Tire.FindNativeTransform failed: Native rigid body not initialized.", rb );
        return agx.AffineMatrix4x4.identity();
      }

      var worldRotationAxis = FindRotationAxisWorld( rb.Shapes,
                                                     Collide.Shape.FindMeshFilters( rb.Shapes ) );
      var rotationAxisTransform = agx.AffineMatrix4x4.identity();
      if ( worldRotationAxis == Vector3.zero ) {
        Debug.LogWarning( "TwoBodyTire failed to identify rotation axis - assuming Tire local z axis." );
        rotationAxisTransform.setRotate( agx.Quat.rotate( agx.Vec3.Z_AXIS(), agx.Vec3.Y_AXIS() ) );
      }
      else {
        rotationAxisTransform.setRotate( agx.Quat.rotate( rb.Native.getFrame().transformVectorToLocal( worldRotationAxis.ToHandedVec3() ),
                                                          agx.Vec3.Y_AXIS() ) );
      }
      return rotationAxisTransform;
    }

    /// <summary>
    /// Finds property named "Radius" if it exists in <paramref name="type"/>.
    /// </summary>
    /// <param name="type">Type to check for property "Radius".</param>
    /// <returns>Property info of "Radius" if exist - otherwise null.</returns>
    protected static System.Reflection.PropertyInfo GetRadiusProperty( System.Type type )
    {
      return type.GetProperty( "Radius", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public );
    }
  }
}
