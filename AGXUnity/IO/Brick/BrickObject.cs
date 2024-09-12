using AGXUnity.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AGXUnity.IO.BrickIO
{
  public class BrickObject : MonoBehaviour
  {
    [field: SerializeField]
    public List<string> SourceDeclarations { get; private set; } = new List<string>();

    public static GameObject CreateGameObject( string name )
    {
      GameObject go = new GameObject( );
      RegisterGameObject( name, go );

      return go;
    }

    public static void RegisterGameObject( string name, GameObject go, bool overrideName = false )
    {

      var bo = go.GetOrCreateComponent<BrickObject>();
      if(bo.SourceDeclarations.Count == 0 || overrideName ) {
        var nameShort = name.Split('.').Last();
        go.name = nameShort;
      }
      bo.SourceDeclarations.Add(name);
    }
  }
}
