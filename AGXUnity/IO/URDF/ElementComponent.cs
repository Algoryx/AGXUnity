using UnityEngine;

namespace AGXUnity.IO.URDF
{
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#urdf-import" )]
  [AddComponentMenu("")]
  public class ElementComponent : ScriptComponent
  {
    public Element Element { get { return m_element; } private set { m_element = value; } }

    public void SetElement( Element element )
    {
      Element = element;
    }

    [SerializeField]
    private Element m_element = null;
  }
}
