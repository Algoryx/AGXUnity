using UnityEngine;

namespace AGXUnity.Utils
{
  [AddComponentMenu( "" )]
  [HideInInspector]
  [DisallowMultipleComponent]
  public class OnSelectionProxy : ScriptComponent
  {
    [SerializeField]
    private ScriptComponent m_component = null;

    public GameObject Target
    {
      get { return m_component != null ? m_component.gameObject : null; }
    }

    public ScriptComponent Component
    {
      get { return m_component; }
      set { m_component = value; }
    }

    protected virtual void Reset()
    {
      hideFlags |= HideFlags.HideInInspector;
    }
  }
}
