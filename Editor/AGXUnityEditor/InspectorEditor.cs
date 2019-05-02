using System.Reflection;
using System.ComponentModel;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Utils;

using GUI = AGXUnityEditor.Utils.GUI;

namespace AGXUnityEditor
{
  public class InspectorEditor : Editor
  {
    public static GUISkin Skin = null;

    private struct Result
    {
      public bool Changed;
      public object Value;
    }

    private void OnEnable()
    {
      if ( Skin == null ) {
        Skin                    = EditorGUIUtility.GetBuiltinSkin( EditorSkin.Inspector );
        Skin.label.richText     = true;
        Skin.toggle.richText    = true;
        Skin.button.richText    = true;
        Skin.textArea.richText  = true;
        Skin.textField.richText = true;

        if ( EditorGUIUtility.isProSkin )
          Skin.label.normal.textColor = 204.0f / 255.0f * Color.white;
      }
    }

    private void OnDisable()
    {
    }

    public sealed override void OnInspectorGUI()
    {
      DrawMembersGUI( target );
    }

    private void DrawMembersGUI( object obj )
    {
      // TODO: Handle undo.

      InvokeWrapper[] fieldsAndProperties = InvokeWrapper.FindFieldsAndProperties( obj );
      foreach ( InvokeWrapper wrapper in fieldsAndProperties ) {
        if ( !ShouldBeShownInInspector( wrapper.Member ) )
          continue;

        var result = HandleType( wrapper );
        if ( !result.Changed )
          continue;

        Undo.RecordObjects( targets, "Modification: " + wrapper.Member.Name.SplitCamelCase() );
        foreach ( var targetObject in targets ) {
          wrapper.ConditionalSet( targetObject, result.Value );
          EditorUtility.SetDirty( targetObject );
        }
      }
    }

    private Result HandleType( InvokeWrapper wrapper )
    {
      //EditorGUI.showMixedValue = true;

      Result result = new Result() { Changed = false, Value = null };
      if ( !wrapper.CanRead() )
        return result;

      var type        = wrapper.GetContainingType();
      bool isNullable = false;
      if ( type == typeof( Vector4 ) ) {
        Vector4 valInField = wrapper.Get<Vector4>();
        result.Value = EditorGUILayout.Vector4Field( MakeLabel( wrapper.Member ).text, valInField );
      }
      else if ( wrapper.GetContainingType() == typeof( Vector3 ) ) {
        Vector3 valInField = wrapper.Get<Vector3>();
        GUILayout.BeginHorizontal();
        {
          GUILayout.Label( GUI.MakeLabel( wrapper.Member.Name ) );
          result.Value = EditorGUILayout.Vector3Field( "", valInField );
        }
        GUILayout.EndHorizontal();
      }
      else if ( type == typeof( Vector2 ) ) {
        Vector2 valInField = wrapper.Get<Vector2>();
        result.Value = EditorGUILayout.Vector2Field( MakeLabel( wrapper.Member ).text, valInField );
      }
      else if ( type == typeof( int ) ) {
        int valInField = wrapper.Get<int>();
        result.Value = EditorGUILayout.IntField( MakeLabel( wrapper.Member ), valInField, Skin.textField );
      }
      else if ( type == typeof( bool ) ) {
        bool valInField = wrapper.Get<bool>();
        result.Value = Utils.GUI.Toggle( MakeLabel( wrapper.Member ), valInField, Skin.button, Skin.label );

      }
      else if ( type == typeof( Color ) ) {
        Color valInField = wrapper.Get<Color>();
        result.Value = EditorGUILayout.ColorField( MakeLabel( wrapper.Member ), valInField );
      }
      else if ( type == typeof( DefaultAndUserValueFloat ) ) {
        DefaultAndUserValueFloat valInField = wrapper.Get<DefaultAndUserValueFloat>();

        float newValue = Utils.GUI.HandleDefaultAndUserValue( wrapper.Member.Name, valInField, Skin );
        if ( wrapper.IsValid( newValue ) ) {
          if ( !valInField.UseDefault )
            valInField.Value = newValue;
          result.Value = valInField;
        }
      }
      else if ( type == typeof( DefaultAndUserValueVector3 ) ) {
        DefaultAndUserValueVector3 valInField = wrapper.Get<DefaultAndUserValueVector3>();

        Vector3 newValue = Utils.GUI.HandleDefaultAndUserValue( wrapper.Member.Name, valInField, Skin );
        if ( wrapper.IsValid( newValue ) ) {
          if ( !valInField.UseDefault )
            valInField.Value = newValue;
          result.Value = valInField;
        }
      }
      else if ( type == typeof( RangeReal ) ) {
        RangeReal valInField = wrapper.Get<RangeReal>();

        GUILayout.BeginHorizontal();
        {
          GUILayout.Label( MakeLabel( wrapper.Member ), Skin.label );
          valInField.Min = EditorGUILayout.FloatField( "", (float)valInField.Min, Skin.textField, GUILayout.MaxWidth( 64 ) );
          valInField.Max = EditorGUILayout.FloatField( "", (float)valInField.Max, Skin.textField, GUILayout.MaxWidth( 64 ) );
        }
        GUILayout.EndHorizontal();

        if ( valInField.Min > valInField.Max )
          valInField.Min = valInField.Max;

        result.Value = valInField;
      }
      else if ( type == typeof( string ) && wrapper.CanRead() ) {
        result.Value = EditorGUILayout.TextField( MakeLabel( wrapper.Member ), wrapper.Get<string>(), Skin.textField );
      }
      else if ( type.IsEnum && type.IsVisible && wrapper.CanRead() ) {
        System.Enum valInField = wrapper.Get<System.Enum>();
        result.Value = EditorGUILayout.EnumPopup( MakeLabel( wrapper.Member ), valInField, Skin.button );
      }
      else if ( type == typeof( float ) || type == typeof( double ) ) {
        float valInField = type == typeof( double ) ?
                             System.Convert.ToSingle( wrapper.Get<double>() ) :
                             wrapper.Get<float>();
        FloatSliderInInspector slider = wrapper.GetAttribute<FloatSliderInInspector>();
        if ( slider != null )
          result.Value = EditorGUILayout.Slider( MakeLabel( wrapper.Member ), valInField, slider.Min, slider.Max );
        else
          result.Value = EditorGUILayout.FloatField( MakeLabel( wrapper.Member ), valInField, Skin.textField );
      }
      else if ( typeof( ScriptAsset ).IsAssignableFrom( type ) ||
                type.BaseType == typeof( UnityEngine.Object ) ||
                type.BaseType == typeof( ScriptComponent ) ) {
        bool allowSceneObject         = type == typeof( GameObject ) ||
                                        type.BaseType == typeof( ScriptComponent );
        UnityEngine.Object valInField = wrapper.Get<UnityEngine.Object>();
        bool recursiveEditing         = wrapper.HasAttribute<AllowRecursiveEditing>();
        bool createNewAssetButton     = false;

        if ( recursiveEditing ) {
          var foldoutData = EditorData.Instance.GetData( target as UnityEngine.Object, wrapper.Member.Name );

          GUILayout.BeginHorizontal();
          {
            var objFieldLabel = MakeLabel( wrapper.Member );
            var buttonSize = Skin.label.CalcHeight( objFieldLabel, Screen.width );
            UnityEngine.GUI.enabled = valInField != null;
            foldoutData.Bool = GUILayout.Button( Utils.GUI.MakeLabel( foldoutData.Bool ? "-" : "+" ),
                                                 Skin.button,
                                                 new GUILayoutOption[] { GUILayout.Width( 20.0f ), GUILayout.Height( buttonSize ) } ) ?
                                 // Button clicked - toggle current value.
                                 !foldoutData.Bool :
                                 // If foldout were enabled but valInField has changed to null - foldout will become disabled.
                                 valInField != null && foldoutData.Bool;
            UnityEngine.GUI.enabled = true;
            result.Value = EditorGUILayout.ObjectField( objFieldLabel, valInField, type, allowSceneObject, new GUILayoutOption[] { } );

            if ( typeof( ScriptAsset ).IsAssignableFrom( type ) ) {
              GUILayout.Space( 4 );
              using ( new GUI.ColorBlock( Color.Lerp( UnityEngine.GUI.color, Color.green, 0.1f ) ) )
                createNewAssetButton = GUILayout.Button( GUI.MakeLabel( "New", false, "Create new asset" ),
                                                         GUILayout.Width( 42 ),
                                                         GUILayout.Height( buttonSize ) );
            }
          }
          GUILayout.EndHorizontal();

          if ( GUILayoutUtility.GetLastRect().Contains( Event.current.mousePosition ) && Event.current.type == EventType.MouseDown && Event.current.button == 0 ) {
            foldoutData.Bool = !foldoutData.Bool;
            GUIUtility.ExitGUI();
          }

          if ( foldoutData.Bool ) {
            using ( new Utils.GUI.Indent( 12 ) ) {
              Utils.GUI.Separator();

              GUILayout.Space( 6 );

              GUILayout.Label( Utils.GUI.MakeLabel( "Changes made to this object will affect all objects referencing this asset.",
                                                    Color.Lerp( Color.red, Color.white, 0.25f ),
                                                    true ),
                               new GUIStyle( Skin.textArea ) { alignment = TextAnchor.MiddleCenter } );

              GUILayout.Space( 6 );

              Editor editor = Editor.CreateEditor( result.Value as UnityEngine.Object );
              if ( editor != null )
                editor.OnInspectorGUI();

              Utils.GUI.Separator();
            }
          }
        }
        else
          result.Value = EditorGUILayout.ObjectField( MakeLabel( wrapper.Member ), valInField, type, allowSceneObject, new GUILayoutOption[] { } );

        if ( createNewAssetButton ) {
          var assetName = type.Name.SplitCamelCase().ToLower();
          var path = EditorUtility.SaveFilePanel( "Create new " + assetName, "Assets", "new " + assetName + ".asset", "asset" );
          if ( path != string.Empty ) {
            var info         = new System.IO.FileInfo( path );
            var relativePath = IO.Utils.MakeRelative( path, Application.dataPath );
            var newInstance  = ScriptAsset.Create( type );
            newInstance.name = info.Name;
            AssetDatabase.CreateAsset( newInstance, relativePath + ( info.Extension == ".asset" ? "" : ".asset" ) );
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            result.Value = newInstance;
          }
        }

        isNullable = true;
      }

      result.Changed = UnityEngine.GUI.changed &&
                       ( isNullable || result.Value != null );

      return result;
    }

    public static GUIContent MakeLabel( MemberInfo field )
    {
      GUIContent guiContent = new GUIContent();

      guiContent.text = field.Name.SplitCamelCase();
      object[] descriptions = field.GetCustomAttributes( typeof( DescriptionAttribute ), true );
      if ( descriptions.Length > 0 )
        guiContent.tooltip = ( descriptions[ 0 ] as DescriptionAttribute ).Description;

      return guiContent;
    }

    private static bool ShouldBeShownInInspector( MemberInfo memberInfo )
    {
      if ( memberInfo == null )
        return false;

      // Override hidden in inspector.
      if ( memberInfo.IsDefined( typeof( HideInInspector ), true ) )
        return false;

      // In general, don't show UnityEngine objects unless ShowInInspector is set.
      bool show = memberInfo.IsDefined( typeof( ShowInInspector ), true ) ||
                  !( memberInfo.DeclaringType.Namespace != null && memberInfo.DeclaringType.Namespace.Contains( "UnityEngine" ) );

      return show;
    }
  }
}
