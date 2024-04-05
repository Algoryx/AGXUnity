using System.Linq;
using UnityEngine;

namespace AGXUnity.IO.BrickIO
{
  [Icon( "Assets/Brick/brick-icon.png" )]
  public class BrickObject : MonoBehaviour
  {
    [field: SerializeField]
    public string SourceDeclaration { get; private set; }

    public static GameObject CreateGameObject( string name )
    {
      GameObject go = new GameObject( );
      RegisterGameObject( name, go );

      return go;
    }

    public static void RegisterGameObject( string name, GameObject go )
    {
      var nameShort = name.Split('.').Last();
      go.name = nameShort;

      var bo = go.AddComponent<BrickObject>();
      bo.SourceDeclaration = name;
    }
  }
}
