using UnityEngine;
using AGXUnity;

namespace AGXUnity.Utils
{
  public class ConstraintUtils
  {
    public class ConstraintRow
    {
      public ElementaryConstraint ElementaryConstraint { get; private set; }
      public bool Valid { get { return ElementaryConstraint != null && Row >= 0 && Row < ElementaryConstraint.NumRows; } }
      public ElementaryConstraintRowData RowData { get { return Valid ? ElementaryConstraint.RowData[ Row ] : null; } }

      public ConstraintRow( ElementaryConstraint elementaryConstraint, int row )
      {
        ElementaryConstraint = elementaryConstraint;
        Row = row;
      }

      private int Row { get; set; }
    }

    public class ConstraintRowParser
    {
      public enum RowType
      {
        Translational,
        Rotational
      }

      public static ConstraintRowParser Create( Constraint constraint )
      {
        if ( constraint == null || constraint.ElementaryConstraints.Length == 0 )
          return null;

        ConstraintRowParser constraintRowParser = new ConstraintRowParser();
        System.Action<ConstraintRow[], ElementaryConstraint, int, int> AssignRow = ( rows, ec, constraintRow, localRow ) =>
        {
          if ( rows[ constraintRow ] != null )
            throw new Exception( "Row index " + constraintRow + " already assigned: " + rows[ constraintRow ].ElementaryConstraint.NativeName );

          rows[ constraintRow ] = new ConstraintRow( ec, localRow );
        };

        ElementaryConstraint[] ordinaryElementaryConstraints = constraint.GetOrdinaryElementaryConstraints();
        foreach ( ElementaryConstraint ec in ordinaryElementaryConstraints ) {
          if ( ec.NumRows > 1 ) {
            // Only rotational QuatLock ("QL") and translational SphericalRel ("SR") with three rows.
            ConstraintRow[] rows = ec.NativeName == "QL" ? constraintRowParser.RotationalRows :
                                   ec.NativeName == "SW" ? constraintRowParser.RotationalRows :
                                   ec.NativeName == "SR" ? constraintRowParser.TranslationalRows :
                                                           null;
            if ( rows == null )
              throw new Exception( "Unknown elementary constraint with name: " + ec.NativeName );

            // For Swing we should assign rows 0 and 1.
            for ( int row = 0; row < ec.NumRows; ++row )
              AssignRow( rows, ec, row, row );
          }
          else if ( ec.NumRows == 1 ) {
            // Dot2 for single translational row ("D2_U" along U and "D2_V" along V).
            int translationalRow = ec.NativeName == "D2_U" ? 0 :
                                   ec.NativeName == "D2_V" ? 1 :
                                   ec.NativeName == "D2_N" ? 2 :
                                   ec.NativeName == "CN"   ? 2 :
                                                            -1;
            // Dot1 for single rotational row ("D1_VN" about U and "D1_UN" about V ).
            int rotationalRow = ec.NativeName == "D1_VN" ? 0 :
                                ec.NativeName == "D1_UN" ? 1 :
                                ec.NativeName == "D1_UV" ? 2 :
                                                          -1;

            if ( translationalRow < 0 && rotationalRow < 0 )
              throw new Exception( "Unknown single row elementary constraint with name: " + ec.NativeName );

            if ( translationalRow >= 0 )
              AssignRow( constraintRowParser.TranslationalRows, ec, translationalRow, 0 );
            else
              AssignRow( constraintRowParser.RotationalRows, ec, rotationalRow, 0 );
          }
        }

        return constraintRowParser;
      }

      public bool Empty { get { return !HasTranslationalRows && !HasRotationalRows; } }
      public bool HasTranslationalRows { get { return HasEntries( TranslationalRows ); } }
      public bool HasRotationalRows { get { return HasEntries( RotationalRows ); } }

      public ConstraintRow[] TranslationalRows = new ConstraintRow[] { null, null, null };
      public ConstraintRow[] RotationalRows    = new ConstraintRow[] { null, null, null };
      public ConstraintRow[] this[ RowType rowType ] { get { return rowType == RowType.Translational ? TranslationalRows : RotationalRows; } }

      private ConstraintRowParser()
      {
      }

      private bool HasEntries( ConstraintRow[] rows )
      {
        for ( int i = 0; i < rows.Length; ++i )
          if ( rows[ i ] != null )
            return true;
        return false;
      }
    }

    public static string FindName( ElementaryConstraintController controller )
    {
      return controller.GetType().Name.SplitCamelCase();
    }
  }
}
