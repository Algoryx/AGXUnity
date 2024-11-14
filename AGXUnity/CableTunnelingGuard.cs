using UnityEngine;

namespace AGXUnity
{
    [AddComponentMenu( "AGXUnity/Cable Tunneling Guard" )]
    [DisallowMultipleComponent]
    [RequireComponent( typeof( AGXUnity.Cable ) )]
    //[HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#cable-tunneling-guard" )]
    public class CableTunnelingGuard : ScriptComponent
    {
        /// <summary>
        /// Native instance of the cable tuneling guard.
        /// </summary>
        public agxCable.CableTunnelingGuard Native { get; private set; }

        [System.NonSerialized]
        private Cable m_cable = null;

        /// <summary>
        /// The Cable ScriptComponent that this CableTunnelingGuard follows
        /// </summary>
        [HideInInspector]
        public Cable Cable { get { return m_cable ??= GetComponent<Cable>(); } }

        [SerializeField]
        private double m_hullScale;

        public double HullScale
        {
            get { return m_hullScale; }
            set
            {
                m_hullScale = value;
                if ( Native != null ) {
                    Native.setHullScale( m_hullScale );
                }
            }
        }

        [SerializeField]
        private double m_angleThreshold;

        public double AngleThreshold
        {
            get { return m_angleThreshold; }
            set
            {
                m_angleThreshold = value;
                if ( Native != null ) {
                    Native.setAngleThreshold( m_angleThreshold );
                }
            }
        }

        [SerializeField]
        private double m_leniency;

        public double Leniency
        {
            get { return m_leniency; }
            set
            {
                m_leniency = value;
                if ( Native != null ) {
                    Native.setLeniency( m_leniency );
                }
            }
        }

        [SerializeField]
        private uint m_debounceSteps;

        public uint DebounceSteps
        {
            get { return m_debounceSteps; }
            set
            {
                m_debounceSteps = value;
                if ( Native != null ) {
                    Native.setDebounceSteps( m_debounceSteps );
                }
            }
        }

        [SerializeField]
        private bool m_alwaysAdd;

        public bool AlwaysAdd
        {
            get { return m_alwaysAdd; }
            set
            {
                m_alwaysAdd = value;
                if ( Native != null ) {
                    Native.setAlwaysAdd( m_alwaysAdd );
                }
            }
        }

        [SerializeField]
        private bool m_selfInteractionEnabled;

        public bool EnableSelfInteraction
        {
            get { return m_selfInteractionEnabled; }
            set
            {
                m_selfInteractionEnabled = value;
                if ( Native != null ) {
                    Native.setEnableSelfInteraction( m_selfInteractionEnabled );
                }
            }
        }

        protected override bool Initialize()
        {
            Native = new agxCable.CableTunnelingGuard( m_hullScale );
            Native.setAngleThreshold( m_angleThreshold );
            Native.setLeniency( m_leniency );
            Native.setDebounceSteps( m_debounceSteps );
            Native.setAlwaysAdd( m_alwaysAdd );
            Native.setEnableSelfInteraction( m_selfInteractionEnabled );
            Native.setEnabled( enabled );

            var cable = Cable?.GetInitialized<Cable>()?.Native;
            if ( cable == null ) {
                Debug.LogWarning( "Unable to find Cable component for CableTunnelingGuard - cable tunneling guard instance ignored.", this );
                return false;
            }

            cable.addComponent( Native );

            return true;
        }

        protected override void OnDestroy()
        {
            Native = null;

            base.OnDestroy();
        }

        protected override void OnEnable()
        {
            if ( Native != null ) {
                Native.setEnabled( true );
            }
        }

        protected override void OnDisable()
        {
            if ( Native != null ) {
                Native.setEnabled( false );
            }
        }

        private void Reset()
        {
            if ( GetComponent<Cable>() == null )
                Debug.LogError( "Component: CableDamage requires Cable component.", this );
        }
    }

}
