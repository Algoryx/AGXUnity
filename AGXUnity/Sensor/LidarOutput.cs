using agxSensor;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.Sensor
{
  [Serializable]
  public class LidarOutput
  {
    public RtOutput Native { get; private set; } = null;

    [SerializeField]
    private List<RtOutput.Field> m_fields = new List<RtOutput.Field>();

    private uint m_outputID = 0; // Must be greater than 0 to be valid

    public bool Initialize( LidarSensor sensor )
    {
      if ( Native != null )
        return true;

      m_outputID = SensorEnvironment.Instance.GenerateOutputID();

      Native = new RtOutput();
      foreach ( var field in m_fields )
        Native.add( field );

      sensor.Native.getOutputHandler().add( m_outputID, Native );

      return true;
    }

    // TODO: Better handling of adding/removing output fields
    public void Add( RtOutput.Field field )
    {
      m_fields.Add( field );
    }

    public void Remove( RtOutput.Field field )
    {
      m_fields.Remove( field );
    }
  }
}
