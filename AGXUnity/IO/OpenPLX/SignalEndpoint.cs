using System;
using UnityEngine;

namespace AGXUnity.IO.OpenPLX
{
  [Serializable]
  public class SerializedType : ISerializationCallbackReceiver
  {
    public Type Type;

    [SerializeField]
    private string m_serializedType;

    public SerializedType( Type type )
    {
      Type = type;
    }

    public void OnAfterDeserialize()
    {
      if ( m_serializedType != null )
        Type = Type.GetType( m_serializedType );
    }

    public void OnBeforeSerialize()
    {
      if ( Type == null )
        m_serializedType = null;
      else
        m_serializedType = Type.AssemblyQualifiedName;
    }
  }

  [Serializable]
  public class SignalEndpoint
  {
    [field: SerializeField]
    public string Name { get; protected set; }

    [field: SerializeField]
    public bool Enabled { get; protected set; }

    [field: SerializeField]
    public long ValueTypeCode { get; protected set; }

    [SerializeField]
    protected SerializedType m_serializedType;

    public Type Type => m_serializedType.Type;

    [field: NonSerialized]
    public OpenPLXSignals SignalRoot { get; private set; }

    public virtual bool IsValueTypeCompatible<T>() => false;

    virtual internal bool Initialize( OpenPLXSignals signalRoot )
    {
      SignalRoot = signalRoot;
      return true;
    }
  }
}
