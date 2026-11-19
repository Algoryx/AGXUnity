using AGXUnity.IO.OpenPLX;
using AGXUnity.Sensor;
using System;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Simulation = AGXUnity.Simulation;

namespace AGXUnityEditor.Windows
{
  /// <summary>Browses the lidar outputs of an imported OpenPLX scene.</summary>
  public class OpenPLXLidarOutputWindow : EditorWindow
  {
    [SerializeField] private OpenPLXSignals m_signals;
    [SerializeField] private int m_outputIndex;
    [SerializeField] private bool m_drawPoints = true;
    [SerializeField] private int m_maxPoints = 5000;
    [SerializeField] private float m_pointRadius = 0.03f;
    [SerializeField] private Color m_pointColor = Color.cyan;
    private LidarSignalReader m_reader;
    private OutputSource m_output;
    private LidarSensor m_sensor;
    private Simulation m_simulation;
    private bool m_connecting;
    private string m_pendingOutput;
    private long m_readCount;
    private Vector3[] m_points = Array.Empty<Vector3>();
    private int m_pointCount;
    private string m_gizmoStatus = string.Empty;
    private LidarSignalFrame m_frozenWindow;
    private LidarSignalFrame m_displayWindow;
    private long m_displayRead;
    private long m_frozenRead;
    private Vector2 m_scroll;
    private Vector2 m_pageScroll;
    private Vector2 m_fieldsScroll;
    private string m_fieldFilter = string.Empty;
    private string m_message = string.Empty;
    private int m_page;
    private int m_rowsPerPage = 25;
    private bool m_showFields = true;
    private bool m_showGizmoSettings;
    private GUIStyle m_pageStyle;
    private GUIStyle m_panelStyle;
    private GUIStyle m_cellStyle;
    private GUIStyle m_headerStyle;
    private GUIStyle m_numberStyle;
    private const float TableRowHeight = 22;
    private const float IndexColumnWidth = 64;
    private const float ValueColumnWidth = 170;

    [MenuItem( "AGXUnity/OpenPLX/Lidar Output Viewer" )]
    public static void Open()
    {
      var window = GetWindow<OpenPLXLidarOutputWindow>( "OpenPLX Lidar" );
      window.minSize = new Vector2( 550, 420 );
      if ( window.m_signals == null )
        window.UseSelection();
      window.Show();
    }

    private void OnEnable()
    {
      EditorApplication.playModeStateChanged += OnPlayModeChanged;
      SceneView.duringSceneGui += DrawScenePoints;
    }

    private void OnDisable()
    {
      EditorApplication.playModeStateChanged -= OnPlayModeChanged;
      SceneView.duringSceneGui -= DrawScenePoints;
      Disconnect();
    }

    private void OnPlayModeChanged( PlayModeStateChange state )
    {
      if ( state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode )
        Disconnect();
      Repaint();
    }

    private void OnInspectorUpdate()
    {
      if ( Application.isPlaying ) {
        if ( m_reader != null && ( m_signals == null || !m_output.IsValid ) ) {
          Disconnect();
          m_message = "The signal root was destroyed or invalidated. Select another root and Connect.";
        }
        Repaint();
      }
    }

    private void UseSelection()
    {
      var selection = Selection.activeGameObject;
      if ( selection == null )
        return;
      var signals = selection.GetComponentInParent<OpenPLXSignals>();
      if ( signals == null ) {
        var children = selection.GetComponentsInChildren<OpenPLXSignals>();
        if ( children.Length == 1 )
          signals = children[ 0 ];
      }
      if ( signals != m_signals ) {
        Disconnect();
        m_signals = signals;
        m_outputIndex = 0;
      }
    }

    private void OnGUI()
    {
      EnsureStyles();
      var oldLabelWidth = EditorGUIUtility.labelWidth;
      var oldFieldWidth = EditorGUIUtility.fieldWidth;
      var oldWideMode = EditorGUIUtility.wideMode;
      var oldIndent = EditorGUI.indentLevel;
      try {
        EditorGUIUtility.labelWidth = 150;
        EditorGUIUtility.fieldWidth = 60;
        EditorGUIUtility.wideMode = true;
        EditorGUI.indentLevel = 0;
        using ( var page = new EditorGUILayout.ScrollViewScope( m_pageScroll ) ) {
          m_pageScroll = page.scrollPosition;
          using ( new EditorGUILayout.VerticalScope( m_pageStyle ) )
            DrawContents();
        }
      }
      finally {
        EditorGUIUtility.labelWidth = oldLabelWidth;
        EditorGUIUtility.fieldWidth = oldFieldWidth;
        EditorGUIUtility.wideMode = oldWideMode;
        EditorGUI.indentLevel = oldIndent;
      }
    }

    private void EnsureStyles()
    {
      if ( m_panelStyle != null )
        return;
      m_pageStyle = new GUIStyle { padding = new RectOffset( 8, 8, 8, 8 ) };
      m_panelStyle = new GUIStyle( EditorStyles.helpBox )
      {
        padding = new RectOffset( 10, 10, 8, 8 ),
        margin = new RectOffset( 0, 0, 0, 8 )
      };
      m_cellStyle = new GUIStyle( EditorStyles.label )
      {
        clipping = TextClipping.Clip,
        alignment = TextAnchor.MiddleLeft,
        padding = new RectOffset( 6, 6, 2, 2 )
      };
      m_headerStyle = new GUIStyle( m_cellStyle ) { fontStyle = FontStyle.Bold };
      m_numberStyle = new GUIStyle( m_cellStyle ) { alignment = TextAnchor.MiddleRight };
    }

    private void DrawContents()
    {
      GUILayout.Label( "OpenPLX lidar outputs", EditorStyles.boldLabel );
      GUILayout.Label( "Browse the fields and sample window of a lidar output in your imported scene.", EditorStyles.wordWrappedLabel );
      EditorGUILayout.Space( 8 );

      using ( new EditorGUILayout.VerticalScope( m_panelStyle ) ) {
        GUILayout.Label( "Source", EditorStyles.boldLabel );
        EditorGUI.BeginChangeCheck();
        var signals = (OpenPLXSignals)EditorGUILayout.ObjectField( "OpenPLX root", m_signals, typeof( OpenPLXSignals ), true );
        if ( EditorGUI.EndChangeCheck() ) {
          Disconnect();
          m_signals = signals;
          m_outputIndex = 0;
        }
        using ( new EditorGUILayout.HorizontalScope() ) {
          GUILayout.FlexibleSpace();
          if ( GUILayout.Button( "Use selection", GUILayout.Width( 120 ) ) )
            UseSelection();
        }
        if ( m_signals == null ) {
          if ( m_reader != null || m_connecting )
            Disconnect();
          EditorGUILayout.HelpBox( "Select an imported OpenPLX scene instance containing OpenPLXSignals.", MessageType.Info );
          return;
        }

        var outputs = m_signals.Outputs.Where( IsLidarOutput ).ToArray();
        if ( outputs.Length == 0 ) {
          if ( m_reader != null || m_connecting )
            Disconnect();
          EditorGUILayout.HelpBox( "No lidar outputs were found under this OpenPLX root. Declare a Sensors.Signals.LidarOutput in the model and reimport it.", MessageType.Info );
          return;
        }
        EditorGUILayout.Space( 4 );
        m_outputIndex = Mathf.Clamp( m_outputIndex, 0, outputs.Length - 1 );
        EditorGUI.BeginChangeCheck();
        var outputIndex = EditorGUILayout.Popup( "Lidar output", m_outputIndex, outputs.Select( value => value.Name ).ToArray() );
        if ( EditorGUI.EndChangeCheck() ) {
          Disconnect();
          m_outputIndex = outputIndex;
        }

        EditorGUILayout.Space( 6 );
        using ( new EditorGUILayout.HorizontalScope() )
        using ( new EditorGUI.DisabledScope( !Application.isPlaying || !m_signals.gameObject.scene.IsValid() ) ) {
          if ( GUILayout.Button( m_reader == null && !m_connecting ? "Connect" : "Reconnect", GUILayout.Height( 24 ) ) )
            Connect( outputs[ m_outputIndex ] );
          using ( new EditorGUI.DisabledScope( m_reader == null ) ) {
            if ( GUILayout.Button( "Read now", GUILayout.Height( 24 ) ) )
              ReadNow();
          }
          using ( new EditorGUI.DisabledScope( m_reader == null && !m_connecting ) ) {
            if ( GUILayout.Button( "Disconnect", GUILayout.Height( 24 ) ) )
              Disconnect();
          }
        }
        if ( !Application.isPlaying )
          EditorGUILayout.HelpBox( "Enter Play Mode, then Connect to read the selected output. Reading and gizmos stop when the window is closed.", MessageType.Info );
        else if ( !string.IsNullOrEmpty( m_message ) )
          EditorGUILayout.HelpBox( m_message, MessageType.Info );
      }
      if ( !Application.isPlaying || m_reader == null )
        return;

      // Hold the displayed rows stable between IMGUI's Layout/Repaint events.
      if ( m_displayWindow == null || Event.current.type == EventType.Layout ) {
        m_displayWindow = m_frozenWindow ?? m_reader.Frame.Copy();
        m_displayRead = m_frozenWindow != null ? m_frozenRead : m_readCount;
      }
      var frame = m_displayWindow;
      using ( new EditorGUILayout.VerticalScope( m_panelStyle ) ) {
        GUILayout.Label( "Sample window", EditorStyles.boldLabel );
        EditorGUILayout.LabelField( "Array", frame.ArrayPath );
        EditorGUILayout.LabelField( "Samples", frame.SampleCount.ToString( "N0", CultureInfo.InvariantCulture ) );
        EditorGUILayout.LabelField( "Fields per sample", frame.Fields.Count.ToString() );
        EditorGUILayout.LabelField( "Bytes per sample", frame.RecordSize.ToString() );
        EditorGUILayout.Space( 4 );
        if ( GUILayout.Button( m_frozenWindow == null ? "Freeze table" : "Resume live table", GUILayout.Height( 24 ) ) ) {
          m_frozenWindow = m_frozenWindow == null ? m_displayWindow : null;
          m_frozenRead = m_displayRead;
        }
        GUILayout.Label( m_frozenWindow == null ? $"Live read {m_displayRead}" : $"Frozen read {m_frozenRead}. Point display remains live.",
                         EditorStyles.wordWrappedLabel );
      }
      DrawGizmoControls();
      DrawFields( frame );
      DrawWindowValues( frame );
    }

    private void DrawGizmoControls()
    {
      using ( new EditorGUILayout.VerticalScope( m_panelStyle ) ) {
        GUILayout.Label( "Point display", EditorStyles.boldLabel );
        EditorGUI.BeginChangeCheck();
        m_drawPoints = EditorGUILayout.Toggle( "Draw 3D positions", m_drawPoints );
        m_showGizmoSettings = EditorGUILayout.Foldout( m_showGizmoSettings, "Appearance", true );
        if ( m_showGizmoSettings ) {
          m_maxPoints = Math.Max( 1, EditorGUILayout.IntField( "Maximum drawn points", m_maxPoints ) );
          m_pointRadius = Mathf.Max( 0.001f, EditorGUILayout.FloatField( "Point radius", m_pointRadius ) );
          m_pointColor = EditorGUILayout.ColorField( "Color", m_pointColor );
        }
        if ( EditorGUI.EndChangeCheck() ) {
          UpdatePoints( m_reader.Frame );
          SceneView.RepaintAll();
        }
        if ( !string.IsNullOrEmpty( m_gizmoStatus ) )
          EditorGUILayout.HelpBox( m_gizmoStatus, MessageType.Info );
      }
    }

    private void DrawFields( LidarSignalFrame frame )
    {
      using ( new EditorGUILayout.VerticalScope( m_panelStyle ) ) {
        m_showFields = EditorGUILayout.Foldout( m_showFields, $"Output fields ({frame.Fields.Count})", true );
        if ( !m_showFields )
          return;
        using ( var scroll = new EditorGUILayout.ScrollViewScope( m_fieldsScroll, GUILayout.Height( Mathf.Min( 200, ( frame.Fields.Count + 1 ) * TableRowHeight + 4 ) ) ) ) {
          m_fieldsScroll = scroll.scrollPosition;
          var table = GUILayoutUtility.GetRect( 300, ( frame.Fields.Count + 1 ) * TableRowHeight, GUILayout.ExpandWidth( true ) );
          var fieldWidth = table.width - 170;
          for ( int row = 0; row <= frame.Fields.Count; ++row ) {
            var rect = new Rect( table.x, table.y + row * TableRowHeight, table.width, TableRowHeight );
            DrawTableRowBackground( rect, row );
            if ( row == 0 ) {
              GUI.Label( Cell( rect, 0, fieldWidth ), "Field", m_headerStyle );
              GUI.Label( Cell( rect, fieldWidth, 110 ), "Type", m_headerStyle );
              GUI.Label( Cell( rect, fieldWidth + 110, 60 ), "Bytes", m_headerStyle );
            }
            else {
              var field = frame.Fields[ row - 1 ];
              GUI.Label( Cell( rect, 0, fieldWidth ), new GUIContent( field.Path, field.Path ), m_cellStyle );
              GUI.Label( Cell( rect, fieldWidth, 110 ), field.Type.ToString(), m_cellStyle );
              GUI.Label( Cell( rect, fieldWidth + 110, 60 ), field.ByteSize.ToString(), m_numberStyle );
            }
          }
        }
      }
    }

    private void DrawWindowValues( LidarSignalFrame frame )
    {
      using ( new EditorGUILayout.VerticalScope( m_panelStyle ) ) {
        GUILayout.Label( "Window values", EditorStyles.boldLabel );
        m_fieldFilter = EditorGUILayout.TextField( "Filter fields", m_fieldFilter );
        m_rowsPerPage = EditorGUILayout.IntPopup( "Rows per page", m_rowsPerPage, new[] { "25", "50", "100" }, new[] { 25, 50, 100 } );
        var fields = frame.Fields.Where( field => string.IsNullOrEmpty( m_fieldFilter ) ||
                                                 field.Path.IndexOf( m_fieldFilter, StringComparison.OrdinalIgnoreCase ) >= 0 ).ToArray();
        var pages = Math.Max( 1, (int)( ( (long)frame.SampleCount + m_rowsPerPage - 1 ) / m_rowsPerPage ) );
        m_page = Mathf.Clamp( m_page, 0, pages - 1 );
        EditorGUILayout.Space( 4 );
        using ( new EditorGUILayout.HorizontalScope() ) {
          GUILayout.FlexibleSpace();
          using ( new EditorGUI.DisabledScope( m_page == 0 ) )
            if ( GUILayout.Button( "Previous", GUILayout.Width( 70 ) ) )
              --m_page;
          GUILayout.Label( "Page", GUILayout.Width( 34 ) );
          m_page = Mathf.Clamp( EditorGUILayout.IntField( m_page + 1, GUILayout.Width( 60 ) ) - 1, 0, pages - 1 );
          GUILayout.Label( $"of {pages}", GUILayout.Width( 80 ) );
          using ( new EditorGUI.DisabledScope( m_page >= pages - 1 ) )
            if ( GUILayout.Button( "Next", GUILayout.Width( 70 ) ) )
              ++m_page;
          GUILayout.FlexibleSpace();
        }
        EditorGUILayout.Space( 4 );
        if ( frame.SampleCount == 0 ) {
          EditorGUILayout.HelpBox( "This window is empty. Field information remains available.", MessageType.Info );
          return;
        }
        if ( fields.Length == 0 ) {
          EditorGUILayout.HelpBox( "No fields match the filter.", MessageType.Info );
          return;
        }

        using ( var scroll = new EditorGUILayout.ScrollViewScope( m_scroll, GUILayout.Height( Mathf.Clamp( position.height * 0.45f, 180, 360 ) ) ) ) {
          m_scroll = scroll.scrollPosition;
          var start = m_page * m_rowsPerPage;
          var end = (int)Math.Min( frame.SampleCount, (long)( m_page + 1 ) * m_rowsPerPage );
          var table = GUILayoutUtility.GetRect( IndexColumnWidth + fields.Length * ValueColumnWidth,
                                                ( end - start + 1 ) * TableRowHeight, GUILayout.ExpandWidth( true ) );
          var header = new Rect( table.x, table.y, table.width, TableRowHeight );
          DrawTableRowBackground( header, 0 );
          GUI.Label( Cell( header, 0, IndexColumnWidth ), "Index", m_headerStyle );
          for ( int column = 0; column < fields.Length; ++column )
            GUI.Label( Cell( header, IndexColumnWidth + column * ValueColumnWidth, ValueColumnWidth ),
                       new GUIContent( fields[ column ].Path, fields[ column ].ToString() ), m_headerStyle );

          for ( int row = start; row < end; ++row ) {
            var rect = new Rect( table.x, table.y + ( row - start + 1 ) * TableRowHeight, table.width, TableRowHeight );
            DrawTableRowBackground( rect, row - start + 1 );
            GUI.Label( Cell( rect, 0, IndexColumnWidth ), row.ToString(), m_numberStyle );
            for ( int column = 0; column < fields.Length; ++column )
              EditorGUI.SelectableLabel( Cell( rect, IndexColumnWidth + column * ValueColumnWidth, ValueColumnWidth ),
                                         Convert.ToString( frame.GetValue( row, fields[ column ].Path ), CultureInfo.InvariantCulture ), m_numberStyle );
          }
        }
      }
    }

    private static Rect Cell( Rect row, float offset, float width )
      => new Rect( row.x + offset + 1, row.y + 1, width - 2, row.height - 2 );

    private static void DrawTableRowBackground( Rect rect, int row )
    {
      var dark = EditorGUIUtility.isProSkin;
      var color = row == 0 ? new Color( 0, 0, 0, dark ? 0.3f : 0.12f ) :
                  row % 2 == 0 ? new Color( 0, 0, 0, dark ? 0.12f : 0.04f ) :
                                 new Color( 1, 1, 1, dark ? 0.035f : 0.3f );
      EditorGUI.DrawRect( rect, color );
    }

    private void Connect( OutputSource output )
    {
      Disconnect();
      m_pendingOutput = output.Name;
      m_connecting = true;
      m_message = "Connecting to the lidar output...";
      // Leave OnGUI before initializing the imported hierarchy.
      EditorApplication.delayCall += CompleteConnection;
    }

    private void CompleteConnection()
    {
      m_connecting = false;
      if ( !Application.isPlaying || m_signals == null || !m_signals.gameObject.scene.IsValid() ) {
        Disconnect();
        return;
      }
      try {
        if ( m_signals.GetInitialized<OpenPLXSignals>() == null )
          throw new InvalidOperationException( "OpenPLX signals failed to initialize. Check preceding initialization errors." );
        // Resolve the declaration directly; the reader discovers its window layout.
        m_output = m_signals.FindOutputSource( m_pendingOutput );
        m_reader = new LidarSignalReader( m_output );
        var source = ( (openplx.Sensors.Signals.LidarOutput)m_output.Native ).source();
        var sourceObject = source != null ? m_signals.Root.FindMappedObject( source.getName() ) : null;
        m_sensor = sourceObject != null ? sourceObject.GetComponent<LidarSensor>() : null;
        m_simulation = Simulation.Instance.GetInitialized<Simulation>();
        if ( m_simulation == null )
          throw new InvalidOperationException( "The simulation failed to initialize." );
        m_simulation.StepCallbacks.PostStepForward += ReadNow;
        ReadNow();
      }
      catch ( Exception exception ) {
        Fail( exception );
      }
      Repaint();
    }

    private void ReadNow()
    {
      if ( m_reader == null )
        return;
      if ( m_signals == null || !m_output.IsValid ) {
        Disconnect();
        m_message = "The signal root was destroyed or invalidated. Select another root and Connect.";
        return;
      }
      try {
        var frame = m_reader.Read();
        ++m_readCount;
        m_message = frame.SampleCount == 0 ? "Connected; the current scan is empty." : "Reading lidar signal.";
        UpdatePoints( frame );
        SceneView.RepaintAll();
      }
      catch ( Exception exception ) {
        Fail( exception );
      }
    }

    private void UpdatePoints( LidarSignalFrame frame )
    {
      m_pointCount = 0;
      m_gizmoStatus = string.Empty;
      if ( !m_drawPoints )
        return;
      var position = frame.FindPositionField();
      if ( position == null ) {
        m_gizmoStatus = "No unambiguous 3D position field in this output. Window values are still available.";
        return;
      }
      if ( m_sensor == null ) {
        m_gizmoStatus = "No imported source sensor was found for the point transform. Numeric sampling remains active.";
        return;
      }
      var x = frame.FindField( position + ".x" );
      var y = frame.FindField( position + ".y" );
      var z = frame.FindField( position + ".z" );
      var hit = frame.FindField( "is_hit" );
      var limit = Math.Max( 1, m_maxPoints );
      var capacity = Math.Min( frame.SampleCount, limit );
      if ( m_points.Length < capacity )
        m_points = new Vector3[ capacity ];
      var skip = Math.Max( 1, (int)( ( (long)frame.SampleCount + limit - 1 ) / limit ) );
      var transform = m_sensor.GlobalTransform;
      for ( int index = 0; index < frame.SampleCount && m_pointCount < capacity; index += skip ) {
        if ( hit != null && frame.GetNumber( index, hit ) == 0 )
          continue;
        var point = new Vector3( -(float)frame.GetNumber( index, x ), (float)frame.GetNumber( index, y ), (float)frame.GetNumber( index, z ) );
        if ( Finite( point.x ) && Finite( point.y ) && Finite( point.z ) )
          m_points[ m_pointCount++ ] = transform.MultiplyPoint3x4( point );
      }
    }

    private void DrawScenePoints( SceneView sceneView )
    {
      if ( !Application.isPlaying || !sceneView.drawGizmos || !m_drawPoints || m_reader == null || Event.current.type != EventType.Repaint )
        return;
      using ( new Handles.DrawingScope( m_pointColor, Matrix4x4.identity ) ) {
        for ( int i = 0; i < m_pointCount; ++i )
          Handles.SphereHandleCap( 0, m_points[ i ], Quaternion.identity, 2 * Mathf.Max( 0.001f, m_pointRadius ), EventType.Repaint );
      }
    }

    private void Fail( Exception exception )
    {
      Disconnect();
      m_message = exception.Message;
      Debug.LogException( exception, this );
    }

    private void Disconnect()
    {
      EditorApplication.delayCall -= CompleteConnection;
      m_connecting = false;
      if ( m_simulation != null )
        m_simulation.StepCallbacks.PostStepForward -= ReadNow;
      m_simulation = null;
      m_reader = null;
      m_output = null;
      m_sensor = null;
      m_readCount = 0;
      m_pointCount = 0;
      m_points = Array.Empty<Vector3>();
      m_gizmoStatus = string.Empty;
      m_frozenWindow = null;
      m_displayWindow = null;
      m_page = 0;
      m_scroll = Vector2.zero;
      m_message = "Choose a lidar output and Connect.";
      SceneView.RepaintAll();
    }

    private static bool Finite( float value ) => !float.IsNaN( value ) && !float.IsInfinity( value );

    private static bool IsLidarOutput( OutputSource output )
      => output != null && ( output.Native is openplx.Sensors.Signals.LidarOutput ||
                             typeof( openplx.Sensors.Signals.LidarOutput ).IsAssignableFrom( output.Type ) );
  }
}
