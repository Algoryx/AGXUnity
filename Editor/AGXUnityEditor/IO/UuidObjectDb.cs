using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace AGXUnityEditor.IO
{
  public class UuidObjectDb
  {
    private Dictionary<agx.Uuid, GameObject> m_gameObjects = new Dictionary<agx.Uuid, GameObject>( new AGXUnity.IO.UuidComparer() );

    public UuidObjectDb( AGXFileInfo fileInfo )
    {
      if ( fileInfo.PrefabInstance == null )
        return;

      var uuidGameObjects = fileInfo.PrefabInstance.GetComponentsInChildren<AGXUnity.IO.Uuid>();
      foreach ( var uuidComponent in uuidGameObjects )
        if ( !m_gameObjects.ContainsKey( uuidComponent.Native ) )
          m_gameObjects.Add( uuidComponent.Native, uuidComponent.gameObject );
    }

    public GameObject GetGameObject( agx.Uuid uuid )
    {
      GameObject go;
      if ( m_gameObjects.TryGetValue( uuid, out go ) )
        return go;
      return null;
    }
  }
}
