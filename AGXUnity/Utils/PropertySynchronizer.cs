﻿using System;
using System.Reflection;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AGXUnity.Utils
{
  /// <summary>
  /// This object couples a private serialized field with a
  /// property. When an object has been initialized with a
  /// native reference it's possible to call "Synchronize" and
  /// the class will have all matching properties "set" with
  /// the current value.
  /// 
  /// Following this design pattern enables synchronization of
  /// data with the native ditto seemingly transparent.
  /// </summary>
  public static class PropertySynchronizer
  {
    /// <summary>
    /// Field and property pair. Calling Invoke will fetch
    /// the value of the field and give it as input to
    /// the property.
    /// </summary>
    public struct FieldPropertyPair
    {
      private FieldInfo m_field;
      private PropertyInfo m_property;

      /// <summary>
      /// Valid if the set method of the property is accessible.
      /// </summary>
      public bool IsValid
      {
        get { return m_property.GetSetMethod() != null; }
      }

      /// <summary>
      /// Construct given field and property infos.
      /// </summary>
      /// <param name="field">Object field info.</param>
      /// <param name="property">Object property info.</param>
      public FieldPropertyPair( FieldInfo field, PropertyInfo property )
      {
        m_field = field;
        m_property = property;
      }

      /// <summary>
      /// Invoke property set method given current value of the field.
      /// </summary>
      /// <param name="obj">Object with field and property.</param>
      public void Invoke( object obj, bool propertyGetToSet )
      {
        if ( !IsValid )
          return;

        if ( propertyGetToSet )
          m_property.SetValue( obj, m_property.GetValue( obj ) );
        else
          m_property.SetValue( obj, m_field.GetValue( obj ) );
      }
    }

    private static Dictionary<Type, List<FieldPropertyPair>> m_cache = new Dictionary<Type, List<FieldPropertyPair>>();
    private static Regex m_fieldPropertyMatcher = new Regex( @"\b(m_)([a-z])(\w+)", RegexOptions.Compiled );

    /// <summary>
    /// Searches for field + property match:
    ///   - Field:    m_example ->
    ///   - Property: Example
    /// of same type and invokes set value in the property with
    /// the field value. This is necessary when fields in general
    /// are easy to serialize and the object doesn't know when
    /// the value is written back.
    /// <example>
    /// [SerializeField]
    /// private float m_radius = 1.0f;
    /// public float Radius
    /// {
    ///   get { return m_radius; }
    ///   set
    ///   {
    ///     m_radius = value;
    ///     if ( sphere != null )
    ///       sphere.SetRadius( m_radius );
    ///   }
    /// }
    /// </example>
    /// </summary>
    public static void Synchronize( object obj )
    {
      Synchronize( obj, GetOrCreateSynchronizedProperties( obj.GetType() ), false );
    }

    /// <summary>
    /// Synchronizes using MyProperty = MyProperty, i.e.,
    /// get-method result of the property is used as value
    /// for set-method. This can be used when the property
    /// get method has side-effects, e.g., when resetting/
    /// reading values from a temporary object.
    /// <example>
    /// private float m_radius = 1.0f;
    /// public float Radius
    /// {
    ///   get { return m_tempNative != null ? m_tempNative.getRadius() : m_radius; }
    ///   set
    ///   {
    ///     m_radius = value;
    ///   }
    /// }
    /// 
    /// m_tempNative = new agxCollide.Sphere();
    /// m_tempNative.setRadius( 0.123 );
    /// PropertySynchronizer.SynchronizeGetToSet( this );
    /// Debug.Log( m_radius ); // Prints: 0.123
    /// </example>
    /// </summary>
    public static void SynchronizeGetToSet( object obj )
    {
      Synchronize( obj, GetOrCreateSynchronizedProperties( obj.GetType() ), true );
    }

    /// <summary>
    /// Get (from cache) or find list of field and property pairs enabled
    /// for synchronization.
    /// </summary>
    /// <param name="type">Object type.</param>
    /// <returns>List of field and property pairs that supports synchronization.</returns>
    /// <seealso cref="Synchronize(object)"/>
    public static List<FieldPropertyPair> GetOrCreateSynchronizedProperties( Type type )
    {
      List<FieldPropertyPair> fieldPropertyPairs = null;
      if ( m_cache.TryGetValue( type, out fieldPropertyPairs ) )
        return fieldPropertyPairs;

      fieldPropertyPairs = new List<FieldPropertyPair>();

      CollectFieldPropertyPairs( type, fieldPropertyPairs );
      m_cache.Add( type, fieldPropertyPairs );

      return fieldPropertyPairs;
    }

    /// <summary>
    /// Synchronizes properties given object and synchronized properties array.
    /// </summary>
    /// <param name="obj">Object with field and properties to synchronize.</param>
    /// <param name="synchronizedProperties">List of fields and properties.</param>
    private static void Synchronize( object obj,
                                     List<FieldPropertyPair> synchronizedProperties,
                                     bool propertyGetToSet )
    {
      foreach ( var fieldPropertyPair in synchronizedProperties )
        fieldPropertyPair.Invoke( obj, propertyGetToSet );
    }

    /// <summary>
    /// Recursive method collecting field and property pairs for a given type.
    /// </summary>
    /// <param name="type">Object type.</param>
    /// <param name="fieldPropertyPairs">List of field and property pairs.</param>
    private static void CollectFieldPropertyPairs( Type type, List<FieldPropertyPair> fieldPropertyPairs )
    {
      FieldInfo[] fields = type.GetFields( BindingFlags.Instance |
                                           BindingFlags.NonPublic |
                                           BindingFlags.DeclaredOnly );
      foreach ( FieldInfo field in fields ) {
        // Note: Only serialized field.
        if ( field.IsNotSerialized )
          continue;

        // Matches ["m_"][first lower case char][rest of field name].
        // Group: 0   1           2                     3
        // Note that Groups[0] is the actual name if it follows the pattern.
        Match nameMatch = m_fieldPropertyMatcher.Match( field.Name );
        if ( nameMatch.Success ) {
          // Construct property name as: Group index 2 (first lower case char) to upper.
          //                             Group index 3 (rest of the name).
          string propertyName = nameMatch.Groups[ 2 ].ToString().ToUpper() + nameMatch.Groups[ 3 ];
          PropertyInfo property = type.GetProperty( propertyName );
          // If the property exists and has a "set" defined - execute it.
          if ( property != null &&
               property.GetSetMethod() != null &&
               property.GetCustomAttribute<IgnoreSynchronizationAttribute>() == null )
            fieldPropertyPairs.Add( new FieldPropertyPair( field, property ) );
        }
      }

      if ( TypeSupportsUpdate( type ) )
        CollectFieldPropertyPairs( type.BaseType, fieldPropertyPairs );
    }

    /// <summary>
    /// Checks if the given type is ScriptComponent or ScriptAsset.
    /// </summary>
    /// <param name="type">Type to check.</param>
    /// <returns>True if <paramref name="type"/> is ScriptComponent or ScriptAsset - otherwise false.</returns>
    private static bool TypeSupportsUpdate( Type type )
    {
      return type != null &&
             ( typeof( ScriptComponent ).IsAssignableFrom( type ) ||
               typeof( ScriptAsset ).IsAssignableFrom( type ) );
    }
  }
}
