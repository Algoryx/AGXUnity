using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
{
  [SerializeField]
  private List<TKey> m_keys = new List<TKey>();
  [SerializeField]
  private List<TValue> m_values = new List<TValue>();

  public void OnBeforeSerialize()
  {
    m_keys = new List<TKey>();
    m_values = new List<TValue>();

    foreach ( var (k, v) in this ) {
      m_keys.Add( k );
      m_values.Add( v );
    }
  }

  public void OnAfterDeserialize()
  {
    for ( int i = 0; i < m_keys.Count; i++ ) {
      var k = m_keys[i];
      var v = m_values[i];

      this[ k ] = v;
    }
  }
}
