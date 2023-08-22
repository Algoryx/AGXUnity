using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AGXUnity.Utils
{
  public class Plot : ScriptComponent
  {
    /// <summary>
    /// Native AGXPlot.System object.
    /// </summary>
    public agxPlot.System Native { get { return m_plotSystem; } }

    private agxPlot.System m_plotSystem;

    /// <summary>
    /// Toggle to save plot to file.
    /// </summary>
    [SerializeField]
    private bool m_WritePlotToFile = false;

    /// <summary>
    /// Toggle to save plot to file.
    /// </summary>
    public bool WritePlotToFile
    {
      get { return m_WritePlotToFile; }
      set
      {
        m_WritePlotToFile = value;
      }
    }

    /// <summary>
    /// Path to save file to.
    /// </summary>
    [SerializeField]
    private string m_filePath = string.Empty;

    /// <summary>
    /// Path to save file to.
    /// </summary>
    public string FilePath {
      get { return m_filePath; }
      set
      {
        m_filePath = value;
      }
    }

    /// <summary>
    /// Toggle if plot window should open on start.
    /// </summary>
    [SerializeField]
    private bool m_AutomaticallyOpenPlotWindow = true;

    /// <summary>
    /// Toggle if plot window should open on start.
    /// </summary>
    public bool AutomaticallyOpenPlotWindow
    {
      get { return m_AutomaticallyOpenPlotWindow; }
      set
      {
        m_AutomaticallyOpenPlotWindow = value;
      }
    }

    protected override bool Initialize()
    {
      m_plotSystem = GetSimulation().getPlotSystem();
      if (AutomaticallyOpenPlotWindow)
        OpenPlotWindow();
      if (WritePlotToFile && FilePath != string.Empty)
        Native.add(new agxPlot.FilePlot(FilePath));

      return true;
    }

    /// <summary>
    /// Create a plot from two data series.
    /// </summary>
    /// <param name="xSeries">agxPlot.Data Series for x-axis.</param>
    /// <param name="ySeries">agxPlot.Data Series for y-axis.</param>
    /// <param name="name">Plot name.</param>
    /// <param name="legend">Legend for what is being plotted.</param>
    public void CreatePlot(agxPlot.DataSeries xSeries, agxPlot.DataSeries ySeries, string name, string legend)
    {
      agxPlot.Curve plotCurve = new agxPlot.Curve(xSeries, ySeries);
      agxPlot.Window plotWindow = Native.getOrCreateWindow(name);
      plotWindow.add(plotCurve);
    }

    /// <summary>
    /// Create a plot from two data series.
    /// </summary>
    /// <param name="xSeries">Data Series for x-axis.</param>
    /// <param name="ySeries">Data Series for y-axis.</param>
    /// <param name="name">Plot name.</param>
    /// <param name="legend">Legend for what is being plotted.</param>
    public void CreatePlot(DataSeries xSeries, DataSeries ySeries, string name, string legend)
    {
      GetInitialized<AGXUnity.Utils.Plot>();
      agxPlot.Curve plotCurve = new agxPlot.Curve(xSeries.GetInitialized<AGXUnity.Utils.DataSeries>().Native, ySeries.GetInitialized<AGXUnity.Utils.DataSeries>().Native, legend);
      agxPlot.Window plotWindow = Native.getOrCreateWindow(name);
      plotWindow.add(plotCurve);
    }

    /// <summary>
    /// Opens plot window in web browser.
    /// </summary>
    public void OpenPlotWindow()
    {
      Native.add(new agxPlot.WebPlot(true));
    }

    public void WriteToFile(string path)
    {
      agxPlot.FilePlot fp = new agxPlot.FilePlot(path);
      Native.add(fp);
    }
  };

  #if UNITY_EDITOR
  [CustomEditor(typeof(Plot))]
  public class PlotEditor : Editor
  {
    public override void OnInspectorGUI()
    {
      base.DrawDefaultInspector();

      Plot plot = (Plot)target;

      GUILayout.BeginHorizontal();
      if (GUILayout.Button("Open Plot Window"))
      {
        plot.OpenPlotWindow();
      }
      GUILayout.EndHorizontal();
    }
  }
  #endif
}