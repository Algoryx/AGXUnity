using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;
using UnityEditor;

using AGXUnity.Utils;

namespace AGXUnityEditor.IO
{
  public class DirectoryContentHandler
  {
    /// <summary>
    /// Delete all content that's not user specific or possibly loaded
    /// by the editor (dll files).
    /// </summary>
    /// <returns>True if no exception - otherwise false.</returns>
    public static bool DeleteContent()
    {
      try {
        var handler = new DirectoryContentHandler( Utils.AGXUnityPackageDirectory );
        handler.DeleteAllCollected();
      }
      catch ( System.Exception ) {
        return false;
      }

      return true;
    }

    /// <summary>
    /// Root directory to parse files and directories from.
    /// </summary>
    public DirectoryInfo RootDirectory { get; private set; } = null;

    /// <summary>
    /// All files from RootDirectory that may be deleted.
    /// </summary>
    public FileInfo[] Files { get { return m_files.ToArray(); } }

    /// <summary>
    /// Files that are ignored in the files to may be deleted collection.
    /// </summary>
    public FileInfo[] IgnoredFiles { get { return m_ignoredFiles.ToArray(); } }

    /// <summary>
    /// Directories that may be deleted due to empty contents.
    /// </summary>
    public DirectoryInfo[] Directories
    {
      get
      {
        return ( from directory in m_directories.ToList()
                 where MayDelete( directory )
                 select directory ).ToArray();
      }
    }

    /// <summary>
    /// All directories with collected files.
    /// </summary>
    public DirectoryInfo[] AllDirectories { get { return m_directories.ToArray(); } }

    public DirectoryContentHandler( string rootDirectory )
      : this( new DirectoryInfo( rootDirectory ) )
    {
    }

    public DirectoryContentHandler( DirectoryInfo rootDirectory )
    {
      if ( rootDirectory == null )
        throw new System.ArgumentNullException( "rootDirectory" );
      if ( !rootDirectory.Exists )
        throw new DirectoryNotFoundException( rootDirectory.FullName + " doesn't exists." );

      RootDirectory = rootDirectory;

      CollectData( RootDirectory );
    }

    public void DeleteAllCollected()
    {
      EditorApplication.LockReloadAssemblies();

      Debug.Log( "    - Ignoring delete of files:" );
      foreach ( var file in m_ignoredFiles )
        Debug.Log( $"        {file.FullName}" );

      Debug.Log( $"    - Deleting {m_files.Count} files..." );
      foreach ( var file in m_files ) {
        try {
          file.Delete();
        }
        catch ( System.Exception e ) {
          Debug.LogError( $"        Unable to remove file: {file.FullName}\n                {e.Message}" );
        }
      }

      var directories = Directories;
      Debug.Log( $"    - Deleting {directories.Length} directories..." );
      foreach ( var directory in directories ) {
        var directoryPath = directory.FullName.PrettyPath();
        if ( Directory.Exists( directoryPath ) && MayDelete( directory ) ) {
          try {
            Directory.Delete( directoryPath, true );
          }
          catch ( System.Exception e ) {
            Debug.LogError( $"        Unable to remove directory: {directory.FullName}\nIs it open in File Explorer or cmd?\n\n" +
                            $"        {e.Message}" );
          }
        }
      }

      EditorApplication.UnlockReloadAssemblies();
    }

    private void CollectData( DirectoryInfo currentDirectory )
    {
      try {
        var files = currentDirectory.GetFiles( "*.*" );
        foreach ( var file in files ) {
          if ( MayDelete( file ) )
            m_files.Add( file );
          else
            AddIgnoredFile( file );
        }

        foreach ( var directory in currentDirectory.GetDirectories() ) {
          m_directories.Add( directory );
          CollectData( directory );
        }
      }
      catch ( System.UnauthorizedAccessException ) {
      }
      catch ( DirectoryNotFoundException ) {
      }
      catch ( System.Exception e ) {
        Debug.LogException( e );
      }
    }

    private bool MayDelete( FileInfo file )
    {
      var ignoreFile = file.Name.Contains( "agx.lic" ) ||
                       ( file.Name.Contains( "Data.asset" ) && file.Directory.Name == "Data" ) ||
                       ( file.Name.Contains( "Settings.asset" ) && file.Directory.Name == "Data" ) ||
                       ( file.Name.Contains( "AGXInitData.asset" ) && file.Directory.Name == "Data" ) ||
                       ( file.Extension == ".dll" || file.Name.EndsWith( ".dll.meta" ) ) ||
                       ( file.Directory.Name == "AGXUnityUpdateHandler" || file.Directory.Parent.Name == "AGXUnityUpdateHandler" );
      return !ignoreFile;
    }

    private bool MayDelete( DirectoryInfo directory )
    {
      return !m_ignoreDirectories.Contains( directory );
    }

    private void AddIgnoredFile( FileInfo file )
    {
      m_ignoredFiles.Add( file );
      m_ignoreDirectories.Add( file.Directory );
      var parent = file.Directory.Parent;
      while ( parent != null && parent.FullName != RootDirectory.FullName ) {
        m_ignoreDirectories.Add( parent );
        parent = parent.Parent;
      }
    }

    class DistinctFileSystemInfoComparer : IEqualityComparer<FileSystemInfo>
    {
      public bool Equals( FileSystemInfo x, FileSystemInfo y )
      {
        return x.FullName == y.FullName;
      }

      public int GetHashCode( FileSystemInfo obj )
      {
        return obj.FullName.GetHashCode();
      }
    }

    class DirectoryFullNameMaxLengthComparer : IComparer<DirectoryInfo>
    {
      public int Compare( DirectoryInfo x, DirectoryInfo y )
      {
        return x.FullName.Length < y.FullName.Length ? 1 : x.FullName.Length > y.FullName.Length ? -1 : 0;
      }
    }

    private List<FileInfo> m_files = new List<FileInfo>();
    private HashSet<FileInfo> m_ignoredFiles = new HashSet<FileInfo>( new DistinctFileSystemInfoComparer() );
    private List<DirectoryInfo> m_directories = new List<DirectoryInfo>();
    private HashSet<DirectoryInfo> m_ignoreDirectories = new HashSet<DirectoryInfo>( new DistinctFileSystemInfoComparer() );
  }
}
