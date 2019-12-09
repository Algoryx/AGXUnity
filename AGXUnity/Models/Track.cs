using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.Models
{
  public class Track : ScriptComponent
  {
    public agxVehicle.Track Native { get; private set; } = null;

    [SerializeField]
    private List<TrackWheel> m_wheels = new List<TrackWheel>();

    /// <summary>
    /// Registered track wheel instances.
    /// </summary>
    [HideInInspector]
    public TrackWheel[] Wheels
    {
      get { return m_wheels.ToArray(); }
    }

    /// <summary>
    /// Associate track wheel instance to this track.
    /// </summary>
    /// <param name="wheel">Track wheel instance to add.</param>
    /// <returns>True if added, false if null or already added.</returns>
    public bool Add( TrackWheel wheel )
    {
      if ( wheel == null || m_wheels.Contains( wheel ) )
        return false;

      m_wheels.Add( wheel );

      return true;
    }

    /// <summary>
    /// Disassociate track wheel instance from this track.
    /// </summary>
    /// <param name="wheel">Track wheel instance to remove.</param>
    /// <returns>True if removed, false if null or not associated to this track.</returns>
    public bool Remove( TrackWheel wheel )
    {
      if ( wheel == null )
        return false;

      return m_wheels.Remove( wheel );
    }

    /// <summary>
    /// True if <paramref name="wheel"/> is associated to this track.
    /// </summary>
    /// <param name="wheel">Track wheel instance.</param>
    /// <returns>True if <paramref name="wheel"/> is associated to this track.</returns>
    public bool Contains( TrackWheel wheel )
    {
      return m_wheels.Contains( wheel );
    }

    /// <summary>
    /// Verifies so that all added track wheels still exists. Wheels that
    /// has been deleted are removed.
    /// </summary>
    public void RemoveInvalidWheels()
    {
      m_wheels.RemoveAll( wheel => wheel == null );
    }

    protected override bool Initialize()
    {
      return true;
    }

    protected override void OnDestroy()
    {
      base.OnDestroy();
    }
  }
}
