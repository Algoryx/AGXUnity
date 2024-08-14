using System;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity
{
  /// <summary>
  /// Static factory object to create shapes, bodies, constraints, wires etc.
  /// </summary>
  public class Factory
  {
    /// <summary>
    /// Creates unique name. If for example there's a game object with name
    /// "body", then Factory.CreateName( "body" ) will return "body (1)".
    /// </summary>
    public static string CreateName( string name )
    {
      int counter = 0;
      string finalName = name;
      while ( GameObject.Find( finalName ) != null )
        finalName = name + " (" + (++counter) + ")";

      return finalName;
    }

    /// <summary>
    /// Creates unique name given type. E.g., "RigidBody_0" for the first
    /// AGXUnity.RigidBody instance, "RigidBody_1" for second etc.
    /// </summary>
    /// <typeparam name="T">Object type.</typeparam>
    /// <returns>Unique name containing the type name.</returns>
    public static string CreateName<T>() where T : ScriptComponent
    {
      Type t = typeof( T );
      string namespaceName = t.Namespace != null ? t.Namespace + "." : "";
      return CreateName( namespaceName + t.Name );
    }

    /// <summary>
    /// Creates a new game object with component of type <typeparamref name="T"/>.
    /// </summary>
    /// <example>
    /// GameObject rb = Factory.Create<RigidBody>();
    /// </example>
    /// <typeparam name="T">Type of component.</typeparam>
    /// <returns>New game object with component <typeparamref name="T"/>.</returns>
    public static GameObject Create<T>() where T : ScriptComponent
    {
      GameObject go = new GameObject( CreateName<T>() );
      go.AddComponent<T>();

      PrefabUtils.PlaceInCurrentStange( go );

      return go;
    }

    /// <summary>
    /// Create new game object with component <typeparamref name="T"/> and adds
    /// <paramref name="child"/> as child.
    /// </summary>
    /// <example>
    /// GameObject rbBox = Factory.Create<RigidBody>( Factor.Create<Box>() );
    /// </example>
    /// <typeparam name="T">Type of component.</typeparam>
    /// <param name="child">Child to new game object.</param>
    /// <returns>New game object with component <typeparamref name="T"/> and child <paramref name="child"/>.</returns>
    public static GameObject Create<T>( GameObject child ) where T : ScriptComponent
    {
      return Create<T>().AddChild( child );
    }

    /// <summary>
    /// Creates constraint of given type.
    /// </summary>
    /// <param name="constraintType">Constraint type.</param>
    /// <param name="givenAttachmentPair">Optional initial attachment pair. If null, a new one will be created.</param>
    /// <returns>Constraint game object if the configuration is valid.</returns>
    public static GameObject Create( ConstraintType constraintType, AttachmentPair givenAttachmentPair = null )
    {
      var constraint = Constraint.Create( constraintType, givenAttachmentPair );
      if ( constraint == null )
        return null;

      PrefabUtils.PlaceInCurrentStange( constraint.gameObject );

      return constraint.gameObject;
    }

    /// <summary>
    /// Creates constraint of given type given local position and rotation relative to <paramref name="rb1"/>.
    /// </summary>
    /// <param name="constraintType">Constraint type.</param>
    /// <param name="localPosition">Position in rb1 frame.</param>
    /// <param name="localRotation">Rotation in rb1 frame.</param>
    /// <param name="rb1">First rigid body instance.</param>
    /// <param name="rb2">Second rigid body instance (world if null).</param>
    /// <returns>Constraint game object if the configuration is valid.</returns>
    public static GameObject Create( ConstraintType constraintType, Vector3 localPosition, Quaternion localRotation, RigidBody rb1, RigidBody rb2 )
    {
      if ( rb1 == null ) {
        Debug.LogWarning( "Unable to create constraint. Reference object must contain a rigid body component." );
        return null;
      }

      GameObject constraintGameObject = Create( constraintType );
      if ( constraintGameObject == null )
        return null;

      Constraint constraint = constraintGameObject.GetComponent<Constraint>();
      constraint.AttachmentPair.ReferenceObject = rb1.gameObject;
      constraint.AttachmentPair.ConnectedObject = rb2 != null ? rb2.gameObject : null;

      constraint.AttachmentPair.ReferenceFrame.LocalPosition = localPosition;
      constraint.AttachmentPair.ReferenceFrame.LocalRotation = localRotation;

      constraint.AttachmentPair.ConnectedFrame.Position = constraint.AttachmentPair.ReferenceFrame.Position;
      constraint.AttachmentPair.ConnectedFrame.Rotation = constraint.AttachmentPair.ReferenceFrame.Rotation;

      return constraintGameObject;
    }

    /// <summary>
    /// Creates constraint of given type given local position and rotation relative to <paramref name="rb1"/>.
    /// </summary>
    /// <param name="constraintType">Constraint type.</param>
    /// <param name="localPosition">Position in rb1 frame.</param>
    /// <param name="localAxis">Axis in rb1 frame.</param>
    /// <param name="rb1">First rigid body instance.</param>
    /// <param name="rb2">Second rigid body instance (world if null).</param>
    /// <returns>Constraint game object if the configuration is valid.</returns>
    public static GameObject Create( ConstraintType constraintType, Vector3 localPosition, Vector3 localAxis, RigidBody rb1, RigidBody rb2 )
    {
      return Create( constraintType, localPosition, Quaternion.FromToRotation( Vector3.forward, localAxis ), rb1, rb2 );
    }

    /// <summary>
    /// Create a wire given route, radius, resolution and material.
    /// </summary>
    /// <param name="radius">Radius if the wire.</param>
    /// <param name="resolutionPerUnitLength">Resolution of the wire.</param>
    /// <param name="material">Shape material of the wire.</param>
    /// <returns>A new game object with a Wire component.</returns>
    public static Wire CreateWire( float radius = 0.02f, float resolutionPerUnitLength = 1.5f, ShapeMaterial material = null )
    {
      GameObject go = new GameObject( CreateName<Wire>() );
      Wire wire     = go.AddComponent<Wire>();

      wire.Radius                  = radius;
      wire.ResolutionPerUnitLength = resolutionPerUnitLength;
      wire.Material                = material;

      PrefabUtils.PlaceInCurrentStange( go );

      return wire;
    }

    /// <summary>
    /// Create a cable given route, radius, resolution and material.
    /// </summary>
    /// <param name="radius">Radius of the cable.</param>
    /// <param name="resolutionPerUnitLength">Resolution of the cable.</param>
    /// <param name="material">Shape material of the cable.</param>
    /// <returns>Cable component.</returns>
    public static Cable CreateCable( float radius = 0.05f, float resolutionPerUnitLength = 5.0f, ShapeMaterial material = null )
    {
      GameObject go = new GameObject( CreateName<Cable>() );
      Cable cable   = go.AddComponent<Cable>();

      cable.Radius                  = radius;
      cable.ResolutionPerUnitLength = resolutionPerUnitLength;
      cable.Material                = material;

      PrefabUtils.PlaceInCurrentStange( go );

      return cable;
    }
  }
}
