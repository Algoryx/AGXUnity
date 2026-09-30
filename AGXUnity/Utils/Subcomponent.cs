using System;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.Util
{
  public interface INativeSynchronizer
  {
    public void SynchronizeNative();
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
    [field: NonSerialized]
    public ParentT Parent { get; private set; }

    internal bool Bind( ParentT parent )
    {
      if ( Parent != null ) {
        if ( !ReferenceEquals( Parent, parent ) ) {
          Debug.LogError( "Subcomponents cannot be attached to multiple components" );
          return false;
        }
        return true;
      }

      Parent = parent;
      NativeSync();
      return true;
    }

    protected abstract void NativeSync();

    public void SynchronizeNative() => NativeSync();
  }

  public abstract class Subcomponent<ParentT, T> :
    Subcomponent<ParentT>
    where ParentT : class
    where T : class
  {
    [field: NonSerialized]
    public T Native { get; private set; }

    internal bool Initialize( ParentT parent )
    {
      if ( !Bind( parent ) )
        return false;

      if ( Native != null ) {
        Debug.LogWarning( "Reinitializing subcomponents is not allowed" );
        return false;
      }

      Native = InitializeNative();

      if ( Native == null ) {
        Debug.LogError( "Failed to initialize subcomponent" );
        return false;
      }

      NativeSync();
      return true;
    }

    internal void Disconnect()
    {
      DisposeNative();
      Native = default;
    }

    protected virtual void DisposeNative() { }

    protected abstract T InitializeNative();
  }
}
