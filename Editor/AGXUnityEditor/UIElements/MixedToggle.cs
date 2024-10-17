using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace AGXUnityEditor.UIElements
{
  public class MixedToggle : Toggle
  {
    private Label m_mixedLabel;
    private List<Toggle> m_controlledToggles = new List<Toggle>();
    private bool m_updating = false;

    protected override void UpdateMixedValueContent()
    {
      if ( showMixedValue ) {
        m_CheckMark.style.flexDirection = FlexDirection.Row;
        m_CheckMark.style.justifyContent = Justify.Center;
        if ( m_mixedLabel == null ) {
          m_mixedLabel = new Label( "-" );
          m_mixedLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
          m_mixedLabel.style.paddingRight = 0;
          m_mixedLabel.style.fontSize = 20;
          m_mixedLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        }
        m_CheckMark.Add( m_mixedLabel );
      }
      else
        m_CheckMark.Remove( m_mixedLabel );
    }

    public void AddControlledToggle( Toggle toggle )
    {
      m_controlledToggles.Add( toggle );
      showMixedValue = m_controlledToggles.Select( t => t.value ).Distinct().Count() == 2;
      SetValueWithoutNotify( !showMixedValue && toggle.value );

      toggle.RegisterValueChangedCallback( ce => {
        if ( m_updating )
          return;
        m_updating = true;

        showMixedValue = m_controlledToggles.Select( t => t.value ).Distinct().Count() == 2;
        SetValueWithoutNotify( !showMixedValue && ce.newValue );

        m_updating = false;
      } );
      this.RegisterValueChangedCallback( ce => {
        if ( m_updating )
          return;
        m_updating = true;

        foreach ( var t in m_controlledToggles )
          t.value = ce.newValue;

        showMixedValue = false;

        m_updating = false;
      } );
    }
  }
}