using UnityEngine;
namespace AGXUnity.Utils
{
  [DisallowMultipleComponent]
  public class Plot : ScriptComponent
  {
    /// <summary>
    /// Native AGXPlot.System object.
    /// </summary>
    public agxPlot.System Native { get; private set; } = null;

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
    /// Full path to save file to.
    /// </summary>
    [SerializeField]
    [Tooltip("Where to save plot data. Root directory is Assets folder.")]
    private string m_filePath = string.Empty;

    /// <summary>
    /// Full path to save file to.
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
    private bool m_AutomaticallyOpenPlotWindow = false;

    /// <summary>
    /// Full path to save file to.
    /// </summary>
    public bool ForceFileOverWrite
    {
      get { return m_ForceFileOverWrite; }
      set
      {
        m_ForceFileOverWrite = value;
      }
    }

    /// <summary>
    /// Toggle if plot window should open on start.
    /// </summary>
    [SerializeField]
    private bool m_ForceFileOverWrite = false;

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
      Native = GetSimulation().getPlotSystem();
      if (AutomaticallyOpenPlotWindow)
        OpenPlotWindow();
      if (WritePlotToFile)
      {
        FilePath = Application.dataPath + "/" + FilePath + ".csv";

        if (FilePath == string.Empty)
        {
          UnityEngine.Debug.LogWarning("WritePlotToFile is set but no path is specified. Select path.");
        }

        if (System.IO.File.Exists(FilePath) && !ForceFileOverWrite)
        {
          UnityEngine.Debug.LogWarning("File already exists in specified path and Force File Overwrite is disabled. Proceeding without writing to file");
        }
        else
        {
          Native.add(new agxPlot.FilePlot(FilePath));
        }
      }

      return true;
    }

    /// <summary>
    /// Create a plot from two data series.
    /// </summary>
    /// <param name="xSeries">agxPlot.Data Series for x-axis.</param>
    /// <param name="ySeries">agxPlot.Data Series for y-axis.</param>
    /// <param name="name">Plot name.</param>
    /// <param name="legend">Legend for what is being plotted.</param>
    public void CreatePlot(agxPlot.DataSeries xSeries, agxPlot.DataSeries ySeries, string name, string legend, Color color)
    {
      agx.Vec4 curveColor;
      System.Random random = new System.Random();
      curveColor = new agx.Vec4(color.r, color.g, color.b, 1);
      agxPlot.Curve plotCurve = new agxPlot.Curve(xSeries, ySeries, legend);
      plotCurve.setColor(curveColor);
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
    public void CreatePlot(DataSeries xSeries, DataSeries ySeries, string name, string legend, Color color)
    {
      agx.Vec4 curveColor;
      System.Random random = new System.Random();
      GetInitialized<AGXUnity.Utils.Plot>();
      curveColor = new agx.Vec4(color.r, color.g, color.b, 1);
      agxPlot.Curve plotCurve = new agxPlot.Curve(xSeries.GetInitialized<AGXUnity.Utils.DataSeries>().Native, ySeries.GetInitialized<AGXUnity.Utils.DataSeries>().Native, legend);
      plotCurve.setColor(curveColor);
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
  };
}
