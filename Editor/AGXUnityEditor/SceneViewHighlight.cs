using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AGXUnity.Rendering;

namespace AGXUnityEditor
{
  [InitializeOnLoad]
  public static class SceneViewHighlight
  {
    public static void Add<T>( T instance )
      where T : Object
    {
      var transform = GetTransform( instance );
      if ( transform == null )
        return;

      if ( s_renderGizmosData.ContainsKey( transform ) )
        return;

      s_renderGizmosData.Add( transform, GetRenderData( transform ) );
    }

    public static void Remove<T>( T instance )
      where T : Object
    {
      var transform = GetTransform( instance );
      if ( transform == null )
        return;

      s_renderGizmosData.Remove( transform );
    }

    [DrawGizmo( GizmoType.NonSelected | GizmoType.Selected )]
    private static void OnDrawGizmo( Transform target, GizmoType gizmoType )
    {
      RenderData renderData;
      if ( !s_renderGizmosData.TryGetValue( target, out renderData ) )
        return;

      var maxColor = new Color( 1, 1, 1, s_globalAlpha );
      foreach ( var filter in renderData.MeshFilters ) {
        Gizmos.color = s_timeInterpolator.Lerp( renderData.Color, maxColor );
        Gizmos.matrix = filter.transform.localToWorldMatrix;
        Gizmos.DrawWireMesh( filter.sharedMesh );
      }
    }

    private static Transform GetTransform<T>( T instance )
      where T : Object
    {
      if ( instance == null )
        return null;

      if ( instance is GameObject )
        return ( instance as GameObject ).transform;
      else if ( instance is Component )
        return ( instance as Component ).transform;

      return null;
    }

    private static Color GetColor( Transform transform )
    {
      Color color;
      if ( !s_colorCache.TryGetValue( transform, out color ) ) {
        color = FindColor( Color.white );
        s_colorCache.Add( transform, color );
      }

      return color;
    }

    private static Color FindColor( Color mix )
    {
      var oldState = Random.state;
      Random.state = s_randomState;
      var color = new Color( Random.value, Random.value, Random.value );
      s_randomState = Random.state;
      Random.state = oldState;

      color.r = 0.5f * ( color.r + mix.r );
      color.g = 0.5f * ( color.g + mix.g );
      color.b = 0.5f * ( color.b + mix.b );
      color.a = s_globalAlpha;

      return color;
    }

    private static RenderData GetRenderData( Transform transform )
    {
      var filters = transform.GetComponents<MeshFilter>();
      if ( filters.Length == 0 ) {
        if ( transform.GetComponent<DebugRenderData>() != null && transform.GetComponent<DebugRenderData>().Node != null )
          filters = transform.GetComponent<DebugRenderData>().Node.GetComponentsInChildren<MeshFilter>();
        else if ( transform.GetComponent<AGXUnity.Collide.Shape>() != null ) {
          var shape = transform.GetComponent<AGXUnity.Collide.Shape>();
          var shapeVisual = ShapeVisual.Find( shape );
          if ( shapeVisual != null )
            filters = shapeVisual.GetComponentsInChildren<MeshFilter>();
        }
        else if ( transform.GetComponent<AGXUnity.RigidBody>() != null )
          filters = transform.GetComponentsInChildren<MeshFilter>();
      }
      return new RenderData()
      {
        Color = GetColor( transform ),
        MeshFilters = filters
      };
    }

    private struct RenderData
    {
      public Color Color;
      public MeshFilter[] MeshFilters;
    }

    static SceneViewHighlight()
    {
      var prevState = Random.state;
      Random.InitState( 73 );
      s_randomState = Random.state;
      Random.state = prevState;

      s_timeInterpolator = new Utils.TimeInterpolator01( 4.0f, 2.0f );
    }

    private static Random.State s_randomState = new Random.State();
    private static Dictionary<Transform, Color> s_colorCache = new Dictionary<Transform, Color>();
    private static Dictionary<Transform, RenderData> s_renderGizmosData = new Dictionary<Transform, RenderData>();
    private static float s_globalAlpha = 0.25f;
    private static Utils.TimeInterpolator01 s_timeInterpolator = null;
  }
}
