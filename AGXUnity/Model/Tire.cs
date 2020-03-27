using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity.Model
{
  [DoNotGenerateCustomEditor]
  [AddComponentMenu( "" )]
  public class Tire : ScriptComponent
  {
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
      var radiusShapes = FindRadius( rb.GetComponentsInChildren<Collide.Shape>() );
      if ( radiusShapes > 0.0f && ignoreMeshFilterWhenShapeHasRadius )
        return radiusShapes;

      var radiusMeshes = FindRadius( rb.GetComponentsInChildren<MeshFilter>(), FindRotationAxisWorld( rb ) );
      // Unsure how reliable shape radius is and it seems that
      // the maximum of the two gives most accurate result. E.g.,
      // the tire has a primitive cylinder encapsulating the whole
      // wheel while the rim probably don't have one encapsulating
      // the rim.
      return Mathf.Max( radiusShapes, radiusMeshes );
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
        var radiusProperty = shape.GetType().GetProperty( "Radius", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public );
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

    /// <summary>
    /// Estimates rotation axis of "cylinder like" rigid body with
    /// distinct mesh bounds (two extends approximately equal and
    /// the last is smaller than the ones that are approximately equal)
    /// </summary>
    /// <param name="tireRigidBody">Rigid body.</param>
    /// <returns>Rotation axis in world coordinate frame - Vector3.zero if failed.</returns>
    public static Vector3 FindRotationAxisWorld( RigidBody tireRigidBody )
    {
      if ( tireRigidBody == null )
        return Vector3.zero;

      var result = Vector3.zero;
      foreach ( var shape in tireRigidBody.GetComponentsInChildren<Collide.Shape>() ) {
        if ( shape is Collide.Cylinder || shape is Collide.Capsule )
          result = shape.transform.TransformDirection( Vector3.up );
      }
      if ( result != Vector3.zero )
        return result;

      MeshFilter bestFilter = null;
      float maxExtent = 0.0f;
      foreach ( var filter in tireRigidBody.GetComponentsInChildren<MeshFilter>() ) {
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

      var worldRotationAxis = FindRotationAxisWorld( rb );
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
  }
}
