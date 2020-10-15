using System.Xml.Linq;
using UnityEngine;

namespace AGXUnity.IO.URDF
{
  public class Pose : Element
  {
    public Pose Origin { get { return this; } }
    public Vector3 Xyz { get; private set; } = Vector3.zero;
    public Vector3 Rpy { get; private set; } = Vector3.zero;

    public override void Read( XElement element, bool optional = true )
    {
      base.Read( element, optional );
      Xyz = Utils.ReadVector3( element?.Element( "origin" ), "xyz" );
      Rpy = Utils.ReadVector3( element?.Element( "origin" ), "rpy" );
    }
  }
}
