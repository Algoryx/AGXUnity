using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AGXUnity.IO;
using AGXUnity.Utils;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( RestoredAssetsRoot ) )]
  public class RestoredAssetsRootTool : CustomTargetTool
  {
    public RestoredAssetsRootTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnAdd()
    {
      foreach ( var root in GetTargets<RestoredAssetsRoot>() ) {
        var dataDirectory = Path.GetDirectoryName( AssetDatabase.GetAssetPath( root ) );
        m_assets.Add( ( from asset in IO.ObjectDb.GetAssets( dataDirectory, root.Type )
                        where !( asset is RestoredAssetsRoot )
                        select asset ).ToArray() );
      }
    }

    public override void OnPreTargetMembersGUI()
    {
      for ( int i = 0; i < Targets.Length; ++i ) {
        var root = Targets[ i ] as RestoredAssetsRoot;
        InspectorGUI.ToolArrayGUI( this,
                                   m_assets[ i ],
                                   $"[{m_assets[ i ].Length}] {root.Type.ToString().SplitCamelCase()} assets" );
      }
    }

    private List<Object[]> m_assets = new List<Object[]>();
  }
}
