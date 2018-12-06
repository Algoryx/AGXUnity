using System;
using UnityEngine;

using Object = UnityEngine.Object;

namespace AGXUnityEditor.Tools
{
  public class CustomTargetTool : Tool
  {
    public Object Target { get; private set; }

    protected CustomTargetTool( Object target )
    {
      Target = target;
    }
  }
}
