using System.Linq;
using System.Xml.Linq;
using UnityEngine;

namespace AGXUnity.IO.URDF
{
  /// <summary>
  /// Fundamental element "link" defining visual en physical properties of a
  /// link in a model/robot.
  /// This element reads:
  ///   - Required attribute "name".
  ///   - Optional element "inertial" (<see cref="Inertial"/>).
  ///   - Optional element(s) "visual" - multiple supported (<see cref="Visual"/>).
  ///   - Optional element(s) "collision" - multiple supported (<see cref="Collision"/>).
  /// </summary>
  [DoNotGenerateCustomEditor]
  public class Link : Element
  {
    public enum InterpretedType
    {
      World,
      Static,
      Dynamic
    }

    /// <summary>
    /// Inertial of this link. Null if not defined for this link.
    /// </summary>
    public Inertial Inertial { get { return m_inertial; } private set { m_inertial = value; } }

    /// <summary>
    /// Array of visuals, empty array if none defined for this link.
    /// </summary>
    public Visual[] Visuals
    {
      get
      {
        if ( m_visuals == null )
          m_visuals = new Visual[] { };
        return m_visuals;
      }
      private set { m_visuals = value; }
    }

    /// <summary>
    /// Array of collisions, empty array if none defined for this link.
    /// </summary>
    public Collision[] Collisions
    {
      get
      {
        if ( m_collisions == null )
          m_collisions = new Collision[] { };
        return m_collisions;
      }
      private set { m_collisions = value; }
    }

    /// <summary>
    /// Interpreted type of this link given its content, e.g., "world"
    /// when the element is empty, "static" when "inertial" isn't defined
    /// or inertial mass is zero - otherwise "dynamic".
    /// </summary>
    public InterpretedType InterpretedAs { get { return m_interpredType; } private set { m_interpredType = value; } }

    /// <summary>
    /// True if this link doesn't have any child elements, otherwise false.
    /// </summary>
    [HideInInspector]
    public bool IsWorld { get { return m_interpredType == InterpretedType.World; } }

    /// <summary>
    /// True if "inertial" is defined and mass is zero, otherwise false.
    /// </summary>
    [HideInInspector]
    public bool IsStatic { get { return m_interpredType == InterpretedType.Static; } }

    /// <summary>
    /// Reads element "link" with required attribute "name".
    /// </summary>
    /// <param name="element">Optional element "link".</param>
    /// <param name="optional">Unused.</param>
    public override void Read( XElement element, bool optional = true )
    {
      if ( element == null )
        return;

      base.Read( element, false );

      Inertial      = Inertial.ReadOptional( element.Element( "inertial" ) );
      Visuals       = ( from visualElement in element.Elements( "visual" ) select Instantiate<Visual>( visualElement ) ).ToArray();
      Collisions    = ( from collisionElement in element.Elements( "collision" ) select Instantiate<Collision>( collisionElement ) ).ToArray();
      InterpretedAs = !element.HasElements ?
                        InterpretedType.World :
                      Inertial != null && Inertial.Mass == 0.0f ?
                        InterpretedType.Static :
                        InterpretedType.Dynamic;
    }

    /// <summary>
    /// Construct given optional element "link".
    /// </summary>
    /// <param name="element">Optional element "link".</param>
    public Link( XElement element )
    {
      Read( element );
    }

    [SerializeField]
    private Inertial m_inertial = null;

    [SerializeField]
    private Visual[] m_visuals = new Visual[] { };

    [SerializeField]
    private Collision[] m_collisions = new Collision[] { };

    [SerializeField]
    private InterpretedType m_interpredType = InterpretedType.World;
  }
}
