using System.Linq;
using System.Collections.Generic;

using UnityEditor;

namespace AGXUnityEditor.Build
{
  public static class DefineSymbols
  {
    public static readonly string ON_AGXUNITY_UPDATE = "AGXUNITY_UPDATING";

    public static void Add( string symbol, BuildTargetGroup group = BuildTargetGroup.Standalone )
    {
      if ( Contains( symbol, group ) )
        return;

      var symbolsList = GetSymbolsList( group );
      symbolsList.Add( symbol );
      SetSymbols( symbolsList, group );
    }

    public static void Remove( string symbol, BuildTargetGroup group = BuildTargetGroup.Standalone )
    {
      if ( !Contains( symbol, group ) )
        return;

      var symbolsList = GetSymbolsList( group );
      symbolsList.Remove( symbol );
      SetSymbols( symbolsList, group );
    }

    public static bool Contains( string symbol, BuildTargetGroup group = BuildTargetGroup.Standalone )
    {
      return PlayerSettings.GetScriptingDefineSymbolsForGroup( group ).Split( ';' ).Any( defineSymbol => defineSymbol == symbol );
    }

    private static List<string> GetSymbolsList( BuildTargetGroup group )
    {
      return PlayerSettings.GetScriptingDefineSymbolsForGroup( group ).Split( ';' ).ToList();
    }

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
  }
}
