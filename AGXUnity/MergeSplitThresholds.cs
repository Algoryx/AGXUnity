using System;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity
{
  [DoNotGenerateCustomEditor]
  public class MergeSplitThresholds : ScriptAsset
  {
    [HideInInspector]
    public static string ResourceDirectory { get { return @"MergeSplit"; } }
    [HideInInspector]
    public static string AssetDirectory { get { return @"Assets/AGXUnity/Resources/MergeSplit"; } }

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
