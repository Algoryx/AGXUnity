using System;
using System.Collections.Generic;
using UnityEngine;
using AGXUnity.Utils;
using AGXUnity;
using AGXUnity.Rendering;

namespace AGXUnity
{
  [DisallowMultipleComponent] // TODO not 100 that we want to disallow multiples, depends on usecases etc
  [RequireComponent(typeof(AGXUnity.Cable))] // TODO other ScriptComponents does this through code instead of Attribute. Is this because RequireComponent will create instance of Cable? Look into how Cable is created from menu
  public class CableDamage : ScriptComponent
  {
    /// <summary>
    /// Native instance of the cable damage.
    /// </summary>
    public agxCable.CableDamage Native { get; private set; }

     [System.NonSerialized]
     private Cable m_cable = null;

    /// <summary>
    /// The Cable ScriptComponent that this CableDamage follows
    /// </summary>
    [HideInInspector]
    public Cable Cable { get { return m_cable ?? ( m_cable = GetComponent<Cable>() ); } }

    [System.NonSerialized]
    private CableRenderer m_cableRenderer = null;

    /// <summary>
    /// The CableRenderer ScriptComponent that might be used to render the damages
    /// </summary>
    [HideInInspector]
    public CableRenderer CableRenderer { get { return m_cableRenderer ?? ( m_cableRenderer = GetComponent<CableRenderer>() ); } }

    /// <summary>
    /// ScriptableAsset with weights of each of the cable damage components, determining which one contributes how much to the cable damage score
    /// </summary>
    [SerializeField]
    private CableDamageProperties m_properties = null;

    [AllowRecursiveEditing]
    public CableDamageProperties Properties
    {
      get { return m_properties; }
      set
      {
        if ( Native != null && m_properties != null && m_properties != value )
          m_properties.Unregister( this );

        m_properties = value;

        if ( Native != null && m_properties != null )
          m_properties.Register( this );
      }
    }

    /// <summary>
    /// Will use the first CableRenderer component instance on this object to render the cable damage if active.
    /// </summary>
    [SerializeField]
    private bool m_renderCableDamage = false;
    public bool RenderCableDamage {
      get { return m_renderCableDamage; }
      set {
        m_renderCableDamage = value;
        CableRenderer?.RenderDamages(value);
      }
    }

    private float[] m_damageValues = new float[0]; // TODO it wouldn't be too bad to be able to see these in the inspector...

    protected override bool Initialize()
    {
      Native = new agxCable.CableDamage();

      var cable = Cable?.GetInitialized<Cable>()?.Native;
      if ( cable == null ) {
        Debug.LogWarning( "Unable to find Cable component for CableDamage - cable damage instance ignored.", this );
        return false;
      }

      cable.addComponent(Native);

      if ( Properties == null ) 
      {
        Properties = ScriptAsset.Create<CableDamageProperties>();
        Properties.name = "[Temporary] Cable Damage Properties";
      }

      return true;
    }

    void Update()
    {
      var segmentDamages = Native.getCurrentDamages();

      if (m_damageValues.Length != segmentDamages.Count)
        m_damageValues = new float[segmentDamages.Count];

      float maxValue = float.MinValue;
      for (int i = 0; i < segmentDamages.Count; i++){
        float value = (float)segmentDamages[i].total();
        maxValue = Mathf.Max(maxValue, value);
        m_damageValues[i] = value;
      }

      if (RenderCableDamage && CableRenderer)
        CableRenderer.SetDamageValues(m_damageValues, maxValue);

      //Debug.Log("Total damage: " + total);
    }

    protected override void OnDestroy()
    {
      if ( Properties != null )
        Properties.Unregister( this );

      Native = null;

      base.OnDestroy();
    }

    protected override void OnEnable()
    {
    }

    protected override void OnDisable()
    {
    }

    private void Reset()
    {
      if ( GetComponent<Cable>() == null )
        Debug.LogError( "Component: CableDamage requires Cable component.", this );
    }
  }
}