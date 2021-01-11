using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEditor;

namespace AGXUnityEditor.Web
{
  public static class RequestHandler
  {
    public enum Status
    {
      Success,
      Error
    }

    public static bool Get( string url,
                            Action<string, Status> onComplete,
                            Action<float> onProgress = null )
    {
      if ( Contains( onComplete ) )
        return false;

      var data = new StringRequestData()
      {
        WebRequest = UnityWebRequest.Get( url ),
        OnComplete = onComplete,
        OnProgress = onProgress
      };
      data.WebRequest.SendWebRequest();

      Add( data );

      return true;
    }

    public static bool Get( string url,
                            DirectoryInfo directory,
                            Action<FileInfo, Status> onComplete,
                            Action<float> onProgress = null )
    {
      if ( Contains( onComplete ) )
        return false;

      if ( directory == null || !directory.Exists ) {
        Debug.LogWarning( "Unable to request file, target directory is null or doesn't exist." );
        return false;
      }

      var filename = url.Substring( url.LastIndexOf( '/' ) + 1 );

      var data = new FileRequestData()
      {
        WebRequest = new UnityWebRequest( url ),
        OnComplete = onComplete,
        OnProgress = onProgress,
        Target     = directory.FullName +
                     Path.DirectorySeparatorChar +
                     filename
      };
      var fileDownloadHandler = new DownloadHandlerFile( data.Target );
      fileDownloadHandler.removeFileOnAbort = true;
      data.WebRequest.downloadHandler = fileDownloadHandler;
      data.WebRequest.SendWebRequest();

      Add( data );

      return true;
    }

    public static void Abort( Action<string, Status> onComplete )
    {
      Abort( GetData( onComplete ) );
    }

    public static void Abort( Action<FileInfo, Status> onComplete )
    {
      Abort( GetData( onComplete ) );
    }

    public static bool Contains( Action<string, Status> onComplete )
    {
      return GetData( onComplete ) != null;
    }

    public static bool Contains( Action<FileInfo, Status> onComplete )
    {
      return GetData( onComplete ) != null;
    }

    private static void Update()
    {
      foreach ( var data in s_requestData.ToArray() ) {
        if ( !data.WebRequest.isDone ) {
          data.OnProgress?.Invoke( data.WebRequest.downloadProgress );
          continue;
        }

        data.OnProgress?.Invoke( 1.0f );

#if UNITY_2020_2_OR_NEWER
        var status = ( data.WebRequest.result == UnityWebRequest.Result.ProtocolError ||
                       data.WebRequest.result == UnityWebRequest.Result.ConnectionError ) ?
#else
        var status = ( data.WebRequest.isHttpError || data.WebRequest.isNetworkError ) ?
#endif
                       Status.Error :
                       Status.Success;
        if ( status == Status.Error )
          Debug.LogError( data.WebRequest.error );
        if ( data is StringRequestData )
          ( data as StringRequestData ).OnComplete( status == Status.Success ?
                                                      data.WebRequest.downloadHandler.text :
                                                      string.Empty,
                                                    status );
        else if ( data is FileRequestData ) {
          var fileData = data as FileRequestData;
          var target   = new FileInfo( fileData.Target );
          if ( !target.Exists )
            status = Status.Error;
          fileData.OnComplete.Invoke( target, status );
        }
        else {
          Debug.LogWarning( $"Unknown web request: {data.GetType()}" );
        }

        Remove( data );
      }
    }

    private static void Add( RequestData data )
    {
      s_requestData.Add( data );
      OnAddRemoveRequest();
    }

    private static void Remove( RequestData data )
    {
      if ( s_requestData.Remove( data ) )
        OnAddRemoveRequest();
    }

    private static void Abort( RequestData data )
    {
      if ( data == null )
        return;

      data.WebRequest.Abort();

      Remove( data );
    }

    private static void OnAddRemoveRequest()
    {
      if ( s_requestData.Count > 0 && !s_initialized ) {
        EditorApplication.update += Update;
        s_initialized = true;
      }
      else if ( s_requestData.Count == 0 && s_initialized ) {
        s_initialized = false;
        EditorApplication.update -= Update;
      }
    }

    private static StringRequestData GetData( Action<string, Status> onComplete )
    {
      return GetData<StringRequestData>().FirstOrDefault( data => data.OnComplete == onComplete );
    }

    private static FileRequestData GetData( Action<FileInfo, Status> onComplete )
    {
      return GetData<FileRequestData>().FirstOrDefault( data => data.OnComplete == onComplete );
    }

    private static IEnumerable<T> GetData<T>()
      where T : RequestData
    {
      foreach ( var data in s_requestData )
        if ( data is T )
          yield return data as T;
    }

    private class RequestData
    {
      public UnityWebRequest WebRequest = null;
      public Action<float> OnProgress = null;
    }

    private class StringRequestData : RequestData
    {
      public Action<string, Status> OnComplete = null;
    }

    private class FileRequestData : RequestData
    {
      public Action<FileInfo, Status> OnComplete = null;
      public string Target = string.Empty;
    }

    private static List<RequestData> s_requestData = new List<RequestData>();
    private static bool s_initialized = false;
  }
}
