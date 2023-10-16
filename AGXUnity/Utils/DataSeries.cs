using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AGXUnity;

namespace AGXUnity.Utils
{
  public class DataSeries : ScriptComponent
  {
    public agxPlot.DataSeries Native { get; private set; } = null;
    public string Name;
    public string Unit;

    protected override bool Initialize()
    {
      Native = new agxPlot.DataSeries(Name);
      Native.setUnit(Unit);

      return true;
    }

    public void Write(float value)
    {
      Native.push(value);
    }
  }
}