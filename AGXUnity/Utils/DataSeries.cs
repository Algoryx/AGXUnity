using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AGXUnity;

namespace AGXUnity.Utils
{
  public class DataSeries : ScriptComponent
  {
    private agxPlot.DataSeries m_dataSeries;
    public agxPlot.DataSeries Native { get { return m_dataSeries; } }
    public string Name;
    public string Unit;

    protected override bool Initialize()
    {
      m_dataSeries = new agxPlot.DataSeries(Name);
      m_dataSeries.setUnit(Unit);

      return true;
    }

    public void Write(float value)
    {
      m_dataSeries.push(value);
    }
  }
}