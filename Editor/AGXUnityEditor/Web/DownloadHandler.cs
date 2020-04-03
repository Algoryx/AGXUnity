using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.ComponentModel;

using UnityEngine;

namespace AGXUnityEditor.Web
{
  public static class DownloadHandler
  {
    public static void Create( string url,
                               string target,
                               AsyncCompletedEventHandler onComplete,
                               DownloadProgressChangedEventHandler onProgress = null )
    {
      if ( onComplete == null ) {
        Debug.LogError( $"Unable to start download from {url} - onComplete callback not given." );
        return;
      }

      var data = new FileDownloadData()
      {
        Client = new WebClient(),
        OnComplete = onComplete
      };

      data.Client.DownloadFileCompleted += OnDownloadFileComplete;
      if ( onProgress != null )
        data.Client.DownloadProgressChanged += onProgress;

      data.Client.DownloadFileAsync( new System.Uri( url ), target );

      s_fileDownloadData.Add( data );
    }

    public static void Abort( AsyncCompletedEventHandler onComplete )
    {
      var data = s_fileDownloadData.FirstOrDefault( d => d.OnComplete == onComplete );
      if ( data == null )
        return;

      data.Client.CancelAsync();

      s_fileDownloadData.Remove( data );
    }

    public static bool Contains( AsyncCompletedEventHandler onComplete )
    {
      return s_fileDownloadData.Any( data => data.OnComplete == onComplete );
    }

    private static void OnDownloadFileComplete( object sender,
                                                AsyncCompletedEventArgs e )
    {
      var client = sender as WebClient;
      var data = s_fileDownloadData.FirstOrDefault( d => d.Client == client );
      if ( data == null ) {
        Debug.LogWarning( "Unable to find data for completed download." );
        return;
      }

      data.OnComplete.Invoke( sender, e );

      s_fileDownloadData.Remove( data );
    }

    private class FileDownloadData
    {
      public WebClient Client = null;
      public AsyncCompletedEventHandler OnComplete = null;
    }

    private static List<FileDownloadData> s_fileDownloadData = new List<FileDownloadData>();
  }
}
