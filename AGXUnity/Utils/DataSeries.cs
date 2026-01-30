using UnityEngine;

namespace AGXUnity.Utils
{
  [AddComponentMenu( "AGXUnity/Data Series" )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#plotting-and-data-acquisition" )]
  public class DataSeries : ScriptComponent
  {
    public agxPlot.DataSeries Native { get; private set; } = null;
    public string Name;
    public string Unit;

    protected override bool Initialize()
    {
      Native = new agxPlot.DataSeries( Name );
      Native.setUnit( Unit );

      return true;
    }

    public void Write( float value )
    {
      Native.push( value );
    }
  }
}
