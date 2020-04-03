using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;

namespace AGXUnityEditor.Web
{
  public static class HttpRequestHandler
  {
    public enum RequestStatus
    {
      Success,
      Error
    }

    public static bool Create( string url, Action<string, RequestStatus> callback )
    {
      if ( string.IsNullOrEmpty( url ) || callback == null || Contains( callback ) )
        return false;

      try {
        var requestData              = new RequestData();
        requestData.WebRequest       = WebRequest.Create( url );
        requestData.ResponseTask     = requestData.WebRequest.GetResponseAsync();
        requestData.ResponseCallback = callback;

        s_requests.Add( requestData );

        OnAddRemoveRequest();
      }
      catch ( Exception e ) {
        Debug.LogException( e );
        return false;
      }

      return true;
    }

    public static void Abort( Action<string, RequestStatus> callback )
    {
      if ( callback == null || !Contains( callback ) )
        return;

      var requestData = s_requests.Find( rd => rd.ResponseCallback == callback );
      requestData.WebRequest.Abort();

      s_requests.Remove( requestData );

      OnAddRemoveRequest();
    }

    public static bool Contains( Action<string, RequestStatus> callback )
    {
      return s_requests.Any( requestData => requestData.ResponseCallback == callback );
    }

    private static void Update()
    {
      foreach ( var requestData in s_requests.ToArray() ) {
        if ( !requestData.ResponseTask.IsCompleted )
          continue;

        var response = string.Empty;
        var status = RequestStatus.Success;
        try {
          using ( var responseStream = requestData.ResponseTask.Result.GetResponseStream() )
            response = ( new StreamReader( responseStream ) ).ReadToEnd();
        }
        catch ( Exception e ) {
          Debug.LogException( e );
          status = RequestStatus.Error;
        }

        requestData.ResponseCallback( response, status );

        s_requests.Remove( requestData );

        OnAddRemoveRequest();
      }
    }

    private static void OnAddRemoveRequest()
    {
      if ( s_requests.Count > 0 && !s_initialized ) {
        EditorApplication.update += Update;
        s_initialized = true;
      }
      else if ( s_requests.Count == 0 && s_initialized ) {
        s_initialized = false;
        EditorApplication.update -= Update;
      }
    }

    private class RequestData
    {
      public WebRequest WebRequest = null;
      public Task<WebResponse> ResponseTask = null;
      public Action<string, RequestStatus> ResponseCallback = null;
    }

    private static List<RequestData> s_requests = new List<RequestData>();
    private static bool s_initialized = false;
  }
}
