using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.IO
{
  public class RestoredAssetsRoot : ScriptableObject
  {
    public enum ContainingType
    {
      ShapeMaterial,
      ContactMaterial,
      FrictionModel,
      CableProperties,
      RenderMaterial,
      Mesh,
      Unknown // This is last - add entries before this.
    }

    public static RestoredAssetsRoot Create( string prefixName, ContainingType type )
    {
      var instance = ScriptableObject.CreateInstance<RestoredAssetsRoot>();
      instance.Type = type;
      instance.name = FindName( prefixName, type );

      return instance;
    }

    public static string FindName( string prefixName, ContainingType type )
    {
      return $"{prefixName}_{type}";
    }

    public static ContainingType FindAssetType( System.Type type )
    {
      var index = System.Array.IndexOf( s_fileAssetTypeToTypeMap, type );
      return index < 0 ? ContainingType.Unknown : (ContainingType)index;
    }

    public static ContainingType FindAssetType<T>()
    {
      return FindAssetType( typeof( T ) );
    }

    public static int FindAssetTypeIndex<T>()
    {
      return (int)FindAssetType<T>();
    }

    public static IEnumerable<ContainingType> Types
    {
      get
      {
        foreach ( ContainingType type in System.Enum.GetValues( typeof( ContainingType ) ) )
          yield return type;
      }
    }

    public static System.Type GetType( ContainingType type )
    {
      return s_fileAssetTypeToTypeMap[ (int)type ];
    }

    [SerializeField]
    private ContainingType m_type = ContainingType.Unknown;

    [HideInInspector]
    public ContainingType Type
    {
      get { return m_type; }
      private set { m_type = value; }
    }

    private static System.Type[] s_fileAssetTypeToTypeMap = new System.Type[]
    {
      typeof( ShapeMaterial ),
      typeof( ContactMaterial ),
      typeof( FrictionModel ),
      typeof( CableProperties ),
      typeof( Material ),
      typeof( Mesh ),
      null // Unknown assets, the only one valid to be null.
    };
  }
}
