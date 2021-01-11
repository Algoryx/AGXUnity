using System;
using UnityEditor;

namespace AGXUnityEditor.Utils
{
  public class UndoCollapseBlock : IDisposable
  {
    public UndoCollapseBlock( string undoGroupName )
    {
      Undo.SetCurrentGroupName( undoGroupName );
      m_undoIndex = Undo.GetCurrentGroup();
    }

    public void Dispose()
    {
      Undo.CollapseUndoOperations( m_undoIndex );
    }

    private int m_undoIndex = -1;
  }
}
