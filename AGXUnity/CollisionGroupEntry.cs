using System;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity
{
  /// <summary>
  /// Collision group identifier containing a name tag.
  /// </summary>
  [Serializable]
  public class CollisionGroupEntry
  {
    /// <summary>
    /// Flag if this component should affect all children. E.g., add this
    /// component to a game object which contains several rigid bodies as
    /// children - all shapes and bodies will inherit the collision groups.
    /// 
    /// If false, this component will check for compatible components to
    /// affect on the same level as this.
    /// </summary>
    /// <remarks>
    /// It's not possible to change this property during runtime.
    /// </remarks>
    [SerializeField]
    private bool m_propagateToChildren = false;

    /// <summary>
    /// Flag if this component should affect all children. E.g., add this
    /// component to a game object which contains several rigid bodies as
    /// children - all shapes and bodies will inherit the collision groups.
    /// 
    /// If false, this component will check for compatible components to
    /// affect on the same level as this.
    /// </summary>
    /// <remarks>
    /// It's not possible to change this property during runtime.
    /// </remarks>
    public bool PropagateToChildren
    {
      get { return m_propagateToChildren; }
      set { m_propagateToChildren = value; }
    }

    [SerializeField]
    private string m_tag = "";
    public string Tag
    {
      get { return m_tag; }
      set { m_tag = value; }
    }

    /// <summary>
    /// If <paramref name="obj"/> has method "addGroup( UInt32 )" this method
    /// converts the name tag to an UInt32 using 32 bit FNV1 hash.
    /// </summary>
    /// <param name="obj">Object to execute addGroup on.</param>
    public void AddTo( object obj )
    {
      InvokeIdMethod( "addGroup", obj );
    }

    public void RemoveFrom( object obj )
    {
      InvokeIdMethod( "removeGroup", obj );
    }

    private void InvokeIdMethod( string method, object obj )
    {
      if ( obj == null )
        return;

      var m = obj.GetType().GetMethod( method, new Type[] { typeof( UInt32 ) } );
      if ( m == null )
        throw new Exception( "Method " + method + " not found in type: " + obj.GetType().FullName );
      m.Invoke( obj, new object[] { Tag.To32BitFnv1aHash() } );
    }
  }
}
