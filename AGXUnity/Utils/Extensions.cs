using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Text.RegularExpressions;

namespace AGXUnity.Utils
{
  /// <summary>
  /// Extensions to Unity Engine GameObject class.
  /// </summary>
  public static partial class Extensions
  {
    /// <summary>
    /// Add child to parent game object. I.e., the parent transform is
    /// inherited by the child. By default (makeCurrentTransformLocal = true),
    /// the current global position of the child is becoming the local relative
    /// the parent. If makeCurrentTransformLocal = false the child position in
    /// world will be preserved.
    /// </summary>
    /// <param name="parent">Extension.</param>
    /// <param name="child">Child to add.</param>
    /// <param name="makeCurrentTransformLocal">If true, the current global transform of the child
    ///                                         will be moved to be local transform in the parent.
    ///                                         If false, the current global transform of the child
    ///                                         will be preserved.</param>
    /// <returns>The parent (this).</returns>
    /// <example>
    /// GameObject go = new GameObject( "go" );
    /// GameObject child = new GameObject( "child" );
    /// go.AddChild( child.AddChild( new GameObject( "childOfChild" ) ) );
    /// </example>
    public static GameObject AddChild( this GameObject parent, GameObject child, bool makeCurrentTransformLocal = true )
    {
      if ( parent == null || child == null ) {
        Debug.LogWarning( "Parent and/or child is null. Parent: " + parent + ", child: " + child );
        return null;
      }

      Vector3 posBefore = child.transform.position;
      Quaternion rotBefore = child.transform.rotation;

      child.transform.parent = parent.transform;

      if ( makeCurrentTransformLocal ) {
        child.transform.localPosition = posBefore;
        child.transform.localRotation = rotBefore;
      }

      return parent;
    }

    /// <summary>
    /// Finds root/top level game object.
    /// </summary>
    /// <returns>Root/top level game object.</returns>
    public static GameObject GetRoot( this GameObject gameObject )
    {
      if ( gameObject == null )
        return null;

      return gameObject.transform.root.gameObject;
    }

    /// <summary>
    /// Visits all children to this game object.
    /// </summary>
    /// <param name="visitor">Game object visitor.</param>
    public static void TraverseChildren( this GameObject gameObject, Action<GameObject> visitor )
    {
      if ( visitor == null )
        return;

      foreach ( Transform child in gameObject.transform )
        Traverse( child, visitor );
    }

    private static void Traverse( Transform transform, Action<GameObject> visitor )
    {
      visitor( transform.gameObject );
      foreach ( Transform child in transform )
        Traverse( child, visitor );
    }

    /// <summary>
    /// Returns an initialized component - if present.
    /// </summary>
    /// <typeparam name="T">Component type.</typeparam>
    /// <returns>Initialized component of type T. Null if not present or not possible to initialize.</returns>
    public static T GetInitializedComponent<T>( this GameObject gameObject )
      where T : ScriptComponent
    {
      T component = gameObject.GetComponent<T>();
      if ( component == null )
        return null;
      return component.GetInitialized<T>();
    }

    /// <summary>
    /// Returns a set of initialized components - if any and all components were initialized properly.
    /// </summary>
    /// <remarks>
    /// If one component in the set of components fails to initialize, an exception
    /// is thrown, leaving the rest of the components uninitialized.
    /// </remarks>
    /// <typeparam name="T">Component type.</typeparam>
    /// <returns>
    /// Initialized components of type T. Empty set of none present and throws an exception
    /// if one component fails to initialize.
    /// </returns>
    public static T[] GetInitializedComponents<T>( this GameObject gameObject )
      where T : ScriptComponent
    {
      T[] components = gameObject.GetComponents<T>();
      foreach ( T component in components )
        if ( !component.GetInitialized<T>() )
          throw new AGXUnity.Exception( "Unable to initialize component of type: " + typeof( T ).Name );
      return components;
    }

    /// <summary>
    /// Similar to GameObject.GetComponentInChildren but returns an initialized ScriptComponent.
    /// </summary>
    /// <typeparam name="T">Component type.</typeparam>
    /// <param name="includeInactive">True to include inactive components, default false.</param>
    /// <returns>Initialized child component of type T. Null if not present or not possible to initialize.</returns>
    public static T GetInitializedComponentInChildren<T>( this GameObject gameObject, bool includeInactive = false )
      where T : ScriptComponent
    {
      T component = gameObject.GetComponentInChildren<T>( includeInactive );
      if ( component == null )
        return null;
      return component.GetInitialized<T>();
    }

    /// <summary>
    /// Similar to GameObject.GetComponentInParent but returns an initialized ScriptComponent.
    /// </summary>
    /// <typeparam name="T">Component type.</typeparam>
    /// <returns>Initialized parent component of type T. Null if not present or not possible to initialize.</returns>
    public static T GetInitializedComponentInParent<T>( this GameObject gameObject )
      where T : ScriptComponent
    {
      T component = gameObject.GetComponentInParent<T>();
      if ( component == null )
        return null;
      return component.GetInitialized<T>();
    }

    /// <summary>
    /// Check if parent has child in its children transform.
    /// </summary>
    /// <param name="parent">Parent.</param>
    /// <param name="child">Child.</param>
    /// <returns>true if child has parent as parent.</returns>
    public static bool HasChild( this GameObject parent, GameObject child )
    {
      if ( child == null )
        return false;

      // What's expected when parent == child? Let Unity decide.
      return child.transform.IsChildOf( parent.transform );
    }

    /// <summary>
    /// Fetch component if already present or creates new.
    /// </summary>
    /// <typeparam name="T">Component type.</typeparam>
    /// <returns>Already existing or new component.</returns>
    public static T GetOrCreateComponent<T>( this GameObject gameObject )
      where T : Component
    {
      return gameObject.GetComponent<T>() ?? gameObject.AddComponent<T>();
    }
  }

  /// <summary>
  /// Extensions to Unity Engine math classes.
  /// </summary>
  public static partial class Extensions
  {
    /// <summary>
    /// Direct convert from agx.Vec2 to Vector2.
    /// </summary>
    public static Vector2 ToVector2( this agx.Vec2 v )
    {
      return new Vector2( (float)v.x, (float)v.y );
    }

    /// <summary>
    /// Direct convert from agx.Vec2f to Vector2.
    /// </summary>
    public static Vector2 ToVector2( this agx.Vec2f v )
    {
      return new Vector3( v.x, v.y );
    }

    /// <summary>
    /// Direct convert from agx.Vec3 to Vector3.
    /// </summary>
    /// <seealso cref="ToHandedVector3(agx.Vec3)"/>
    public static Vector3 ToVector3( this agx.Vec3 v )
    {
      return new Vector3( (float)v.x, (float)v.y, (float)v.z );
    }

    /// <summary>
    /// Direct convert from agx.Vec3f to Vector3.
    /// </summary>
    /// <seealso cref="ToHandedVector3(agx.Vec3)"/>
    public static Vector3 ToVector3( this agx.Vec3f v )
    {
      return new Vector3( v.x, v.y, v.z );
    }

    /// <summary>
    /// Direct convert from Vector3 to agx.Vec3.
    /// </summary>
    /// <seealso cref="ToHandedVec3(Vector3)"/>
    public static agx.Vec3 ToVec3( this Vector3 v )
    {
      return new agx.Vec3( (double)v.x, (double)v.y, (double)v.z );
    }

    /// <summary>
    /// Direct convert from Vector3 to agx.Vec3f.
    /// </summary>
    public static agx.Vec3f ToVec3f( this Vector3 v )
    {
      return new agx.Vec3f( v.x, v.y, v.z );
    }

    /// <summary>
    /// Convert from agx.Vec3 to Vector3 - flipping x axis, transforming from
    /// left/right handed to right/left handed coordinate system.
    /// </summary>
    /// <seealso cref="ToHandedVector3(agx.Vec3)"/>
    public static Vector3 ToHandedVector3( this agx.Vec3 v )
    {
      return new Vector3( -(float)v.x, (float)v.y, (float)v.z );
    }

    /// <summary>
    /// Convert from Vector3 to agx.Vec3 - flipping x axis, transforming from
    /// left/right handed to right/left handed coordinate system.
    /// </summary>
    public static agx.Vec3 ToHandedVec3( this Vector3 v )
    {
      return new agx.Vec3( -(double)v.x, (double)v.y, (double)v.z );
    }

    /// <summary>
    /// Convert from agx.Vec3f to Vector3 - flipping x axis, transforming from
    /// left/right handed to right/left handed coordinate system.
    /// </summary>
    public static Vector3 ToHandedVector3( this agx.Vec3f v )
    {
      return new Vector3( -v.x, v.y, v.z );
    }

    /// <summary>
    /// Convert from Vector3 to agx.Vec3f - flipping x axis, transforming from
    /// left/right handed to right/left handed coordinate system.
    /// </summary>
    public static agx.Vec3f ToHandedVec3f( this Vector3 v )
    {
      return new agx.Vec3f( -v.x, v.y, v.z );
    }

    /// <summary>
    /// Convert from right handed Vector3 to left handed Vector3 by flipping x-axis.
    /// </summary>
    /// <param name="v">Right handed Vector3.</param>
    /// <returns>Left handed Vector3.</returns>
    public static Vector3 ToLeftHanded( this Vector3 v )
    {
      return new Vector3( -v.x, v.y, v.z );
    }

    /// <summary>
    /// Convert from agx.Vec4 to UnityEngine.Color.
    /// </summary>
    /// <returns>Vec4 as Color.</returns>
    public static Color ToColor( this agx.Vec4 v )
    {
      return new Color( (float)v.x, (float)v.y, (float)v.z, (float)v.w );
    }

    /// <summary>
    /// Convert from agx.Vec4f to UnityEngine.Color.
    /// </summary>
    /// <returns>Vec4f as Color.</returns>
    public static Color ToColor( this agx.Vec4f v )
    {
      return new Color( v.x, v.y, v.z, v.w );
    }

    /// <summary>
    /// Length of this quaternion.
    /// </summary>
    public static float Length( this Quaternion q )
    {
      return Mathf.Sqrt( q.Length2() );
    }

    /// <summary>
    /// Length squared of this quaternion.
    /// </summary>
    public static float Length2( this Quaternion q )
    {
      return q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
    }

    /// <returns>A new, normalized quaternion.</returns>
    public static Quaternion Normalize( this Quaternion q )
    {
      Quaternion result;
      float inv = 1.0f / q.Length();
      result.x = q.x * inv;
      result.y = q.y * inv;
      result.z = q.z * inv;
      result.w = q.w * inv;
      return result;
    }

    /// <summary>
    /// Converts an left/right handed agx.Quat to a right/left handed Quaternion.
    /// </summary>
    public static Quaternion ToHandedQuaternion( this agx.Quat q )
    {
      return new Quaternion( -(float)q.x, (float)q.y, (float)q.z, -(float)q.w );
    }

    /// <summary>
    /// Converts an left/right handed Quaternion to a right/left handed agx.Quat.
    /// </summary>
    public static agx.Quat ToHandedQuat( this Quaternion q )
    {
      return new agx.Quat( -q.x, q.y, q.z, -q.w );
    }

    // Extensions GetTranslate, GetRotation and GetScale:
    //   - http://forum.unity3d.com/threads/how-to-assign-matrix4x4-to-transform.121966/

    /// <summary>
    /// Extract translation from transform matrix.
    /// </summary>
    /// <param name="matrix">Transform matrix. This parameter is passed by reference
    /// to improve performance; no changes will be made to it.</param>
    /// <returns>
    /// Translation offset.
    /// </returns>
    public static Vector3 GetTranslate( this Matrix4x4 matrix )
    {
      Vector3 translate;
      translate.x = matrix.m03;
      translate.y = matrix.m13;
      translate.z = matrix.m23;
      return translate;
    }

    /// <summary>
    /// Extract rotation quaternion from transform matrix.
    /// </summary>
    /// <param name="matrix">Transform matrix. This parameter is passed by reference
    /// to improve performance; no changes will be made to it.</param>
    /// <returns>
    /// Quaternion representation of rotation transform.
    /// </returns>
    public static Quaternion GetRotation( this Matrix4x4 matrix )
    {
      Vector3 forward;
      forward.x = matrix.m02;
      forward.y = matrix.m12;
      forward.z = matrix.m22;

      Vector3 upwards;
      upwards.x = matrix.m01;
      upwards.y = matrix.m11;
      upwards.z = matrix.m21;

      return Quaternion.LookRotation( forward, upwards );
    }

    /// <summary>
    /// Extract scale from transform matrix.
    /// </summary>
    /// <param name="matrix">Transform matrix. This parameter is passed by reference
    /// to improve performance; no changes will be made to it.</param>
    /// <returns>
    /// Scale vector.
    /// </returns>
    public static Vector3 GetScale( this Matrix4x4 matrix )
    {
      Vector3 scale;
      scale.x = new Vector4( matrix.m00, matrix.m10, matrix.m20, matrix.m30 ).magnitude;
      scale.y = new Vector4( matrix.m01, matrix.m11, matrix.m21, matrix.m31 ).magnitude;
      scale.z = new Vector4( matrix.m02, matrix.m12, matrix.m22, matrix.m32 ).magnitude;
      return scale;
    }

    public static int[] SortedPermutation( this Vector3 v )
    {
      int[] ret = new int[] { 0, 1, 2 };
      Array.Sort( ret, ( i1, i2 ) => { return v[ i1 ].CompareTo( v[ i2 ] ); } );
      return ret;
    }

    public static int MinIndex( this Vector3 v )
    {
      return v.SortedPermutation()[ 0 ];
    }

    public static int MiddleIndex( this Vector3 v )
    {
      return v.SortedPermutation()[ 1 ];
    }

    public static int MaxIndex( this Vector3 v )
    {
      return v.SortedPermutation()[ 2 ];
    }

    public static float MinValue( this Vector3 v )
    {
      return v[ v.MinIndex() ];
    }

    public static float MiddleValue( this Vector3 v )
    {
      return v[ v.MiddleIndex() ];
    }

    public static float MaxValue( this Vector3 v )
    {
      return v[ v.MaxIndex() ];
    }

    public static Vector3 ClampedElementsAbove( this Vector3 v, float minValue )
    {
      Vector3 ret = new Vector3();
      ret.x = Mathf.Max( v.x, minValue );
      ret.y = Mathf.Max( v.y, minValue );
      ret.z = Mathf.Max( v.z, minValue );
      return ret;
    }

    private static Dictionary<Color, string> m_colorTagCache = new Dictionary<Color, string>();
    public static string ToHexStringRGBA( this Color color )
    {
      string result;
      if ( m_colorTagCache.TryGetValue( color, out result ) )
        return result;

      result = "#" + ( (int)( 255 * color.r ) ).ToString( "X2" ) + ( (int)( 255 * color.g ) ).ToString( "X2" ) + ( (int)( 255 * color.b ) ).ToString( "X2" ) + ( (int)( 255 * color.a ) ).ToString( "X2" );
      m_colorTagCache.Add( color, result );

      return result;
    }

    public static string ToHexStringRGB( this Color color )
    {
      return "#" + ( (int)( 255 * color.r ) ).ToString( "X2" ) + ( (int)( 255 * color.g ) ).ToString( "X2" ) + ( (int)( 255 * color.b ) ).ToString( "X2" );
    }

    public static Vector3 ReadVector3( this System.IO.BinaryReader stream )
    {
      return new Vector3( stream.ReadSingle(), stream.ReadSingle(), stream.ReadSingle() );
    }
  }

  /// <summary>
  /// Extensions for system string.
  /// </summary>
  public static partial class Extensions
  {
    public static string Color( this string str, Color color )
    {
      return GUI.AddColorTag( str, color );
    }

    public static uint To32BitFnv1aHash( this string str )
    {
      uint hash = 2166136261u;
      foreach ( char val in str ) {
        hash ^= val;
        hash *= 16777619u;
      }

      return hash;
    }

    public static string SplitCamelCase( this string str )
    {
      return Regex.Replace(
              Regex.Replace(
                  str,
                  @"(\P{Ll})(\P{Ll}\p{Ll})",
                  "$1 $2"
              ),
              @"(\p{Ll})(\P{Ll})",
              "$1 $2"
            );
    }

    public static string FirstCharToUpperCase( this string str )
    {
      if ( string.IsNullOrEmpty( str ) )
        return str;

      return str.First().ToString().ToUpper() + str.Substring( 1 );
    }

    /// <summary>
    /// Make absolute/complete path relative to given root path.
    /// </summary>
    /// <example>
    /// <code>
    /// var relativeToProjectPath = absolutePath.MakeRelative( Application.dataPath );
    /// </code>
    /// </example>
    /// <param name="complete">Complete path.</param>
    /// <param name="root">Root path.</param>
    /// <returns>Path relative to <paramref name="root"/>.</returns>
    public static string MakeRelative( this string complete,
                                       string root,
                                       bool includeTopRoot = true )
    {
      var completeUri = new Uri( complete );
      var rootUri = new Uri( root );
      var relUri = rootUri.MakeRelativeUri( completeUri );
      var result = Uri.UnescapeDataString( relUri.ToString() );
      if ( !string.IsNullOrEmpty( result ) && !includeTopRoot ) {
        var di = new System.IO.DirectoryInfo( root );
        if ( result.StartsWith( di.Name ) )
          result = result.Remove( 0, di.Name.Length + 1 );
      }
      return result;
    }

    /// <summary>
    /// Makes path relative to application path and replaces \\ with /.
    /// </summary>
    /// <param name="path">Absolute or relative path.</param>
    /// <returns>Path relative to application root and with / instead of \\.</returns>
    public static string PrettyPath( this string path )
    {
      var result = string.Copy( path );

      // Unity AssetDataBase and/or Resources doesn't support
      // intermediate relative (..) path, e.g., Assets/Foo/../Bar/file.txt.
      if ( result.Contains( ".." ) )
        result = System.IO.Path.GetFullPath( result );

      result = result.Replace( '\\', '/' );

      if ( System.IO.Path.IsPathRooted( result ) )
        result = result.MakeRelative( Application.dataPath );
      if ( result.StartsWith( "./" ) )
        result = result.Remove( 0, 2 );
      return result;
    }

    /// <summary>
    /// Split on space but exclude multiple spaces, e.g.,
    /// "0.2      3  4" will return ["0.2", "3", "4"].
    /// </summary>
    /// <returns>Array of strings not containing entries with empty spaces.</returns>
    public static string[] SplitSpace( this string str )
    {
      return str.Split( s_strSplit, StringSplitOptions.RemoveEmptyEntries );
    }

    /// <summary>
    /// Find index in string array given predicate.
    /// </summary>
    /// <param name="predicate">String predicate.</param>
    /// <returns>Index where <paramref name="predicate"/> returns true, -1 if not found.</returns>
    public static int IndexOf( this string[] strs, Func<string, bool> predicate )
    {
      for ( int i = 0; predicate != null && i < strs.Length; ++i )
        if ( predicate.Invoke( strs[ i ] ) )
          return i;
      return -1;
    }

    /// <summary>
    /// Parse 3 floats from string array from <paramref name="startIndex"/> to <paramref name="startIndex"/> + 2.
    /// </summary>
    /// <param name="startIndex">Start index.</param>
    /// <returns>Vector3 if successful, throws on failures.</returns>
    public static Vector3 ParseVector3( this string[] strs, int startIndex )
    {
      if ( startIndex + 2 >= strs.Length )
        throw new IndexOutOfRangeException( $"Unable to parse Vector3: End index {startIndex + 2} >= length " +
                                            $"of string array with {strs.Length} elements." );
      return new Vector3( float.Parse( strs[ startIndex + 0 ] ),
                          float.Parse( strs[ startIndex + 1 ] ),
                          float.Parse( strs[ startIndex + 2 ] ) );
    }

    private static char[] s_strSplit = new char[] { ' ' };
  }

  /// <summary>
  /// Extensions for UnityEngine.Material.
  /// </summary>
  public static partial class Extensions
  {
    /// <summary>
    /// Set blend mode from script. Taken from:
    /// https://forum.unity3d.com/threads/standard-material-shader-ignoring-setfloat-property-_mode.344557/#post-2229980
    /// </summary>
    /// <param name="blendMode"></param>
    public static void SetBlendMode( this Material material, Rendering.BlendMode blendMode )
    {
      switch ( blendMode ) {
        case Rendering.BlendMode.Opaque:
          material.SetOverrideTag( "RenderType", "" );
          material.SetInt( "_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One );
          material.SetInt( "_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero );
          material.SetInt( "_ZWrite", 1 );
          material.DisableKeyword( "_ALPHATEST_ON" );
          material.DisableKeyword( "_ALPHABLEND_ON" );
          material.DisableKeyword( "_ALPHAPREMULTIPLY_ON" );
          material.renderQueue = -1;
          break;
        case Rendering.BlendMode.Cutout:
          material.SetOverrideTag( "RenderType", "TransparentCutout" );
          material.SetInt( "_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One );
          material.SetInt( "_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero );
          material.SetInt( "_ZWrite", 1 );
          material.EnableKeyword( "_ALPHATEST_ON" );
          material.DisableKeyword( "_ALPHABLEND_ON" );
          material.DisableKeyword( "_ALPHAPREMULTIPLY_ON" );
          material.renderQueue = 2450;
          break;
        case Rendering.BlendMode.Fade:
          material.SetOverrideTag( "RenderType", "Transparent" );
          material.SetInt( "_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha );
          material.SetInt( "_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha );
          material.SetInt( "_ZWrite", 0 );
          material.DisableKeyword( "_ALPHATEST_ON" );
          material.EnableKeyword( "_ALPHABLEND_ON" );
          material.DisableKeyword( "_ALPHAPREMULTIPLY_ON" );
          material.renderQueue = 3000;
          break;
        case Rendering.BlendMode.Transparent:
          material.SetOverrideTag( "RenderType", "Transparent" );
          material.SetInt( "_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One );
          material.SetInt( "_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha );
          material.SetInt( "_ZWrite", 0 );
          material.DisableKeyword( "_ALPHATEST_ON" );
          material.DisableKeyword( "_ALPHABLEND_ON" );
          material.EnableKeyword( "_ALPHAPREMULTIPLY_ON" );
          material.renderQueue = 3000;
          break;
      }
    }
  }
}
