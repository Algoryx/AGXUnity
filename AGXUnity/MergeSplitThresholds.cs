using System;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity
{
  [DoNotGenerateCustomEditor]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#merge-split-properties" )]
  public class MergeSplitThresholds<T> : ScriptAsset
    where T : MergeSplitThresholds<T>
  {
    private static T s_defaultResource;

    [HideInInspector]
    public static T DefaultResource
    {
      get
      {
        if ( s_defaultResource == null ) {
          s_defaultResource = CreateInstance<T>();
          s_defaultResource.hideFlags = HideFlags.NotEditable;
          s_defaultResource.name = "Default";
        }
        return s_defaultResource;
      }
    }

    [InvokableInInspector( "Reset to default" )]
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
