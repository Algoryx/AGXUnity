using System;
using System.Collections.Generic;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity
{
  public class CableDamage : ScriptComponent
  {
    /// <summary>
    /// Native instance of the cable damage.
    /// </summary>
    public agxCable.CableDamage Native { get; private set; }


    //TODO
    //public void RestoreLocalDataFrom( agx.RigidBody native )
  }
}