using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace AGXUnityEditor
{
  [InitializeOnLoad]
  public static class ContextManager
  {
    static Dictionary<Type,Type> m_cachedContextTypeMap = new Dictionary<Type,Type>();

    public static void RegisterCustomContextAssembly( string assemblyName )
    {
      if ( !m_assembliesWithCustomContexts.Contains( assemblyName ) )
        m_assembliesWithCustomContexts.Add( assemblyName );
    }

    public static Type GetCustomContextForType( Type targetType )
    {
      if ( m_cachedContextTypeMap.TryGetValue( targetType, out var cachedCtx ) )
        return cachedCtx;

      Type[] types = null;
      try {
        types = m_assembliesWithCustomContexts.SelectMany( name => Assembly.Load( name ).GetTypes() ).ToArray();
      }
      catch ( Exception e ) {
        Debug.LogException( e );
        Debug.LogError( "Failed loading custom context assemblies." );
        types = Assembly.Load( Manager.AGXUnityEditorAssemblyName ).GetTypes();
      }

      var customCtxTypes = new List<Type>();
      foreach ( var type in types ) {
        // CustomTool attribute can only be used with tools
        // inheriting from CustomTargetTool.
        if ( !typeof( EditorToolContext ).IsAssignableFrom( type ) )
          continue;

        var customCtxAttribute = type.GetCustomAttributes<CustomContextAttribute>( false );
        if ( customCtxAttribute == null )
          continue;

        // Exact match - break search.
        if ( customCtxAttribute.Any( attr => attr.Type == targetType ) ) {
          customCtxTypes.Clear();
          customCtxTypes.Add( type );
          break;
        }

        // Type of custom tool desired type is assignable from current
        // target type. Store this if an exact match comes later.
        // E.g.: CustomTool( typeof( Shape ) ) and CustomTool( typeof( Box ) ).
        else if ( customCtxAttribute.Any( attr => attr.Type.IsAssignableFrom( targetType ) ) )
          customCtxTypes.Add( type );
      }

      var customCtxType = customCtxTypes.FirstOrDefault();
      if ( customCtxType != null )
        m_cachedContextTypeMap.Add( targetType, customCtxType );

      return customCtxType;
    }

    private static List<string> m_assembliesWithCustomContexts = new List<string>() { Manager.AGXUnityEditorAssemblyName };
  }
}
