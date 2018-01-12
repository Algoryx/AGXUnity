using System;
using UnityEngine;

namespace AGXUnity
{
  /// <summary>
  /// Deprecated - use AttachmentPair instead.
  /// </summary>
  [DoNotGenerateCustomEditor]
  public class ConstraintAttachmentPair : ScriptAsset
  {
#pragma warning disable 0618
    [SerializeField]
    private Frame m_referenceFrame = null;
    [SerializeField]
    private Frame m_connectedFrame = null;
#pragma warning restore 0618
    [SerializeField]
    private bool m_synchronized = true;

    /// <summary>
    /// Copies restored data to new type AttachmentPair.
    /// </summary>
    /// <param name="dest">Destination.</param>
    public void CopyTo( AttachmentPair dest )
    {
      if ( m_referenceFrame != null )
        m_referenceFrame.CopyTo( dest.ReferenceFrame );
      if ( m_connectedFrame != null )
        m_connectedFrame.CopyTo( dest.ConnectedFrame );
      dest.Synchronized = m_synchronized;
    }

    private ConstraintAttachmentPair()
    {
    }

    protected override void Construct()
    {
    }

    protected override bool Initialize()
    {
      return true;
    }

    public override void Destroy()
    {
    }
  }
}
