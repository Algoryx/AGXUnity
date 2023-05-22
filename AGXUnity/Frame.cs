using System;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity
{
  [DoNotGenerateCustomEditor]
  [Obsolete("Use IFrame instead.")]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#frames" )]
  public class Frame : ScriptAsset
  {
    [SerializeField]
    private GameObject m_parent = null;
    [SerializeField]
    private Vector3 m_localPosition = Vector3.zero;
    [SerializeField]
    private Quaternion m_localRotation = Quaternion.identity;

    public void CopyTo( IFrame dest )
    {
      dest.LocalPosition = m_localPosition;
      dest.LocalRotation = m_localRotation;
      dest.SetParent( m_parent, false );
    }

    private Frame()
    {
    }

    public override void Destroy()
    {
      throw new NotImplementedException();
    }

    protected override void Construct()
    {
      throw new NotImplementedException();
    }

    protected override bool Initialize()
    {
      throw new NotImplementedException();
    }
  }
}
