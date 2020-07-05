using UnityEngine;

namespace AGXUnity.Data
{
  public static class Utils
  {
    public static float ToNormalizedX( ref Rect rect, float value )
    {
      return Mathf.InverseLerp( rect.x, rect.xMax, value );
    }

    public static float ToNormalizedY( ref Rect rect, float value )
    {
      return Mathf.InverseLerp( rect.y, rect.yMax, value );
    }

    public static float FromNormalizedX( ref Rect rect, float value )
    {
      return Mathf.Lerp( rect.x, rect.xMax, value );
    }

    public static float FromNormalizedY( ref Rect rect, float value )
    {
      return Mathf.Lerp( rect.y, rect.yMax, value );
    }

    public static Vector2 ToNormalized( Rect rect, Vector2 point )
    {
      point.x = ToNormalizedX( ref rect, point.x );
      point.y = ToNormalizedY( ref rect, point.y );

      return point;
    }

    public static Vector2 FromNormalized( Rect rect, Vector2 point )
    {
      point.x = FromNormalizedX( ref rect, point.x );
      point.y = FromNormalizedY( ref rect, point.y );

      return point;
    }
  }
}
