using AGXUnity;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

using Assembly = System.Reflection.Assembly;

namespace AGXUnityTesting
{
  public class HelpURLTest
  {
    [Test]
    public void HelpURLTestSimplePasses()
    {
      var asm = Assembly.GetAssembly(typeof(RigidBody));
      List<Type> missing = new List<Type>();
      foreach ( var t in asm.ExportedTypes ) {
        if (
          (
            t.IsSubclassOf( typeof( ScriptComponent ) ) ||
            t.IsSubclassOf( typeof( ScriptAsset ) )
          ) && !t.IsAbstract ) {
          var help = t.GetCustomAttribute<HelpURLAttribute>(false);
          var hide = t.GetCustomAttribute<HideInInspector>();
          if ( help == null && hide == null )
            missing.Add( t );
        }
      }

      if ( missing.Count > 0 ) {
        var errStr = $"The following classes are missing the HelpURL Attribute: \n";
        foreach ( var t in missing ) {
          errStr += t.Name + "\n";
        }
        Assert.Fail( errStr );
      }
    }
  }
}
