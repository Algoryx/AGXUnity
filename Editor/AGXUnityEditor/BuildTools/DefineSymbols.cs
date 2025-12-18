using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace AGXUnityEditor.Build
{
  public static class DefineSymbols
  {
    public static readonly string ON_AGXUNITY_UPDATE = "AGXUNITY_UPDATING";

    #region Default API
    public static void Add( string symbol )
    {
#if UNITY_2021_2_OR_NEWER
      var target = NamedBuildTarget.Standalone;
#else
      var target = BuildTargetGroup.Standalone;
#endif
      Add( symbol, target );
    }

    public static void Remove( string symbol )
    {
#if UNITY_2021_2_OR_NEWER
      var target = NamedBuildTarget.Standalone;
#else
      var target = BuildTargetGroup.Standalone;
#endif
      Remove( symbol, target );
    }

    public static bool Contains( string symbol )
    {
#if UNITY_2021_2_OR_NEWER
      var target = NamedBuildTarget.Standalone;
#else
      var target = BuildTargetGroup.Standalone;
#endif
      return Contains( symbol, target );
    }
    #endregion

    #region NamedBuildTarget API
    public static void Add( string symbol, NamedBuildTarget target )
    {
      if ( Contains( symbol, target ) )
        return;

      var symbolsList = GetSymbolsList( target );
      symbolsList.Add( symbol );
      SetSymbols( symbolsList, target );
    }

    public static void Remove( string symbol, NamedBuildTarget target )
    {
      if ( !Contains( symbol, target ) )
        return;

      var symbolsList = GetSymbolsList( target );
      symbolsList.Remove( symbol );
      SetSymbols( symbolsList, target );
    }

    public static bool Contains( string symbol, NamedBuildTarget target )
    {
      return PlayerSettings.GetScriptingDefineSymbols( target ).Split( ';' ).Any( defineSymbol => defineSymbol == symbol );
    }

    private static List<string> GetSymbolsList( NamedBuildTarget target )
    {
      return PlayerSettings.GetScriptingDefineSymbols( target ).Split( ';' ).ToList();
    }

    private static void SetSymbols( List<string> symbols, NamedBuildTarget target )
    {
      var symbolsToSet = symbols.Count == 0 ?
                           "" :
                         symbols.Count == 1 ?
                           symbols[ 0 ] :
                           string.Join( ";", symbols );
      PlayerSettings.SetScriptingDefineSymbols( target, symbolsToSet );
      AssetDatabase.SaveAssets();
    }
    #endregion

    #region BuildTargetGroup API
#if UNITY_6000_0_OR_NEWER
    [System.Obsolete( "The BuildTargetGroup API has been deprecated in favor of the NamedBuildTarget API as of Unity 6.0" )]
#endif
    public static void Add( string symbol, BuildTargetGroup group )
    {
      if ( Contains( symbol, group ) )
        return;

      var symbolsList = GetSymbolsList( group );
      symbolsList.Add( symbol );
      SetSymbols( symbolsList, group );
    }

#if UNITY_6000_0_OR_NEWER
    [System.Obsolete( "The BuildTargetGroup API has been deprecated in favor of the NamedBuildTarget API as of Unity 6.0" )]
#endif
    public static void Remove( string symbol, BuildTargetGroup group )
    {
      if ( !Contains( symbol, group ) )
        return;

      var symbolsList = GetSymbolsList( group );
      symbolsList.Remove( symbol );
      SetSymbols( symbolsList, group );
    }

#if UNITY_6000_0_OR_NEWER
    [System.Obsolete( "The BuildTargetGroup API has been deprecated in favor of the NamedBuildTarget API as of Unity 6.0" )]
#endif
    public static bool Contains( string symbol, BuildTargetGroup group )
    {
      return PlayerSettings.GetScriptingDefineSymbolsForGroup( group ).Split( ';' ).Any( defineSymbol => defineSymbol == symbol );
    }

#if UNITY_6000_0_OR_NEWER
    [System.Obsolete( "The BuildTargetGroup API has been deprecated in favor of the NamedBuildTarget API as of Unity 6.0" )]
#endif
    private static List<string> GetSymbolsList( BuildTargetGroup group )
    {
      return PlayerSettings.GetScriptingDefineSymbolsForGroup( group ).Split( ';' ).ToList();
    }

#if UNITY_6000_0_OR_NEWER
    [System.Obsolete( "The BuildTargetGroup API has been deprecated in favor of the NamedBuildTarget API as of Unity 6.0" )]
#endif
    private static void SetSymbols( List<string> symbols, BuildTargetGroup group )
    {
      var symbolsToSet = symbols.Count == 0 ?
                           "" :
                         symbols.Count == 1 ?
                           symbols[ 0 ] :
                           string.Join( ";", symbols );
      PlayerSettings.SetScriptingDefineSymbolsForGroup( group, symbolsToSet );
      AssetDatabase.SaveAssets();
    }
    #endregion
  }
}
