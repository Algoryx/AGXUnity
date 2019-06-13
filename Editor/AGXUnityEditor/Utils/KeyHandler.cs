using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

using Object = UnityEngine.Object;

namespace AGXUnityEditor.Utils
{
  /// <summary>
  /// Handles when specific key is down/pressed during GUI event loop.
  /// </summary>
  [Serializable]
  public class KeyHandler
  {
    /// <summary>
    /// Array of key codes. Assign new key code by using bracket operator, i.e.,
    /// handler[ keyIndex ] = KeyCode.T;
    /// </summary>
    public KeyCode[] Keys { get { return ( from data in m_keyData select data.Key ).ToArray(); } }

    /// <summary>
    /// Number of key codes in this handler.
    /// </summary>
    public int NumKeyCodes { get { return m_keyData.Count; } }

    /// <summary>
    /// Return/assign key code given key code index.
    /// </summary>
    /// <param name="keyIndex"></param>
    /// <returns></returns>
    public KeyCode this[ int keyIndex ]
    {
      get { return m_keyData[ keyIndex ].Key; }
      set
      {
        KeyData data = m_keyData[ keyIndex ];
        data.Key = value;
        data.IsDown = false;

        OnKeyDataListEntryChanged();
      }
    }

    /// <summary>
    /// True if the given key is down - otherwise false.
    /// </summary>
    public bool IsDown
    {
      get { return m_enable && m_isDown; }
      private set
      {
        if ( value && value != m_isDown && HideDefaultHandlesWhenIsDown ) {
          if ( m_defaultHandleStateHidden != null )
            Debug.LogError( "Default handle state already present." );

          m_defaultHandleStateHidden = new Tools.Tool.HideDefaultState();
        }
        else if ( !value ) {
          if ( m_defaultHandleStateHidden != null )
            m_defaultHandleStateHidden.OnRemove();
          m_defaultHandleStateHidden = null;
        }

        m_isDown = value;
      }
    }

    public bool Enable
    {
      get { return m_enable; }
      set { m_enable = value; }
    }

    /// <summary>
    /// True to hide the default handles when the state is IsDown.
    /// </summary>
    public bool HideDefaultHandlesWhenIsDown { get; set; }

    /// <summary>
    ///  Default constructor.
    /// </summary>
    /// <param name="key">Key to handle.</param>
    public KeyHandler( params KeyCode[] keys )
    {
      m_defaultKeys                = (KeyCode[])keys.Clone();
      m_keyData                    = ( from key in keys
                                       select new KeyData() { IsDown = false, Key = key } ).ToList();
      HideDefaultHandlesWhenIsDown = false;

      OnKeyDataListEntryChanged();
    }

    /// <summary>
    /// Add key to this handler. All keys has to be pressed/down for IsDown to be true.
    /// </summary>
    /// <param name="key">Key to add.</param>
    /// <returns>False if the key already exist. Otherwise true.</returns>
    public bool Add( KeyCode key )
    {
      if ( key != KeyCode.None && Keys.Contains( key ) )
        return false;

      m_keyData.Add( new KeyData() { IsDown = false, Key = key } );

      OnKeyDataListEntryChanged();

      return true;
    }

    /// <summary>
    /// Remove key at given key index.
    /// </summary>
    /// <param name="keyIndex">Index of key to remove.</param>
    /// <returns>True if removed, otherwise false.</returns>
    public bool Remove( int keyIndex )
    {
      if ( keyIndex >= m_keyData.Count )
        return false;

      m_keyData.RemoveAt( keyIndex );

      OnKeyDataListEntryChanged();

      return true;
    }

    /// <summary>
    /// Start/stop detect key given BaseEditor target, enable/disable flag and key index.
    /// </summary>
    /// <param name="target">Target object with BaseEditor custom editor.</param>
    /// <param name="flag">True to enable, false to disable.</param>
    /// <param name="keyIndex">Index of key to detect. </param>
    public void DetectKey( Object target, bool flag, int keyIndex )
    {
      if ( flag && IsValidKeyIndex( keyIndex ) )
        m_detectKeyData = new DetectKeyData() { Target = target, KeyHandler = this, KeyIndex = keyIndex };
      else
        m_detectKeyData = null;
    }

    /// <summary>
    /// Check if <paramref name="keyIndex"/> is within bounds.
    /// </summary>
    /// <param name="keyIndex">Index of key code.</param>
    /// <returns>True if <paramref name="keyIndex"/> is within bounds. Otherwise false.</returns>
    public bool IsValidKeyIndex( int keyIndex )
    {
      return keyIndex >= 0 && keyIndex < m_keyData.Count;
    }

    /// <summary>
    /// True if given key index is currently detecting new value.
    /// </summary>
    /// <param name="keyIndex">Key index to check.</param>
    /// <returns>True if key index currently is detecting new value.</returns>
    public bool IsDetectingKey( int keyIndex )
    {
      return IsValidKeyIndex( keyIndex ) && m_detectKeyData != null && m_detectKeyData.KeyHandler == this && m_detectKeyData.KeyIndex == keyIndex;
    }

    /// <summary>
    /// Reset keys to default - i.e., the ones this key handler were initially created with.
    /// </summary>
    public void ResetToDefault()
    {
      m_keyData = ( from key in m_defaultKeys select new KeyData() { IsDown = false, Key = key } ).ToList();
    }

    public void OnRemove()
    {
      if ( m_defaultHandleStateHidden != null )
        m_defaultHandleStateHidden.OnRemove();
    }

    /// <summary>
    /// Update given current event. This method is automatically
    /// called during GUI update.
    /// </summary>
    public void Update( Event current )
    {
      bool allDown   = m_keyData.Count > 0;
      bool allIsNone = true;
      foreach ( var data in m_keyData ) {
        bool wasDown = data.IsDown;

        if ( data.Key == KeyCode.None )
          data.IsDown = true;
        else if ( data.Key == KeyCode.LeftShift || data.Key == KeyCode.RightShift )
          data.IsDown = current.shift;
        else if ( current.type == EventType.KeyDown && data.Key == current.keyCode )
          data.IsDown = true;
        else if ( current.type == EventType.KeyUp && data.Key == current.keyCode )
          data.IsDown = false;

        allDown   = allDown && data.IsDown;
        allIsNone = allIsNone && data.Key == KeyCode.None;
      }

      IsDown = !allIsNone && allDown;
    }

    [Serializable]
    private class KeyData
    {
      [SerializeField]
      public KeyCode Key = KeyCode.None;

      public bool IsDown { get; set; }
    }

    private class KeyDataEqualityComparer : IEqualityComparer<KeyData>
    {
      public bool Equals( KeyData kd1, KeyData kd2 )
      {
        return kd1.Key != KeyCode.None && kd2.Key != KeyCode.None && kd1.Key == kd2.Key;
      }

      public int GetHashCode( KeyData kd )
      {
        return kd.Key.GetHashCode();
      }
    }

    private void OnKeyDataListEntryChanged()
    {
      m_keyData = m_keyData.Distinct( new KeyDataEqualityComparer() ).ToList();
    }

    [NonSerialized]
    private bool m_isDown = false;
    private Tools.Tool.HideDefaultState m_defaultHandleStateHidden = null;

    [SerializeField]
    private List<KeyData> m_keyData = new List<KeyData>();
    [SerializeField]
    private bool m_enable = true;

    private KeyCode[] m_defaultKeys = null;

    #region Detect key manager
    private class DetectKeyData
    {
      public Object Target = null;
      public KeyHandler KeyHandler = null;
      public int KeyIndex = -1;
    }

    private static DetectKeyData m_detectKeyData = null;

    public static void HandleDetectKeyOnEnable( Object[] targets ) { }

    public static bool HandleDetectKeyOnGUI( Object[] targets, Event current )
    {
      if ( m_detectKeyData == null )
        return false;

      if ( targets.Contains( m_detectKeyData.Target ) && ( current.type == EventType.KeyDown || current.shift ) ) {
        KeyCode keyCode = current.shift ? KeyCode.LeftShift : current.keyCode;
        if ( keyCode != m_detectKeyData.KeyHandler[ m_detectKeyData.KeyIndex ] &&
             EditorUtility.DisplayDialog( "Key detected", "Change '" +
                                          m_detectKeyData.KeyHandler[ m_detectKeyData.KeyIndex ].ToString() +
                                          "' to '" +
                                          keyCode.ToString() +
                                          "'?", "Ok", "Cancel" ) ) {
          m_detectKeyData.KeyHandler[ m_detectKeyData.KeyIndex ] = keyCode;
          m_detectKeyData = null;
          current.Use();
          return true;
        }
        else {
          m_detectKeyData = null;
          foreach ( var target in targets )
            EditorUtility.SetDirty( target );
        }
      }

      return false;
    }

    public static void HandleDetectKeyOnDisable( Object[] targets )
    {
      if ( m_detectKeyData != null && targets.Contains( m_detectKeyData.Target ) )
        m_detectKeyData = null;
    }
    #endregion
  }
}
