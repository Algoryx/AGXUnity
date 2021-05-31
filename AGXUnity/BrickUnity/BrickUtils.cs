using UnityEngine;
using B_Component = Brick.Physics.Component;

namespace AGXUnity.BrickUnity
{
  public static class BrickUtils
  {
    public static B_Component LoadComponentFromFile(string filePath, string nodePath)
    {
      SetupBrickEnvironment();

      Brick.AgxBrick._BrickModule.Init();
      Brick.Model.MarkDirtyModels(true);

      Brick.Model.MarkDirtyModels(true);
      var loader = new Brick.AgxBrick.BrickFileLoader();
      var simWrapper = loader.LoadFile(filePath, nodePath);
      return simWrapper.Scene;
    }

    public static void SetupBrickEnvironment()
    {
      if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("BRICK_DIR")) && string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("BRICK_MODULEPATH")))
      {
        var modulePath = System.IO.Path.Combine(Application.dataPath, "Plugins", "x86_64", "Brick", "modules");
        System.Environment.SetEnvironmentVariable("BRICK_MODULEPATH", modulePath);
      }
    }

  }
}