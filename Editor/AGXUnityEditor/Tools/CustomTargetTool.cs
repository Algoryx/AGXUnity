using System;
using UnityEngine;

namespace AGXUnityEditor.Tools
{
  public class CustomTargetTool : Tool
  {
    public object Target { get; private set; }

    protected CustomTargetTool( object target )
    {
      Target = target;
    }
  }
}
