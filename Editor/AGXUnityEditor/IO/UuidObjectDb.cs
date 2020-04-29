using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AGXUnityEditor.IO
{
  public class UuidObjectDb
  {
    public UuidObjectDb( AGXFileInfo fileInfo )
    {
      if ( fileInfo.PrefabInstance == null )
        return;

      var uuidGameObjects = fileInfo.PrefabInstance.GetComponentsInChildren<AGXUnity.IO.Uuid>();
      foreach ( var uuidComponent in uuidGameObjects )
        if ( !m_gameObjects.ContainsKey( uuidComponent.Native ) )
          m_gameObjects.Add( uuidComponent.Native,
                             new DbData()
                             {
                               GameObject = uuidComponent.gameObject
                             } );
    }

    public GameObject GetOrCreateGameObject( agx.Uuid uuid )
    {
      DbData data;
      if ( m_gameObjects.TryGetValue( uuid, out data ) ) {
        data.RefCount += 1;
        return data.GameObject;
      }

      data = new DbData() { GameObject = new GameObject(), RefCount = 1 };
      data.GameObject.AddComponent<AGXUnity.IO.Uuid>().Native = uuid;
      
      m_gameObjects.Add( uuid, data );

      return data.GameObject;
    }

    public void Ref( agx.Uuid uuid )
    {
      if ( !m_gameObjects.ContainsKey( uuid ) ) {
        Debug.LogWarning( $"Unable to reference object with UUID: {uuid}" );
        return;
      }

      m_gameObjects[ uuid ].RefCount += 1;
    }

    public GameObject[] GetUnreferencedGameObjects()
    {
      return ( from uuidData in m_gameObjects
               where uuidData.Value.RefCount < 1
               select uuidData.Value.GameObject ).ToArray();
    }

    private class DbData
    {
      public GameObject GameObject = null;
      public int RefCount = 0;
    }

    private Dictionary<agx.Uuid, DbData> m_gameObjects = new Dictionary<agx.Uuid, DbData>( new AGXUnity.IO.UuidComparer() );
  }
}
