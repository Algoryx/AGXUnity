using System;
using System.Collections.Generic;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity
{
  [DisallowMultipleComponent] // TODO not 100 that we want to disallow multiples?
  [RequireComponent(typeof(AGXUnity.Cable))] // TODO other ScriptComponents does this through code instead of Attribute
  public class CableDamage : ScriptComponent
  {
    /// <summary>
    /// Native instance of the cable damage.
    /// </summary>
    public agxCable.CableDamage Native { get; private set; }

    /// <summary>
    /// The Cable ScriptComponent that this CableDamage follows
    /// </summary>
    [HideInInspector]
    public Cable Cable { get { return m_cable ?? ( m_cable = GetComponent<Cable>() ); } }

    public void TestyTest()
    {
      var dam = new agxCable.CableDamage();
      dam.setStretchDeformationWeight(1);
    }
    //TODO
    //public void RestoreLocalDataFrom( agx.RigidBody native )


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
    
    protected override bool Initialize()
    {
      Native = new agxCable.CableDamage();

      // TODO remove this if we don't find any use for the native cable in Initialize()
      // var cable = Cable?.GetInitialized<Cable>()?.Native;
      // if ( cable == null ) {
      //   Debug.LogWarning( "Unable to find Cable component for CableDamage - cable damage instance ignored.", this );
      //   return false;
      // }

      if ( Properties == null ) {
        Properties = ScriptAsset.Create<CableDamageProperties>();
        Properties.name = "[Temporary] Cable Damage Properties";
      }

      return true;
    }

    void Update()
    {
      Debug.Log("Test: " + Properties.BendDeformation);
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

    private Cable m_cable = null;

  }
}