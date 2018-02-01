using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AGXUnityEditor.IO
{
  public class AGXUnityScriptData
  {
    public static Dictionary<string, AGXUnityScriptData> CollectAll()
    {
      Dictionary<string, AGXUnityScriptData> nameScriptMap = ( from script
                                                               in Resources.FindObjectsOfTypeAll<MonoScript>()
                                                               where script.GetClass() != null &&
                                                                     script.GetClass().FullName.StartsWith( "AGXUnity." )
                                                                select script ).ToDictionary( script => script.GetClass().FullName,
                                                                                              script => new AGXUnityScriptData( script ) );
      return nameScriptMap;
    }

    public MonoScript Script { get; private set; }
    public int FileId { get; private set; }
    public string Guid { get; private set; }
    public string FullName { get { return Script.GetClass().FullName; } }

    public AGXUnityScriptData( MonoScript script )
    {
      Script = script;
      FileId = 11500000;
      Guid = AssetDatabase.AssetPathToGUID( AssetDatabase.GetAssetPath( script ) );
    }
  }
}
