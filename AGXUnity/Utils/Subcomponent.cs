using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.Util
{
  public interface INativeSynchronizer
  {
    internal void SynchronizeNative();
  }

  public static class PropertyUtil
  {
    public static void Assign<T>( ref T field, T value, INativeSynchronizer owner )
    {
      if ( EqualityComparer<T>.Default.Equals( field, value ) )
        return;
      field = value;
      owner.SynchronizeNative();
    }
  }

  public abstract class Subcomponent<ParentT> :
    INativeSynchronizer
    where ParentT : class
  {
    public ParentT Parent { get; private set; }

    internal bool Attach( ParentT parent )
    {
      if ( Parent != null && !ReferenceEquals( Parent, parent ) ) {
        Debug.LogError( "Subcomponents cannot be attached to multiple components" );
        return false;
      }

      Parent = parent;
      SynchronizeNative();
      return true;
    }

    internal void Disconnect()
    {
      Parent = default;
    }

    protected abstract void SynchronizeNative();

    void INativeSynchronizer.SynchronizeNative() => SynchronizeNative();
  }

  public abstract class Subcomponent<ParentT, T> :
    Subcomponent<ParentT>
    where ParentT : class
    where T : class
  {
    public T Native { get; private set; }

    internal bool Attach( ParentT parent, bool initializeNative = true )
    {
      if ( !base.Attach( parent ) )
        return false;

      if ( initializeNative && Native == null )
        Native = InitializeNative();
      if ( initializeNative && Native != null )
        SynchronizeNative();
      return !initializeNative || Native != null;
    }

    internal new void Disconnect()
    {
      DisposeNative();
      Native = default;
      base.Disconnect();
    }

    protected virtual void DisposeNative() { }

    protected abstract T InitializeNative();
  }
}
