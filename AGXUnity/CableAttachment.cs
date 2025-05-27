using System;
using UnityEngine;

namespace AGXUnity
{
  [Serializable]
  public class CableAttachment : IFrame
  {
    public enum AttachmentType
    {
      Unknown,
      Rigid,
      Ball
    }

    public static CableAttachment Create( AttachmentType attachmentType,
                                          GameObject parent = null,
                                          Vector3 localPosition = default( Vector3 ),
                                          Quaternion localRotation = default( Quaternion ) )
    {
      var attachment = Create<CableAttachment>( parent, localPosition, localRotation );
      attachment.Type = attachmentType;

      return attachment;
    }

    [field: SerializeField]
    public bool IgnoreNodeRotation { get; set; } = true;

    [SerializeField]
    private AttachmentType m_type = AttachmentType.Unknown;

    public AttachmentType Type
    {
      get { return m_type; }
      set { m_type = value; }
    }
  }
}
