using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity
{
  [AddComponentMenu( "" )]
  [DoNotGenerateCustomEditor]
  public abstract class WindAndWaterParameters<T> : ScriptComponent
    where T : agxModel.WindAndWaterParameters
  {
    public enum ShapeTessellationLevel
    {
      Low,
      Medium,
      High,
      UltraHigh,
      Default = Medium
    }

    [SerializeField]
    private ShapeTessellationLevel m_shapeTessellation = ShapeTessellationLevel.Default;

    public ShapeTessellationLevel ShapeTessellation
    {
      get { return m_shapeTessellation; }
      set
      {
        m_shapeTessellation = value;
        if ( m_parameters != null ) {
          foreach ( var parameters in m_parameters )
            parameters.setShapeTessellationLevel( (agxModel.WindAndWaterParameters.ShapeTessellation)Convert.ToInt32( m_shapeTessellation ) );
        }
      }
    }

    [SerializeField]
    private float m_pressureDrag = 0.6f;

    public float PressureDrag
    {
      get { return m_pressureDrag; }
      set
      {
        m_pressureDrag = value;
        SetNativeCoefficient( agxModel.WindAndWaterParameters.Coefficient.PRESSURE_DRAG, m_pressureDrag );
      }
    }

    [SerializeField]
    private float m_viscousDrag = 0.1f;

    public float ViscousDrag
    {
      get { return m_viscousDrag; }
      set
      {
        m_viscousDrag = value;
        SetNativeCoefficient( agxModel.WindAndWaterParameters.Coefficient.VISCOUS_DRAG, m_viscousDrag );
      }
    }

    [SerializeField]
    private float m_lift = 0.01f;

    public float Lift
    {
      get { return m_lift; }
      set
      {
        m_lift = value;
        SetNativeCoefficient( agxModel.WindAndWaterParameters.Coefficient.LIFT, m_lift );
      }
    }

    [SerializeField]
    private bool m_propagateToChildren = false;

    public bool PropagateToChildren
    {
      get { return m_propagateToChildren; }
      set { m_propagateToChildren = value; }
    }

    protected Find.LeafData m_objects = null;
    protected List<T> m_parameters = null;

    protected override bool Initialize()
    {
      if ( !WindAndWaterManager.HasInstanceInScene )
        return false;

      m_objects = Find.LeafObjects( gameObject, PropagateToChildren );
      m_parameters = new List<T>();

      AddParameters<Collide.Shape, agxCollide.Shape>( m_objects.Shapes, shape => shape.NativeShape );
      AddParameters<Wire, agxWire.Wire>( m_objects.Wires, wire => wire.Native );
      AddParameters<Cable, agxCable.Cable>( m_objects.Cables, cable => cable.Native );

      return true;
    }

    protected override void OnDestroy()
    {
      m_objects = null;
      m_parameters = null;

      base.OnDestroy();
    }

    private void AddParameters<U, N>( IEnumerable<U> list, Func<U, object> nativeGetter )
      where U : ScriptComponent
      where N : agx.Referenced
    {
      var manager = WindAndWaterManager.Instance.GetInitialized<WindAndWaterManager>();
      if ( manager == null )
        return;

      var getOrCreateMethod = FindGetOrCreateMethod( typeof( N ) );
      if ( getOrCreateMethod == null )
        return;

      foreach ( var obj in list ) {
        if ( obj.GetInitialized<U>() == null )
          continue;

        var parameters = getOrCreateMethod.Invoke( manager.Native, new object[] { nativeGetter( obj ) } ) as T;
        if ( parameters != null )
          m_parameters.Add( parameters );
      }
    }

    private MethodInfo FindGetOrCreateMethod( Type argType )
    {
      var methodName = typeof( T ) == typeof( agxModel.HydrodynamicsParameters ) ?
                         "getOrCreateHydrodynamicsParameters" :
                         "getOrCreateAerodynamicsParameters";
      try {
        var getOrCreateMethod = typeof( agxModel.WindAndWaterController ).GetMethod( methodName, new Type[] { argType } );
        return getOrCreateMethod;
      }
      catch ( Exception e ) {
        Debug.LogException( e, this );
        return null;
      }
    }

    protected void SetNativeCoefficient( agxModel.WindAndWaterParameters.Coefficient coefficient, float value )
    {
      if ( m_parameters == null )
        return;

      foreach ( var parameters in m_parameters )
        parameters.setCoefficient( coefficient, value );
    }
  }
}
