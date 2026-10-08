using AGXUnity;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

using Assembly = System.Reflection.Assembly;

namespace AGXUnityTesting.Editor
{
  public class EditorMetadataTests
  {
    [Test]
    public void ScriptsHaveHelpURLs()
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


    [Test]
    public void ScriptsHaveExplicitAddComponentPath()
    {
      var asm = Assembly.GetAssembly(typeof(RigidBody));
      List<Type> missing = new List<Type>();
      foreach ( var t in asm.ExportedTypes ) {
        if (
          (
            t.IsSubclassOf( typeof( ScriptComponent ) )
          ) && !t.IsAbstract ) {
          var help = t.GetCustomAttribute<AddComponentMenu>(false);
          //var hide = t.GetCustomAttribute<HideInInspector>();
          if ( help == null /*&& hide == null*/ )
            missing.Add( t );
        }
      }

      if ( missing.Count > 0 ) {
        var errStr = $"The following classes are missing the AddComponent Attribute: \n";
        foreach ( var t in missing ) {
          errStr += t.Name + "\n";
        }
        Assert.Fail( errStr );
      }
    }

    private void CheckScriptIconsInDirectory( string path )
    {
      if ( System.IO.Directory.Exists( path ) ) {
        foreach ( var subfile in System.IO.Directory.EnumerateFiles( path ) )
          CheckScriptIconsInDirectory( subfile );
      }
      else if ( path.EndsWith( ".cs" ) ) {
        var mi = AssetImporter.GetAtPath( path ) as MonoImporter;
        Assert.NotNull( mi.GetIcon(), $"Script file '{path}' has no icon" );
      }

    }

    [Test]
    public void ScriptsHaveIcons()
    {
      var sourceDir = AGXUnityEditor.IO.Utils.AGXUnitySourceDirectory;
      CheckScriptIconsInDirectory( sourceDir );
    }
  }
}
