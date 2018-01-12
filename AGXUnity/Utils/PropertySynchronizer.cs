using System;
using System.Reflection;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.Linq.Expressions;

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
    /// Object with field getter and property setter optimized to
    /// not use reflection. This is about twice as fast as reflection
    /// for a single object.
    /// </summary>
    public class FieldPropertyPair
    {
      private Func<object, object> m_getter = null;
      private Action<object> m_setter = null;

      public bool IsValid { get { return m_getter != null && m_setter != null; } }

      public FieldPropertyPair( object obj, FieldInfo field, PropertyInfo property )
      {
        {
          var setMethod = property.GetSetMethod();
          var parameterType = setMethod.GetParameters().First().ParameterType;
          var parameter = Expression.Parameter( typeof( object ), "val" );
          var methodCall = Expression.Call( Expression.Constant( obj ), setMethod, Expression.Convert( parameter, parameterType ) );
          m_setter = Expression.Lambda<Action<object>>( methodCall, parameter ).Compile();
        }

        {
          var objParam     = Expression.Parameter( typeof( object ), "obj" );
          var objConverted = Expression.Convert( objParam, field.DeclaringType );
          var memberField  = Expression.Field( objConverted, field );

          Expression getterMember = field.FieldType.IsValueType ?
                                      Expression.Convert( memberField, typeof( object ) ) :
                                      (Expression)memberField;
          m_getter = Expression.Lambda<Func<object, object>>( getterMember, objParam ).Compile();
        }
      }

      public void Invoke( object obj )
      {
        if ( !IsValid )
          return;

        m_setter( m_getter( obj ) );
      }
    }

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
      if ( obj is ScriptComponent ) {
        var component = obj as ScriptComponent;
        if ( component.SynchronizedProperties == null )
          component.SynchronizedProperties = CreateSynchronizedProperties( component, component.GetType() );

        Synchronize( component, component.SynchronizedProperties );
      }
      else if ( obj is ScriptAsset ) {
        var asset = obj as ScriptAsset;
        if ( asset.SynchronizedProperties == null )
          asset.SynchronizedProperties = CreateSynchronizedProperties( asset, asset.GetType() );

        Synchronize( asset, asset.SynchronizedProperties );
      }
      else if ( obj != null )
        FindAndUpdateProperties( obj, obj.GetType() );
    }

    /// <summary>
    /// Synchronizes properties given object and synchronized properties array.
    /// </summary>
    /// <param name="obj">Object with field and properties to synchronize.</param>
    /// <param name="synchronizedProperties">List of fields and properties.</param>
    public static void Synchronize( object obj, FieldPropertyPair[] synchronizedProperties )
    {
      for ( int i = 0; i < synchronizedProperties.Length; ++i )
        synchronizedProperties[ i ].Invoke( obj );
    }

    /// <summary>
    /// Creates list of matching field and property pairs for a given type.
    /// For synchronization simply: fieldPropertyPair.Invoke( obj )
    /// </summary>
    /// <param name="type">Object type.</param>
    /// <returns>Array of matching field and property pairs.</returns>
    public static FieldPropertyPair[] CreateSynchronizedProperties( object obj, Type type )
    {
      var synchronziedProperties = new List<FieldPropertyPair>();
      Action<FieldInfo, PropertyInfo> collector = ( field, property ) =>
      {
        synchronziedProperties.Add( new FieldPropertyPair( obj, field, property ) );
      };

      FindSynchronizedProperties( type, collector );

      return synchronziedProperties.ToArray();
    }

    /// <summary>
    /// Parses all non-public fields and looks for a matching property to
    /// invoke with the current value of the field.
    /// </summary>
    /// <param name="obj">Object to parse and update.</param>
    /// <param name="type">Type of the object.</param>
    private static void FindAndUpdateProperties( object obj, Type type )
    {
      Action<FieldInfo, PropertyInfo> invoker = ( field, property ) =>
      {
        property.SetValue( obj, field.GetValue( obj ), null );
      };

      FindSynchronizedProperties( type, invoker );
    }

    /// <summary>
    /// Finds matching field and property and invokes <paramref name="matchCallback"/>.
    /// </summary>
    /// <param name="type">Object type.</param>
    /// <param name="matchCallback">Callback when matching field and property is found.</param>
    private static void FindSynchronizedProperties( Type type, Action<FieldInfo, PropertyInfo> matchCallback )
    {
      FieldInfo[] fields = type.GetFields( BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly );
      foreach ( FieldInfo field in fields ) {
        // Note: Only serialized field.
        if ( field.IsNotSerialized )
          continue;

        // Matches ["m_"][first lower case char][rest of field name].
        // Group: 0   1           2                     3
        // Note that Groups[0] is the actual name if it follows the pattern.
        Match nameMatch = Regex.Match( field.Name, @"\b(m_)([a-z])(\w+)" );
        if ( nameMatch.Success ) {
          // Construct property name as: Group index 2 (first lower case char) to upper.
          //                             Group index 3 (rest of the name).
          string propertyName = nameMatch.Groups[ 2 ].ToString().ToUpper() + nameMatch.Groups[ 3 ];
          PropertyInfo property = type.GetProperty( propertyName );
          // If the property exists and has a "set" defined - execute it.
          if ( property != null &&
               property.GetSetMethod() != null &&
               property.GetCustomAttributes( typeof( IgnoreSynchronization ), false ).Length == 0 )
            matchCallback( field, property );
        }
      }

      // Unsure if this is necessary to recursively update supported objects...
      if ( TypeSupportsUpdate( type.BaseType ) )
        FindSynchronizedProperties( type.BaseType, matchCallback );
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
