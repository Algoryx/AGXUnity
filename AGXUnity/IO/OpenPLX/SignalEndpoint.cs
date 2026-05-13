using System;
using UnityEngine;
using Input = openplx.Physics.Signals.Input;
using Output = openplx.Physics.Signals.Output;

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

    internal void Invalidate()
    {
      SignalRoot = null;
    }

    public bool IsValid => SignalRoot != null;
  }

  [Serializable]
  public class InputTarget : SignalEndpoint
  {
    public Input Native { get; private set; }

    public InputTarget( string name, Input input )
    {
      Name = name;
      Enabled = true;
      m_serializedType = new SerializedType( input.GetType() );
      ValueTypeCode = input.type();
    }

    internal override bool Initialize( OpenPLXSignals signalRoot )
    {
      base.Initialize( signalRoot );
      Native = signalRoot.InitializeNativeEndpoint( Name ) as Input;
      return Native != null;
    }
  }

  [Serializable]
  public class OutputSource : SignalEndpoint
  {
    public Output Native { get; private set; }

    public OutputSource( string name, Output output )
    {
      Name = name;
      Enabled = output.enabled();
      m_serializedType = new SerializedType( output.GetType() );
      ValueTypeCode = output.type();
    }

    internal override bool Initialize( OpenPLXSignals signalRoot )
    {
      base.Initialize( signalRoot );
      Native = signalRoot.InitializeNativeEndpoint( Name ) as Output;
      return Native != null;
    }
  }
}
