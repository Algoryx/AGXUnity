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
  }
}
