using AGXUnity;
using System;
using UnityEngine;

namespace AGXUnityEditor.Utils
{
  public abstract class DirtyOnChangeScope : IDisposable
  {
    private UnityEngine.Object m_object;

    public DirtyOnChangeScope( UnityEngine.Object obj )
    {
      m_object = obj;
    }

    protected abstract bool Changed();

    public void Dispose()
    {
      if ( Changed() )
        UnityEditor.EditorUtility.SetDirty( m_object );
    }
  }

  public class DirtyOnLineChangeScope : DirtyOnChangeScope
  {
    private Line m_line;

    private Vector3 m_p1;
    private Quaternion m_r1;
    private Vector3 m_p2;
    private Quaternion m_r2;

    public DirtyOnLineChangeScope( UnityEngine.Object obj, Line line )
    : base( obj )
    {
      m_line = line;
      m_p1 = line.Start.LocalPosition;
      m_r1 = line.Start.LocalRotation;
      m_p2 = line.End.LocalPosition;
      m_r2 = line.End.LocalRotation;
    }

    protected override bool Changed()
    {
      return m_p1 != m_line.Start.LocalPosition ||
             m_r1 != m_line.Start.LocalRotation ||
             m_p2 != m_line.End.LocalPosition ||
             m_r2 != m_line.End.LocalRotation;
    }
  }
}
