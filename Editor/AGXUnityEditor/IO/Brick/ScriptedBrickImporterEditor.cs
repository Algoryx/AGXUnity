using AGXUnity;
using AGXUnity.IO.BrickIO;
using AGXUnityEditor.UIElements;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AGXUnityEditor.IO.BrickIO
{
  [CustomEditor( typeof( ScriptedBrickImporter ) )]
  public class ScriptedBrickImporterEditor : ScriptedImporterEditor
  {
    private ScriptedBrickImporter ScriptedBrickImporter => target as ScriptedBrickImporter;

    private VisualElement TableRowUI( ScriptedBrickImporter.Error err )
    {
      var row = new VisualElement();
      row.SetPadding( 3, 3, 3, 0 );
      row.RegisterCallback<MouseDownEvent>( mde => AssetDatabase.OpenAsset( ScriptedBrickImporter, err.line ) );

      var flex = new VisualElement();
      flex.style.flexDirection = FlexDirection.Row;
      flex.style.justifyContent = Justify.SpaceBetween;
      flex.style.flexGrow = 1;

      var StatusIcon = new VisualElement( );
      StatusIcon.style.height = 16;
      StatusIcon.style.width = 16;
      StatusIcon.AddToClassList( HelpBox.iconErrorUssClassName );

      var nameLabel = new Label( err.message );
      nameLabel.style.flexGrow = 0.8f;
      nameLabel.style.flexBasis = 0;
      nameLabel.style.overflow = Overflow.Hidden;
      nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
      nameLabel.style.marginRight = 2;

      var pathLabel = new Label( err.Location );
      pathLabel.style.flexGrow = 0.2f;
      pathLabel.style.flexBasis = 0;
      pathLabel.style.overflow = Overflow.Hidden;
      pathLabel.style.unityTextAlign = TextAnchor.MiddleRight;
      pathLabel.style.marginRight = 2;


      flex.Add( StatusIcon );
      flex.Add( nameLabel );
      flex.Add( pathLabel );
      row.Add( flex );

      return row;
    }

    public override VisualElement CreateInspectorGUI()
    {
      var ve = new VisualElement();
      ve.SetPadding( 10, 0, 0, 0 );
      ve.Add( new PropertyField( serializedObject.FindProperty( "SkipImport" ) ) );
      var skipImport = serializedObject.FindProperty( "SkipImport" ).boolValue;
      var skipContainer = new VisualElement();
      skipContainer.SetEnabled( !skipImport );
      skipContainer.Add( new PropertyField( serializedObject.FindProperty( "HideImportedMeshes" ) ) );
      skipContainer.Add( new PropertyField( serializedObject.FindProperty( "HideImportedVisualMaterials" ) ) );
      skipContainer.Add( new PropertyField( serializedObject.FindProperty( "IgnoreDisabledMeshes" ) ) );
      if ( !skipImport ) {

        if ( ScriptedBrickImporter.Errors.Count > 0 ) {
          skipContainer.Add( new Label() { text = $"<b>Errors ({ScriptedBrickImporter.Errors.Count})</b>" } );
          var errors = new VisualElement();
          errors.SetMargin( 5, 0, 0, 0 );
          errors.SetBorder( 2, Color.Lerp( InspectorGUI.BackgroundColor, Color.black, 0.2f ) );
          errors.SetBorderRadius( 5 );
          errors.SetPadding( 5 );
          foreach ( var error in ScriptedBrickImporter.Errors )
            errors.Add( TableRowUI( error ) );
          skipContainer.Add( errors );
        }
        else {
          skipContainer.Add( new Label() { text = "<b>Import statistics:</b>" } );
          var statistics = new VisualElement();
          statistics.SetPadding( 0, 0, 0, 10 );
          statistics.Add( new Label() { text = $"- Model was imported in {ScriptedBrickImporter.ImportTime:F2}s" } );
          var assets = AssetDatabase.LoadAllAssetsAtPath(ScriptedBrickImporter.assetPath);
          statistics.Add( new Label() { text = $"- Imported meshes: {assets.Count( a => a is Mesh )}" } );
          statistics.Add( new Label() { text = $"- Imported visual materials: {assets.Count( a => a is Material )}" } );
          statistics.Add( new Label() { text = $"- Imported shape materials: {assets.Count( a => a is ShapeMaterial )}" } );
          statistics.Add( new Label() { text = $"- Imported Contact materials: {assets.Count( a => a is ContactMaterial )}" } );
          skipContainer.Add( statistics );
        }
      }
      var deps = BrickImporter.FindDependencies(ScriptedBrickImporter.assetPath);
      if ( deps.Length > 0 ) {
        skipContainer.Add( new Label() { text = $"<b>Dependencies ({deps.Length})</b>" } );
        foreach ( var dep in deps ) {
          skipContainer.Add( new Label() { text = dep } );
        }
      }
      ve.Add( skipContainer );
      ve.Add( new IMGUIContainer( () => base.ApplyRevertGUI() ) );
      return ve;
    }
  }
}