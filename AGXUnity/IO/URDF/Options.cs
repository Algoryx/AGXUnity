using UnityEngine;

namespace AGXUnity.IO.URDF
{
  public static class Options
  {
    /// <summary>
    /// True if loaded Collada (.dae) models has been transformed
    /// by Unity that has to be reverted.
    /// </summary>
    public static bool TransformCollada { get; set; } = true;

    /// <summary>
    /// The Unity URDF importer includes a Collada asset processor that
    /// transforms Collada assets in the project. This means that we have
    /// to take into account if that package is installed and "undo" the
    /// additional transformations made by that asset processor.
    ///
    /// In the Editor, this property will detect if the importer is installed
    /// but it's possible to override this option by setting the desired value.
    /// </summary>
    public static bool UnityURDFImporterInstalled
    {
      get
      {
        if ( Application.isEditor && s_unityUrdfImporterInstalled == null )
          s_unityUrdfImporterInstalled = Environment.IsEditorPackageInstalled( "com.unity.robotics.urdf-importer" );

        return s_unityUrdfImporterInstalled ?? false;
      }
      set
      {
        s_unityUrdfImporterInstalled = value;
      }
    }

    private static bool? s_unityUrdfImporterInstalled;
  }
}
