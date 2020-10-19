using UnityEngine;

namespace AGXUnity.IO.URDF
{
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
