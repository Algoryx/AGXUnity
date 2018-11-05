using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AGXUnity.Utils
{
  public class ShapeInitializationData
  {
    public enum Axes
    {
      None,
      Axis_1,
      Axis_2,
      Axis_3,
      Default
    }

    public class AxisData
    {
      public Vector3 Direction = Vector3.zero;
      public float Radius = 0f;
      public float Height = 0f;
    }

    public static ShapeInitializationData Create( GameObject gameObject )
    {
      return Create( new GameObject[] { gameObject } ).FirstOrDefault();
    }

    public static ShapeInitializationData[] Create( GameObject[] gameObjects )
    {
      if ( gameObjects == null || gameObjects.Length == 0 )
        return new ShapeInitializationData[] { };

      if ( gameObjects.Length != 1 ) {
        Debug.LogError( "Multi-selection currently not supported to create shapes." );
        return new ShapeInitializationData[] { };
      }

      // Bounds.Encapsulate!

      List<ShapeInitializationData> result = new List<ShapeInitializationData>();
      foreach ( var gameObject in gameObjects ) {
        if ( gameObject == null )
          continue;

        MeshFilter filter = gameObject.GetComponent<MeshFilter>();
        if ( filter == null )
          continue;

        Bounds localBounds = filter.sharedMesh.bounds;
        result.Add( new ShapeInitializationData()
        {
          LocalBounds  = localBounds,
          LocalExtents = filter.transform.InverseTransformDirection( filter.transform.TransformVector( localBounds.extents ) ),
          WorldCenter  = filter.transform.TransformPoint( localBounds.center ),
          Rotation     = filter.transform.rotation,
          Filter       = filter
        } );
      }

      return result.ToArray();
    }

    public Bounds LocalBounds;
    public Vector3 LocalExtents;
    public Vector3 WorldCenter;
    public Quaternion Rotation;
    public MeshFilter Filter;

    public void SetDefaultPositionRotation( GameObject gameObject )
    {
      gameObject.transform.position = WorldCenter;
      gameObject.transform.rotation = Rotation;
    }

    public void SetPositionRotation( GameObject gameObject, Vector3 axis )
    {
      gameObject.transform.position = WorldCenter;
#if UNITY_2018_1_OR_NEWER
      gameObject.transform.rotation = Rotation * Quaternion.FromToRotation( Vector3.up, axis ).normalized;
#else
      gameObject.transform.rotation = Rotation * Quaternion.FromToRotation( Vector3.up, axis ).Normalize();
#endif
    }

    public AxisData FindAxisData( Axes axis, bool expandRadius )
    {
      if ( axis == Axes.None )
        return null;

      AxisData data = null;
      if ( axis == Axes.Axis_1 ) {
        data = new AxisData()
        {
          Radius    = expandRadius ?
                        new Vector2( LocalExtents.MiddleValue(), LocalExtents.MinValue() ).magnitude :
                        LocalExtents.MiddleValue(),
          Height    = 2f * LocalExtents.MaxValue()
        };

        data.Direction[ LocalExtents.MaxIndex() ] = 1f;
      }
      else if ( axis == Axes.Axis_2 ) {
        data = new AxisData()
        {
          Radius = expandRadius ?
                        new Vector2( LocalExtents.MaxValue(), LocalExtents.MinValue() ).magnitude :
                        LocalExtents.MaxValue(),
          Height = 2f * LocalExtents.MiddleValue()
        };

        data.Direction[ LocalExtents.MiddleIndex() ] = 1f;
      }
      else if ( axis == Axes.Axis_3 ) {
        data = new AxisData()
        {
          Radius = expandRadius ?
                        new Vector2( LocalExtents.MiddleValue(), LocalExtents.MinValue() ).magnitude :
                        LocalExtents.MaxValue(),
          Height = 2f * LocalExtents.MinValue()
        };

        data.Direction[ LocalExtents.MinIndex() ] = 1f;
      }
      else if ( axis == Axes.Default ) {
        data = new AxisData();
        data.Direction = Vector3.up;
      }

      return data;
    }
  }
}
