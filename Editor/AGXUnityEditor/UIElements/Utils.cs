using AGXUnity;
using AGXUnityEditor.Editors;
using AGXUnityEditor.Utils;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AGXUnityEditor.UIElements
{
  public static class Extensions
  {
    public static void SetPadding( this VisualElement ve,
                                   StyleLength top,
                                   StyleLength right,
                                   StyleLength bottom,
                                   StyleLength left )
    {
      ve.style.paddingTop = top;
      ve.style.paddingRight = right;
      ve.style.paddingBottom = bottom;
      ve.style.paddingLeft = left;
    }

    public static void SetPadding( this VisualElement ve, StyleLength padding )
    {
      ve.SetPadding( padding, padding, padding, padding );
    }

    public static void SetMargin( this VisualElement ve,
                                  StyleLength top,
                                  StyleLength right,
                                  StyleLength bottom,
                                  StyleLength left )
    {
      ve.style.marginTop = top;
      ve.style.marginRight = right;
      ve.style.marginBottom = bottom;
      ve.style.marginLeft = left;
    }

    public static void SetMargin( this VisualElement ve, StyleLength margin )
    {
      ve.SetMargin( margin, margin, margin, margin );
    }

    public static void SetBorderRadius( this VisualElement ve,
                                        StyleLength tl,
                                        StyleLength tr,
                                        StyleLength br,
                                        StyleLength bl )
    {
      ve.style.borderTopLeftRadius = tl;
      ve.style.borderTopRightRadius = tr;
      ve.style.borderBottomRightRadius = br;
      ve.style.borderBottomLeftRadius = bl;
    }

    public static void SetBorderRadius( this VisualElement ve, StyleLength radius )
    {
      ve.SetBorderRadius( radius, radius, radius, radius );
    }

    public static void SetBorderWidth( this VisualElement ve,
                                       StyleFloat top,
                                       StyleFloat right,
                                       StyleFloat bottom,
                                       StyleFloat left )
    {
      ve.style.borderTopWidth = top;
      ve.style.borderRightWidth = right;
      ve.style.borderBottomWidth = bottom;
      ve.style.borderLeftWidth = left;
    }

    public static void SetBorderWidth( this VisualElement ve, StyleFloat width )
    {
      ve.SetBorderWidth( width, width, width, width );
    }

    public static void SetBorderColor( this VisualElement ve,
                                       StyleColor top,
                                       StyleColor right,
                                       StyleColor bottom,
                                       StyleColor left )
    {
      ve.style.borderTopColor = top;
      ve.style.borderRightColor = right;
      ve.style.borderBottomColor = bottom;
      ve.style.borderLeftColor = left;
    }

    public static void SetBorderColor( this VisualElement ve, StyleColor color )
    {
      ve.SetBorderColor( color, color, color, color );
    }

    public static void SetBorder( this VisualElement ve, StyleFloat width, StyleColor color )
    {
      ve.SetBorderColor( color );
      ve.SetBorderWidth( width );
    }

    public static void AddUnityAlignment( this VisualElement ve )
    {
      ve.AddToClassList( "unity-base-field__aligned" );
    }

    private static bool IsDynamicallyShown( SerializedProperty sp, out MemberInfo member, out bool invert )
    {
      var propertyMember = sp.GetFieldInfo();

      member = null;
      invert = false;

      if ( propertyMember == null )
        return false;

      var showInfo = propertyMember.GetCustomAttribute<DynamicallyShowInInspector>();
      if ( showInfo == null )
        return false;

      invert = showInfo.Invert;

      var bindings =  BindingFlags.Instance |
                        BindingFlags.Static |
                        BindingFlags.Public |
                        BindingFlags.NonPublic;
      if ( showInfo.IsMethod )
        bindings |=   BindingFlags.InvokeMethod;
      else
        bindings |=   BindingFlags.GetField |
                      BindingFlags.GetProperty;

      var members = propertyMember.DeclaringType.GetMember( showInfo.Name, bindings );
      if ( members.Length == 0 ) {
        Debug.LogWarning( $"No member '{showInfo.Name}' found to determine dynamic inspector status for member '{sp.name}', skipping" );
        return false;
      }
      else if ( members.Length > 1 ) {
        Debug.LogWarning( $"Multiple members '{showInfo.Name}' found to determine dynamic inspector status for member '{sp.name}', skipping" );
        return false;
      }

      member = members[ 0 ];
      if ( member.MemberType == MemberTypes.Method ) {
        var method = (MethodInfo)member;
        if ( method.GetParameters().Length != 0 ) {
          Debug.LogWarning( $"Method '{method.Name}', used to dynamically show '{sp.name}', requires parameters, this is not supported, skipping" );
          return false;
        }
        if ( method.ContainsGenericParameters ) {
          Debug.LogWarning( $"Method '{method.Name}', used to dynamically show '{sp.name}', requires type parameters, sthis is not supported, skipping" );
          return false;
        }
      }

      return true;
    }

    public static VisualElement CreateDefaultInspector( this SerializedProperty sp )
    {
      if ( IsDynamicallyShown( sp, out MemberInfo member, out bool invert ) )
        return new DynamicallyShowField( sp, member, invert );
      else
        return new PropertyField( sp );
    }
  }
}
