using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor
{
  [PreviousSettingsFile( FileName = "Data.asset" )]
  public class EditorData : AGXUnityEditorSettings<EditorData>
  {
    public double SecondsSinceLastGC { get { return EditorApplication.timeSinceStartup - m_lastGC; } }

    public int NumEntries { get { return m_data.Count; } }

    public int NumCachedEntries { get { return m_dataCache.Count; } }

    public EditorDataEntry GetStaticData( string identifier, Action<EditorDataEntry> onCreate = null )
    {
      return GetData( null, identifier, onCreate );
    }

    public EditorDataEntry GetData( UnityEngine.Object target, string identifier, Action<EditorDataEntry> onCreate = null )
    {
      var key = EditorDataEntry.CalculateKey( target, identifier );
      int dataIndex = -1;
      if ( !m_dataCache.TryGetValue( key, out dataIndex ) ) {
        dataIndex = m_data.FindIndex( data => data.Key == key );
        if ( dataIndex < 0 ) {
          EditorDataEntry instance = new EditorDataEntry( target, key );
          dataIndex = m_data.Count;

          m_data.Add( instance );

          if ( onCreate != null )
            onCreate.Invoke( instance );
        }

        m_dataCache.Add( key, dataIndex );
      }

      return m_data[ dataIndex ];
    }

    public void GC()
    {
      m_dataCache.Clear();

      int index = 0;
      while ( index < m_data.Count ) {
        var data = m_data[ index ];
        if ( data == null || ( !data.IsStatic && EditorUtility.InstanceIDToObject( data.InstanceId ) == null ) )
          m_data.RemoveAt( index );
        else
          ++index;
      }

      m_lastGC = EditorApplication.timeSinceStartup;
    }

    public void OnInspectorGUI()
    {
      EditorGUI.BeginChangeCheck();
      var skin       = InspectorEditor.Skin;

      GUILayout.Label( GUI.MakeLabel( "Editor data", 15, true ), skin.Label );

      EditorGUILayout.Space();

      const float firstLabelWidth = 190;

      using ( new EditorGUILayout.HorizontalScope() ) {
        TimeSpan span = TimeSpan.FromSeconds( SecondsSinceLastGC );
        GUILayout.Label( GUI.MakeLabel( "Seconds since last GC:" ), skin.Label, GUILayout.Width( firstLabelWidth ) );
        GUILayout.Label( GUI.MakeLabel( string.Format( "{0:D2}m:{1:D2}s", span.Minutes, span.Seconds ), true ), skin.Label );
      }

      using ( new EditorGUILayout.HorizontalScope() ) {
        GUILayout.Label( GUI.MakeLabel( "Number of data entries:" ), skin.Label, GUILayout.Width( firstLabelWidth ) );
        GUILayout.Label( GUI.MakeLabel( NumEntries.ToString(), true ), skin.Label );
      }

      using ( new EditorGUILayout.HorizontalScope() ) {
        GUILayout.Label( GUI.MakeLabel( "Number of cached data entries:" ), skin.Label, GUILayout.Width( firstLabelWidth ) );
        GUILayout.Label( GUI.MakeLabel( NumCachedEntries.ToString(), true ), skin.Label );
      }

      EditorGUILayout.Space();

      using ( new GUI.ColorBlock( Color.Lerp( UnityEngine.GUI.color, Color.green, 0.25f ) ) )
        if ( GUILayout.Button( GUI.MakeLabel( "Collect garbage" ), skin.Button, GUILayout.Width( 110 ) ) )
          GC();

      if ( EditorGUI.EndChangeCheck() )
        Save();
    }

    [SerializeField]
    private List<EditorDataEntry> m_data = new List<EditorDataEntry>();
    private Dictionary<uint, int> m_dataCache = new Dictionary<uint, int>();

    [SerializeField]
    private double m_lastGC = 0.0;
  }
}
