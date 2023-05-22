using System;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity
{
  [DoNotGenerateCustomEditor]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#merge-split-properties" )]
  public class MergeSplitThresholds : ScriptAsset
  {
    [HideInInspector]
    public static string ResourceDirectory { get { return @"MergeSplit"; } }

    [InvokableInInspector("Reset to default")]
    public void OnResetToDefault()
    {
      ResetToDefault();
    }

    public virtual void ResetToDefault()
    {
      Debug.LogWarning( "Reset to default not implemented." );
    }

    public override void Destroy()
    {
    }

    protected override void Construct()
    {
    }

    protected override bool Initialize()
    {
      return true;
    }
  }
}
