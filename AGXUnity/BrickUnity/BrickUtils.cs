using System.Collections.Generic;
using B_Component = Brick.Physics.Component;

namespace AGXUnity.BrickUnity
{
  public static class BrickUtils
  {
    public static B_Component LoadComponentFromFile(string filePath, string nodePath)
    {
      Brick.AgxBrick._BrickModule.Init();

      var loader = new Brick.AgxBrick.BrickFileLoader();
      var simWrapper = loader.LoadFile(filePath, nodePath);
      return simWrapper.Scene;
    }
  }

  public class BrickAGXUnityMap<TBRICK, TAGXUNITY> where TBRICK : Brick.IObject
                                         where TAGXUNITY : ScriptComponent
  {
    private readonly Dictionary<TBRICK, TAGXUNITY> _brickToAgx = new Dictionary<TBRICK, TAGXUNITY>();
    private readonly Dictionary<TAGXUNITY, TBRICK> _agxToBrick = new Dictionary<TAGXUNITY, TBRICK>();

    public void Add(TBRICK brickObject, TAGXUNITY agxObject)
    {
      _brickToAgx.Add(brickObject, agxObject);
      _agxToBrick.Add(agxObject, brickObject);
    }

    public TBRICK this[TAGXUNITY agxObject] => this._agxToBrick[agxObject];
    public TAGXUNITY this[TBRICK brickObject] => this._brickToAgx[brickObject];
  }
}