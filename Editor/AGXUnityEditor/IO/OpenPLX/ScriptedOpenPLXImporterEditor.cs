using AGXUnity;
using AGXUnityEditor.UIElements;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AGXUnityEditor.IO.OpenPLX
{
  [CustomEditor( typeof( ScriptedOpenPLXImporter ) )]
  public class ScriptedOpenPLXImporterEditor : ScriptedImporterEditor
  {
    private ScriptedOpenPLXImporter ScriptedOpenPLXImporter => target as ScriptedOpenPLXImporter;

    private VisualElement TableRowUI( ScriptedOpenPLXImporter.Error err )
    {
      var row = new VisualElement();
      row.SetPadding( 3, 3, 3, 0 );
      row.RegisterCallback<MouseDownEvent>( mde => IO.Utils.OpenFile( err.document != "" ? err.document : ScriptedOpenPLXImporter.assetPath, err.line, err.column ) );

      var flex = new VisualElement();
      flex.style.flexDirection = FlexDirection.Row;
      flex.style.justifyContent = Justify.SpaceBetween;
      flex.style.flexGrow = 1;

      var StatusIcon = new VisualElement( );
      StatusIcon.style.height = 16;
      StatusIcon.style.width = 16;
      StatusIcon.style.flexGrow = 0;
      StatusIcon.style.flexShrink = 0;
      StatusIcon.AddToClassList( HelpBox.iconErrorUssClassName );

      var nameLabel = new Label( err.message );
      nameLabel.style.flexGrow = 1;
      nameLabel.style.flexShrink = 1;
      nameLabel.style.overflow = Overflow.Hidden;
      nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
      nameLabel.style.marginRight = 2;

      var pathLabel = new Label( err.Location );
      pathLabel.style.flexGrow = 0;
      pathLabel.style.flexShrink = 0;
      pathLabel.style.overflow = Overflow.Hidden;
      pathLabel.style.unityTextAlign = TextAnchor.MiddleRight;
      pathLabel.style.marginRight = 2;

      var hoverTint = new Color(1,1,1,1);
      var normalTint = new Color(0.9f,0.9f,0.9f,1.0f);
      var downTint = new Color(0.8f,0.8f,0.8f,1.0f);

      var CopyIcon = new VisualElement();
      CopyIcon.style.height = 16;
      CopyIcon.style.width = 12;
      CopyIcon.style.flexGrow = 0;
      CopyIcon.style.flexShrink = 0;
      CopyIcon.style.backgroundImage = EditorGUIUtility.FindTexture( "Clipboard" );
      CopyIcon.style.unityBackgroundImageTintColor = normalTint;
      CopyIcon.RegisterCallback<MouseDownEvent>( mde => {
        GUIUtility.systemCopyBuffer = err.raw;
        CopyIcon.style.unityBackgroundImageTintColor = downTint;
        mde.StopPropagation();
      } );
      CopyIcon.RegisterCallback<MouseUpEvent>( mde => CopyIcon.style.unityBackgroundImageTintColor = hoverTint );
      CopyIcon.RegisterCallback<MouseOverEvent>( mde => CopyIcon.style.unityBackgroundImageTintColor = hoverTint );
      CopyIcon.RegisterCallback<MouseOutEvent>( mde => CopyIcon.style.unityBackgroundImageTintColor = normalTint );


      flex.Add( StatusIcon );
      flex.Add( nameLabel );
      flex.Add( pathLabel );
      flex.Add( CopyIcon );
      row.Add( flex );

      return row;
    }

    private string GetDocumentPathShort( string documentPath )
    {
      var normalizedDoc = documentPath.Replace("\\","/");
      var normalizedApp = Application.dataPath.Replace("\\","/")+"/";
      if ( normalizedDoc.StartsWith( normalizedApp ) )
        return normalizedDoc.Replace( normalizedApp, "" );
      return normalizedDoc;
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
      skipContainer.Add( new PropertyField( serializedObject.FindProperty( "RotateUp" ) ) );

      if ( !skipImport ) {

        var models = ScriptedOpenPLXImporter.DeclaredModels.ToList();
        models.Add( "Default" );
        var modelSelection = new DropdownField( "Imported Model", models, ScriptedOpenPLXImporter.ImportedModel );
        modelSelection.RegisterValueChangedCallback( ce => ScriptedOpenPLXImporter.ImportedModel = ce.newValue );
        modelSelection.BindProperty( serializedObject.FindProperty( "ImportedModel" ) );
        skipContainer.Add( modelSelection );

        if ( ScriptedOpenPLXImporter.Errors.Count > 0 ) {
          skipContainer.Add( new Label() { text = $"<b>Errors ({ScriptedOpenPLXImporter.Errors.Count})</b>" } );
          var errors = new VisualElement();
          errors.SetMargin( 5, 0, 0, 0 );
          errors.SetBorder( 2, Color.Lerp( InspectorGUI.BackgroundColor, Color.black, 0.2f ) );
          errors.SetBorderRadius( 5 );
          errors.SetPadding( 5, 5, 5, 15 );
          ScriptedOpenPLXImporter.Errors.Sort( ( e1, e2 ) => e1.document.CompareTo( e2.document ) );

          var foldout = new Foldout() { text = GetDocumentPathShort(ScriptedOpenPLXImporter.Errors[0].document), value = true };
          for ( int i = 0; i < ScriptedOpenPLXImporter.Errors.Count; i++ ) {
            var docShort = GetDocumentPathShort( ScriptedOpenPLXImporter.Errors[ i ].document );
            if ( foldout.text != docShort ) {
              errors.Add( foldout );
              foldout = new Foldout() { text = docShort, value = true };
            }
            foldout.Add( TableRowUI( ScriptedOpenPLXImporter.Errors[ i ] ) );
          }
          errors.Add( foldout );
          skipContainer.Add( errors );
        }
        else {
          skipContainer.Add( new Label() { text = "<b>Import statistics:</b>" } );
          var statistics = new VisualElement();
          statistics.SetPadding( 0, 0, 0, 10 );
          statistics.Add( new Label() { text = $"- Model was imported in {ScriptedOpenPLXImporter.ImportTime:F2}s" } );
          var assets = AssetDatabase.LoadAllAssetsAtPath(ScriptedOpenPLXImporter.assetPath);
          statistics.Add( new Label() { text = $"- Imported meshes: {assets.Count( a => a is Mesh )}" } );
          statistics.Add( new Label() { text = $"- Imported visual materials: {assets.Count( a => a is Material )}" } );
          statistics.Add( new Label() { text = $"- Imported shape materials: {assets.Count( a => a is ShapeMaterial )}" } );
          statistics.Add( new Label() { text = $"- Imported Contact materials: {assets.Count( a => a is ContactMaterial )}" } );
          skipContainer.Add( statistics );
        }
      }
      var deps = ScriptedOpenPLXImporter.Dependencies;
      if ( deps.Length > 0 ) {
        var depFoldout = new Foldout(){ text = $"<b>Dependencies ({deps.Length})</b>", value = false };
        int numCore = 0;
        foreach ( var dep in deps ) {
          if ( dep.StartsWith( "Assets/AGXUnity/OpenPLX" ) )
            numCore++;
          else
            depFoldout.Add( new Label() { text = dep } );
        }
        if ( numCore > 0 )
          depFoldout.Add( new Label() { text = $"<b>Core bundle dependencies ({numCore})</b>" } );
        skipContainer.Add( depFoldout );
      }
      ve.Add( skipContainer );
      ve.Add( new IMGUIContainer( () => base.ApplyRevertGUI() ) );
      return ve;
    }
  }
}
