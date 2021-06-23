using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AGXUnityEditor.Tools
{
  public class CustomTargetTool : Tool
  {
    public Object[] Targets { get; private set; }

    public bool HasInvalidTargets
    {
      get
      {
        foreach ( var target in Targets )
          if ( target == null )
            return true;
        return false;
      }
    }

    public IEnumerable<T> GetTargets<T>()
      where T : Object
    {
      foreach ( var target in Targets )
        yield return (T)target;
    }

    public IEnumerable<U> GetTargets<T, U>( Func<T, U> func )
      where U : Object
      where T : Object
    {
      foreach ( var obj in GetTargets<T>() )
        yield return func( obj );
    }

    public IEnumerable<T> CollectComponentsInChildred<T>()
      where T : Object
    {
      foreach ( var target in GetTargets<Component>() ) {
        if ( target == null )
          continue;
        var children = target.GetComponentsInChildren<T>();
        foreach ( var child in children )
          yield return child;
      }

      yield break;
    }

    public int NumTargets { get { return Targets.Length; } }

    public bool IsMultiSelect { get { return NumTargets > 1; } }

    public virtual void OnUndoRedo() { }

    protected CustomTargetTool( Object[] targets )
      : base( isSingleInstanceTool: false )
    {
      Targets = targets;
    }
  }
}
