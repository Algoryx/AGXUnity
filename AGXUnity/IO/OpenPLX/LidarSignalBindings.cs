using System;
using System.Collections.Generic;
using LidarOutput = openplx.Sensors.Signals.LidarOutput;

namespace AGXUnity.IO.OpenPLX
{
  internal sealed class LidarSignalBindings : IDisposable
  {
    private readonly agxopenplx.AgxMetadata m_metadata;
    private readonly Dictionary<string, Binding> m_bindings = new Dictionary<string, Binding>();
    private bool m_disposed;

    public LidarSignalBindings( agxopenplx.AgxMetadata metadata )
    {
      m_metadata = metadata ?? throw new ArgumentNullException( nameof( metadata ) );
    }

    public int Count => m_bindings.Count;

    public agxSensor.RtOutput Register( LidarOutput definition, agxSensor.Lidar sensor, uint outputId )
    {
      if ( m_disposed )
        throw new ObjectDisposedException( nameof( LidarSignalBindings ) );
      if ( definition == null )
        throw new ArgumentNullException( nameof( definition ) );
      if ( sensor == null || agxSensor.Lidar.getCPtr( sensor ).Handle == IntPtr.Zero )
        throw new ArgumentException( "The lidar must have an initialized native sensor.", nameof( sensor ) );
      var uuid = definition.getUuid();
      if ( string.IsNullOrEmpty( uuid ) )
        throw new ArgumentException( "The lidar output must have an OpenPLX UUID.", nameof( definition ) );
      if ( m_bindings.TryGetValue( uuid, out var existing ) ) {
        if ( existing.Sensor != sensor )
          throw new InvalidOperationException( $"Lidar output '{definition.getName()}' is already bound to another sensor." );
        return existing.Output;
      }

      // Validate before allocating or changing the native sensor graph.
      var fields = CreateFields( definition );
      var binding = new Binding( m_metadata, definition, sensor, outputId, fields );
      m_bindings.Add( uuid, binding );
      return binding.Output;
    }

    public static agxSensor.RtOutput.Field[] CreateFields( LidarOutput definition )
    {
      if ( definition == null )
        throw new ArgumentNullException( nameof( definition ) );
      using ( var constants = new openplx.Sensors.Signals.LidarOutputField() )
      using ( var requested = definition.fields() ) {
        if ( requested.Count == 0 )
          throw new ArgumentException( $"Lidar output '{definition.getName()}' must select at least one field." );
        var mapping = new Dictionary<long, agxSensor.RtOutput.Field> {
          { constants.Position3D_F32(), agxSensor.RtOutput.Field.XYZ_VEC3_F32 },
          { constants.TimeStamp_F64(), agxSensor.RtOutput.Field.TIME_STAMP_F64 },
          { constants.Distance_F32(), agxSensor.RtOutput.Field.DISTANCE_F32 },
          { constants.RayPose3x4_F32(), agxSensor.RtOutput.Field.RAY_POSE_MAT3x4_F32 },
          { constants.IsHit_I32(), agxSensor.RtOutput.Field.IS_HIT_I32 },
          { constants.Intensity_F32(), agxSensor.RtOutput.Field.INTENSITY_F32 },
          { constants.EntityId_I32(), agxSensor.RtOutput.Field.ENTITY_ID_I32 }
        };
        var result = new agxSensor.RtOutput.Field[ requested.Count ];
        for ( int i = 0; i < result.Length; ++i )
          if ( !mapping.TryGetValue( requested[ i ], out result[ i ] ) )
            throw new NotSupportedException( $"Lidar output '{definition.getName()}' selects unsupported field {requested[ i ]}." );
        return result;
      }
    }

    public void Dispose()
    {
      if ( m_disposed )
        return;
      m_disposed = true;
      List<Exception> errors = null;
      foreach ( var binding in m_bindings.Values ) {
        try { binding.Dispose(); }
        catch ( Exception exception ) {
          if ( errors == null )
            errors = new List<Exception>();
          errors.Add( exception );
        }
      }
      m_bindings.Clear();
      if ( errors != null )
        throw new AggregateException( "Failed to release OpenPLX lidar output bindings.", errors );
    }

    private sealed class Binding : IDisposable
    {
      private readonly agxopenplx.AgxMetadata m_metadata;
      private readonly LidarOutput m_definition;
      private bool m_registered;
      private bool m_disposed;
      public agxSensor.Lidar Sensor { get; }
      public agxSensor.RtOutput Output { get; }

      public Binding( agxopenplx.AgxMetadata metadata, LidarOutput definition, agxSensor.Lidar sensor,
                      uint outputId, agxSensor.RtOutput.Field[] fields )
      {
        m_metadata = metadata;
        m_definition = definition;
        // An owning SWIG proxy takes a separate native reference. Unity may
        // dispose its original sensor proxy before the signal root is destroyed.
        Sensor = new agxSensor.Lidar( agxSensor.Lidar.getCPtr( sensor ).Handle, true );
        try {
          Output = new agxSensor.RtOutput();
          foreach ( var field in fields )
            Output.add( field );
          if ( !Sensor.getOutputHandler().add( outputId, Output ) )
            throw new InvalidOperationException( $"Could not attach native lidar output '{definition.getName()}' with ID {outputId}." );
          if ( !metadata.RegisterLidarOutput( Sensor, definition, Output ) )
            throw new InvalidOperationException( $"Could not register lidar output '{definition.getName()}'. Check the native field layout and sensor metadata." );
          m_registered = true;
        }
        catch {
          Dispose();
          throw;
        }
      }

      public void Dispose()
      {
        if ( m_disposed )
          return;
        m_disposed = true;
        try {
          if ( m_registered )
            m_metadata.UnregisterLidarOutput( Sensor, m_definition, Output );
        }
        finally {
          try {
            // RtOutput is parented below the handler's internal processing
            // nodes, rather than directly below the lidar or output handler.
            using ( var parent = Output?.getParent() )
              parent?.removeChild( Output );
          }
          finally {
            Output?.Dispose();
            Sensor.Dispose();
          }
        }
      }
    }
  }
}
