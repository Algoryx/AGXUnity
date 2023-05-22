using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace AGXUnity.IO
{
  public class UuidComparer : IEqualityComparer<agx.Uuid>
  {
    public bool Equals( agx.Uuid id1, agx.Uuid id2 )
    {
      return id1.EqualWith( id2 );
    }

    public int GetHashCode( agx.Uuid id )
    {
      return id.str().GetHashCode();
    }
  }

  [AddComponentMenu( "" )]
  [DisallowMultipleComponent]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#agx-dynamics-import" )]
  public class Uuid : ScriptComponent
  {
    [SerializeField]
    private string m_str = string.Empty;

    public string Str { get { return m_str; } }

    public agx.Uuid Native
    {
      get { return new agx.Uuid( m_str ); }
      set { m_str = value.str(); }
    }

    public Uuid( agx.Uuid uuid )
    {
      m_str = uuid.str();
    }
  }
}
