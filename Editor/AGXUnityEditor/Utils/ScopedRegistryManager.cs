using System.Linq;
using System.Reflection;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

public static class ScopedRegistryManager
{
  private static Request<RegistryInfo[]> s_request = null;
  public static void RequestRegistryListRefresh()
  {
    var getMethod = typeof( Client ).GetMethod( "GetRegistries", BindingFlags.Static | BindingFlags.NonPublic, null, new System.Type[]{}, null );
    s_request = (Request<RegistryInfo[]>)getMethod.Invoke( null, null );
  }

  public static RegistryInfo[] GetRegistryInfos()
  {
    if ( s_request == null )
      RequestRegistryListRefresh();

    var resultAccessor = typeof(Request<RegistryInfo[]>).GetProperty("Result",BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty);
    return (RegistryInfo[])resultAccessor.GetValue( s_request );
  }

  public static void AddOrUpdateScopedRegistry( string name, string url, string[] scopes )
  {
    var assembly = typeof(UnityEditor.AssetDatabase).Assembly;
    var type = assembly.GetType( "UnityEditor.PackageManager.UI.Internal.ServicesContainer" );
    var instanceAccessor =  type.GetProperty( "instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.GetProperty | BindingFlags.FlattenHierarchy );
    var container = instanceAccessor.GetValue(null);

    var clientType = assembly.GetType("UnityEditor.PackageManager.UI.Internal.UpmRegistryClient");

    var client = container.GetType().GetMethod( "Resolve", BindingFlags.Public | BindingFlags.Instance ).MakeGenericMethod( new System.Type[] { clientType } ).Invoke( container, new object[] {} );

    var infos = GetRegistryInfos();
    if ( infos.Any( info => info.name == name ) ) {
      var updateMethod = clientType.GetMethod(
        "UpdateRegistry",
        BindingFlags.Instance | BindingFlags.Public,
        null,
        new System.Type[] { typeof( string ), typeof( string ), typeof( string ), typeof( string[] ) },
        null );
      updateMethod.Invoke( client, new object[] { (object)name, (object)name, (object)url, (object)scopes } );
    }
    else {
      var addMethod = clientType.GetMethod(
        "AddRegistry",
        BindingFlags.Instance | BindingFlags.Public,
        null,
        new System.Type[] { typeof( string ), typeof( string ), typeof( string[] ) },
        null );
      addMethod.Invoke( client, new object[] { (object)name, (object)url, (object)scopes } );
    }
    RequestRegistryListRefresh();
  }
}
