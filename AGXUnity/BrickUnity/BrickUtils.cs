using UnityEngine;
using B_Component = Brick.Physics.Component;

namespace AGXUnity.BrickUnity
{
  public static class BrickUtils
  {
    public static B_Component LoadComponentFromFile(string filePath, string nodePath)
    {
      SetupBrickEnvironment();

      Brick.AGXBrick._BrickModule.Init();
      Brick.Model.MarkDirtyModels(true);

      Brick.Model.MarkDirtyModels(true);
      var loader = new Brick.AGXBrick.BrickFileLoader();
      var simWrapper = loader.LoadFile(filePath, nodePath);
      return simWrapper.Scene;
    }

    public static void SetupBrickEnvironment()
    {
      if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("BRICK_DIR")) && string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("BRICK_MODULEPATH")))
      {
        if (Application.isEditor)
        {
          var modulePath = System.IO.Path.Combine(Application.dataPath, "AGXUnity", "Plugins", "x86_64", "Brick", "modules");
          System.Environment.SetEnvironmentVariable("BRICK_MODULEPATH", modulePath);
        }
        else
        {
          var modulePath = System.IO.Path.Combine(Application.dataPath, "brick", "modules");
          System.Environment.SetEnvironmentVariable("BRICK_MODULEPATH", modulePath);
        }
      }
    }

  }
}