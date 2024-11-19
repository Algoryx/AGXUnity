using AGXUnity.Collide;
using AGXUnity.Utils;
using AGXUnityEditor;
using AGXUnityEditor.UIElements;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AGXUnity.Windows
{
  public class SkinnedMeshConverterWindow : EditorWindow
  {
    private class ConverterEntry : VisualElement
    {
      public ConverterEntry( SerializedObject so, Transform bone, UniqueSelectionSet uniqueSelections )
      {
        var entryField = new SelectableField();
        uniqueSelections.Add( entryField );
        //entryField.RegisterValueChangedCallback( ce => data.BoneDatas[ bone ].Selected = ce.newValue );
        entryField.SetPadding( 0, 0, 0, 4 );
        entryField.SetMargin( 0, 0, 0, 0 );
        entryField.style.flexDirection = FlexDirection.Row;
        entryField.style.justifyContent = Justify.FlexStart;
        entryField.style.alignContent = Align.Center;

        var keys = so.FindProperty( "BoneDatas.m_keys" );
        int idx = 0;
        while ( idx < keys.arraySize && (Object)( keys.GetArrayElementAtIndex( idx ).objectReferenceValue ) != bone )
          idx++;

        entryField.RegisterValueChangedCallback( ce => {
          if ( ce.newValue ) {
            uniqueSelections.FieldSelected( entryField );
          }
        } );

        var boneSP = so.FindProperty($"BoneDatas.m_values").GetArrayElementAtIndex(idx);

        entryField.BindProperty( boneSP.FindPropertyRelative( "Selected" ) );

        var label = new Label( bone.name );
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        entryField.Add( label );
        var shape = new Toggle();
        shape.SetMargin( 1 );
        shape.BindProperty( boneSP.FindPropertyRelative( "GenerateShape" ) );
        shape.SetImage( IconManager.GetIcon( "box_shape_32x32.png" ) );
        var rb = new DependentToggle(shape);
        rb.BindProperty( boneSP.FindPropertyRelative( "GenerateRigidBody" ) );
        rb.SetImage( IconManager.GetIcon( "Algoryx_white_32x32.png" ) );
        rb.SetMargin( 1 );
        var constraint = new DependentToggle(rb);
        constraint.BindProperty( boneSP.FindPropertyRelative( "GenerateConstraint" ) );
        constraint.SetImage( IconManager.GetIcon( "constraint_32x32.png" ) );
        constraint.SetMargin( 1 );

        shape.RegisterValueChangedCallback( ce => rb.value = !ce.newValue ? false : rb.value );
        rb.RegisterValueChangedCallback( ce => constraint.value = !ce.newValue ? false : constraint.value );

        entryField.Add( shape );
        entryField.Add( rb );
        entryField.Add( constraint );

        var constraintType = new EnumField(ConstraintType.LockJoint);
        constraintType.BindProperty( boneSP.FindPropertyRelative( "GeneratedConstraintType" ) );
        constraint.RegisterValueChangedCallback( ce => constraintType.SetEnabled( ce.newValue ) );
        entryField.Add( constraintType );

        var constraintAxis = new EnumField(ConstraintAxis.Z);
        constraintAxis.BindProperty( boneSP.FindPropertyRelative( "GeneratedConstraintAxis" ) );
        constraint.RegisterValueChangedCallback( ce => constraintAxis.SetEnabled( ce.newValue ) );
        entryField.Add( constraintAxis );

        this.contentContainer.Add( entryField );

        if ( bone.transform.childCount > 0 ) {
          var childContainer = new VisualElement();
          childContainer.SetMargin( 0, 0, 0, 3 );
          childContainer.style.flexDirection = FlexDirection.Row;
          childContainer.style.flexGrow = 1;

          var indentMarker = new VisualElement();
          indentMarker.style.backgroundColor = InspectorGUISkin.BrandColor;
          indentMarker.style.width = 2/EditorGUIUtility.pixelsPerPoint;
          indentMarker.SetBorderRadius( 2/EditorGUIUtility.pixelsPerPoint );


          childContainer.Add( indentMarker );
          var subentries = new VisualElement();
          subentries.style.flexGrow = 1;
          for ( int i = 0; i<bone.transform.childCount; i++ )
            subentries.Add( new ConverterEntry( so, bone.GetChild( i ), uniqueSelections ) );
          childContainer.Add( subentries );

          this.contentContainer.Add( childContainer );
        }
      }
    }

    private UniqueSelectionSet m_unqiueSelection;

    private enum ConstraintAxis
    {
      X, Y, Z
    }

    [System.Serializable]
    private class BoneData
    {
      [SerializeField]
      public bool GenerateShape;

      [SerializeField]
      public bool GenerateRigidBody;

      [SerializeField]
      public bool GenerateConstraint;

      [SerializeField]
      public ConstraintType GeneratedConstraintType;

      [SerializeField]
      public ConstraintAxis GeneratedConstraintAxis;

      [SerializeField]
      public bool Selected;

      public BoneData( bool shape = true, bool rb = true, bool constraint = true, ConstraintType constraintType = ConstraintType.LockJoint, ConstraintAxis axis = ConstraintAxis.Z, bool selected = false )
      {
        GenerateShape = shape;
        GenerateRigidBody = rb;
        GenerateConstraint = constraint;
        GeneratedConstraintType = constraintType;
        GeneratedConstraintAxis = axis;
        Selected = selected;
      }
    }

    private class UniqueSelectionSet
    {
      private List<SelectableField> m_fields = new List<SelectableField>();

      public void Add( SelectableField field ) => m_fields.Add( field );

      public void FieldSelected( SelectableField field )
      {
        foreach ( var f in m_fields ) {
          if ( f != field && f.value )
            f.value = false;
        }
      }
    }

    private class SelectableField : BaseBoolField
    {
      private enum SelectionState
      {
        Selected,
        Unselected,
        Hover
      };

      private bool hovering = false;

      public SelectableField( string label = null ) : base( label )
      {
        Clear();
        RegisterCallback<MouseOverEvent>( moe => {
          HandleSelectionState( this.value ? SelectionState.Selected : SelectionState.Hover );
          hovering = true;
        } );
        RegisterCallback<MouseLeaveEvent>( mle => {
          HandleSelectionState( this.value ? SelectionState.Selected : SelectionState.Unselected );
          hovering = false;
        } );
        this.RegisterValueChangedCallback( ce => HandleSelectionState( this.value ? SelectionState.Selected : this.hovering ? SelectionState.Hover : SelectionState.Unselected ) );
      }

      private void HandleSelectionState( SelectionState state )
      {
        if ( state == SelectionState.Unselected )
          style.backgroundColor = Color.clear;
        else if ( state == SelectionState.Selected )
          style.backgroundColor = new Color( 0.5f, 0.5f, 1, 0.6f );
        else if ( state == SelectionState.Hover )
          style.backgroundColor = new Color( 1, 1, 1, 0.1f );
      }

    }

    private class ConverterData : ScriptableObject
    {
      [SerializeField]
      public GameObject ConvertedObject = null;

      [SerializeField]
      public GameObject BoneRoot = null;

      [SerializeField]
      public SerializableDictionary<Transform,BoneData> BoneDatas;



      public bool Validate()
      {
        if ( BoneRoot == null || ConvertedObject == null )
          return false;
        return BoneRoot.transform.IsChildOf( ConvertedObject.transform );
      }

      public void OnConvertedObjectSet()
      {
        if ( !Validate() )
          BoneRoot = null;
        if ( BoneRoot == null && ConvertedObject != null ) {
          List<GameObject> viable = new List<GameObject>();
          for ( int i = 0; i < ConvertedObject.transform.childCount; i++ ) {
            var childGO = ConvertedObject.transform.GetChild(i).gameObject;
            if ( !childGO.TryGetComponent<SkinnedMeshRenderer>( out var _ ) )
              viable.Add( childGO );
          }

          BoneRoot = viable.FirstOrDefault( go => go.name == "root" );
        }
      }

      private bool CompareRotations( Quaternion q1, Quaternion q2 )
      {
        var c2 = q2.ToHandedQuat().conj();
        var z = q1.ToHandedQuat() * c2;
        var angle = 2*Mathf.Acos(Mathf.Abs((float)z.w));
        return angle < 1e-2f;
      }

      private void GenerateBoneDatasHelper( Transform bone )
      {
        ConstraintType cType;
        if ( bone.gameObject == BoneRoot )
          cType = ConstraintType.LockJoint;
        else if ( CompareRotations( bone.rotation, bone.parent.rotation ) )
          cType = ConstraintType.Prismatic;
        else
          cType = ConstraintType.Hinge;
        BoneDatas[ bone ] = new BoneData( true, true, true, cType, cType == ConstraintType.Hinge ? ConstraintAxis.X : ConstraintAxis.Z, false );

        for ( int i = 0; i < bone.childCount; i++ )
          GenerateBoneDatasHelper( bone.GetChild( i ) );
      }

      public void GenerateBoneDatas()
      {
        if ( BoneRoot == null )
          return;

        BoneDatas = new SerializableDictionary<Transform, BoneData>();
        GenerateBoneDatasHelper( BoneRoot.transform );
      }
    }

    [MenuItem( "AGXUnity/Utils/Convert Skinned Model", priority = 80 )]
    public static SkinnedMeshConverterWindow Create()
    {
      var window = GetWindow<SkinnedMeshConverterWindow>( false,
                                                      "Convert skinned model",
                                                      true );
      return window;
    }

    static SkinnedMeshConverterWindow s_window = null;

    private void OnEnable()
    {
      s_window = this;
      SceneView.duringSceneGui += DrawHandles;
    }

    private void OnDisable()
    {
      s_window = null;
      SceneView.duringSceneGui -= DrawHandles;
    }

    ConverterData data;

    class BoneMeshifierUtil
    {
      public Vector3[] Vertices => m_vertices.ToArray();
      public int[] Triangles => m_triangles.ToArray();

      private List<Vector3> m_vertices = new List<Vector3>();
      private List<int> m_triangles = new List<int>();
      private Dictionary<int,int> m_vertexRemap = new Dictionary<int, int>();

      public void AddVertex( Vector3 vert, int originalIndex )
      {
        m_vertexRemap[ originalIndex ] = m_vertices.Count;
        m_vertices.Add( vert );
      }

      public void AddIndex( int index )
      {
        m_triangles.Add( m_vertexRemap[ index ] );
      }

      public Vector3[] GetTransformedVertices( Matrix4x4 transform )
      {
        return m_vertices.Select( v => transform.MultiplyPoint( v ) ).ToArray();
      }
    }

    private void Convert()
    {
      var skinnedMeshes = data.ConvertedObject.GetComponentsInChildren<SkinnedMeshRenderer>();
      foreach ( var skinnedMesh in skinnedMeshes ) {
        var bones = skinnedMesh.bones;
        var weights = skinnedMesh.sharedMesh.boneWeights;
        var vertices = skinnedMesh.sharedMesh.vertices;
        var triangles = skinnedMesh.sharedMesh.triangles;

        var boneMeshifiers = new BoneMeshifierUtil[bones.Length];
        for ( int i = 0; i < bones.Length; i++ )
          boneMeshifiers[ i ] = new BoneMeshifierUtil();

        int[] vertexBoneMap = new int[vertices.Length];

        for ( int i = 0; i < weights.Length; i++ ) {
          Vector3 vertex = vertices[i];
          BoneWeight weight = weights[i];
          int maxI = weight.boneIndex0;
          float maxW = weight.weight0;
          if ( weight.weight1 > maxW ) {
            maxI = weight.boneIndex1;
            maxW = weight.weight1;
          }
          if ( weight.weight2 > maxW ) {
            maxI = weight.boneIndex2;
            maxW = weight.weight2;
          }
          if ( weight.weight3 > maxW ) {
            maxI = weight.boneIndex3;
            maxW = weight.weight3;
          }

          boneMeshifiers[ maxI ].AddVertex( vertex, i );
          vertexBoneMap[ i ] = maxI;
        }

        for ( int i = 0; i < triangles.Length; i += 3 ) {
          var i1 = triangles[i];
          var i2 = triangles[i + 1];
          var i3 = triangles[i + 2];

          var b1 = vertexBoneMap[ i1 ];
          var b2 = vertexBoneMap[ i2 ];
          var b3 = vertexBoneMap[ i3 ];

          if ( b1 != b2 || b1 != b3 )
            continue;

          boneMeshifiers[ b1 ].AddIndex( i1 );
          boneMeshifiers[ b1 ].AddIndex( i2 );
          boneMeshifiers[ b1 ].AddIndex( i3 );
        }

        for ( int i = 0; i < boneMeshifiers.Length; i++ ) {
          var boneData = data.BoneDatas[ bones[ i ] ];
          if ( !boneData.GenerateShape )
            continue;
          if ( !bones[ i ].gameObject.TryGetComponent<AGXUnity.Collide.Mesh>( out var mesh ) ) {
            mesh = bones[ i ].gameObject.AddComponent<AGXUnity.Collide.Mesh>();
            mesh.PrecomputedCollisionMeshes = new CollisionMeshData[ 0 ];
            //mesh.CollisionsEnabled = false;
          }
          var meshData = new CollisionMeshData();
          meshData.Vertices = boneMeshifiers[ i ].GetTransformedVertices( bones[ i ].worldToLocalMatrix );
          meshData.Indices = boneMeshifiers[ i ].Triangles;
          mesh.PrecomputedCollisionMeshes = mesh.PrecomputedCollisionMeshes.Append( meshData ).ToArray();
        }

        if ( !skinnedMesh.rootBone.TryGetComponent<ArticulatedRoot>( out var _ ) )
          skinnedMesh.rootBone.gameObject.AddComponent<ArticulatedRoot>();
        foreach ( var bone in bones ) {
          var boneData = data.BoneDatas[ bone ];
          if ( !bone.TryGetComponent<Constraint>( out var _ ) && boneData.GenerateConstraint ) {
            var constraint = Constraint.CreateInplace( boneData.GeneratedConstraintType, new ConstraintFrame( bone.gameObject ), new ConstraintFrame( bone.parent.gameObject ), bone.gameObject );
            constraint.SetCompliance( 1e-12f );
          }
          if ( !bone.TryGetComponent<RigidBody>( out var _ ) && boneData.GenerateRigidBody )
            bone.gameObject.AddComponent<RigidBody>();
        }
      }
    }

    private void CreateGUI()
    {
      data = ConverterData.CreateInstance<ConverterData>();
      var so = new SerializedObject( data );
      VisualElement ve = new VisualElement();

      var obj = new ObjectField( "Converted model" ) { objectType = typeof(GameObject) };
      obj.BindProperty( so.FindProperty( "ConvertedObject" ) );
      obj.RegisterValueChangedCallback( ce => data.OnConvertedObjectSet() );
      ve.Add( obj );

      var selector = new VisualElement();

      var boneRoot = new ObjectField("Bone Root") { objectType = typeof(GameObject) };
      boneRoot.BindProperty( so.FindProperty( "BoneRoot" ) );
      boneRoot.RegisterValueChangedCallback( ce => {
        selector.Clear();
        if ( ce.newValue != null ) {
          data.GenerateBoneDatas();
          so.Update();
          m_unqiueSelection = new UniqueSelectionSet();
          selector.Add( new ConverterEntry( so, data.BoneRoot.transform, m_unqiueSelection ) );
        }
      } );
      ve.Add( boneRoot );

      ve.Add( selector );

      var convertButton = new Button(){ text = "Convert" };
      convertButton.clicked += Convert;
      ve.Add( convertButton );

      rootVisualElement.Add( ve );
    }

    internal static bool IsHovering( int controlID, Event evt )
    {
      return controlID == HandleUtility.nearestControl && GUIUtility.hotControl == 0 && !Tools.viewToolActive;
    }

    internal static void HingeHandleCap( int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType )
    {
      switch ( eventType ) {
        case EventType.MouseMove:
        case EventType.Layout: {
            Vector3 vector2 = rotation * Vector3.forward / 2.0f;
            HandleUtility.AddControl( controlID, HandleUtility.DistanceToLine( position - vector2 * ( size * 0.9f ), position + vector2 * ( size * 0.9f ) ) );
            HandleUtility.AddControl( controlID, HandleUtility.DistanceToDisc( position, vector2, size * 0.1f ) );
            break;
          }
        case EventType.Repaint: {
            Vector3 vector = rotation * Vector3.forward;
            float num = Handles.lineThickness;
            float num2 = size * 0.1f;
            if ( IsHovering( controlID, Event.current ) ) {
              num += 1.0f;
              num2 *= 1.05f;
            }

            Camera current = Camera.current;
            Vector3 lhs = ((current != null) ? current.transform.forward : (-vector));
            bool flag = Vector3.Dot(lhs, vector) < 0f;
            var s = position - vector * size / 2;
            var e = position + vector * size / 2;
            if ( flag ) {
              Handles.DrawLine( s, e, num );
              Handles.DrawWireDisc( position, vector, num2 );
            }
            else {
              Handles.DrawWireDisc( position, vector, num2 );
              Handles.DrawLine( s, e, num );
            }

            break;
          }
      }
    }

    private void DrawHandles( SceneView view )
    {
      if ( s_window == null || s_window.data == null )
        return;
      foreach ( var (bone, data) in s_window.data.BoneDatas ) {
        if ( !data.GenerateConstraint )
          continue;
        var pos = bone.position;
        var axis = data.GeneratedConstraintAxis switch
        {
          ConstraintAxis.Z => bone.forward,
          ConstraintAxis.X => bone.right,
          ConstraintAxis.Y => bone.up,
          _ => bone.forward
        };

        var preCol = Handles.color;
        float scale = 1.0f;
        if ( data.Selected ) {
          scale = 1.25f;
          Handles.color = Color.green;
        }
        bool preControl = GUIUtility.hotControl == 0;
        if ( data.GeneratedConstraintType == ConstraintType.Hinge ) {
          Handles.Slider( pos, -axis, HandleUtility.GetHandleSize( pos ), HingeHandleCap, -1.0f );
        }
        else if ( data.GeneratedConstraintType == ConstraintType.Prismatic ) {
          var p1 = pos + axis/2 * scale;
          Handles.Slider( pos, axis );
        }
        if ( preControl && GUIUtility.hotControl != 0 )
          data.Selected = true;
        Handles.color = preCol;
      }
    }


    [DrawGizmo( GizmoType.NotInSelectionHierarchy | GizmoType.Selected )]
    private static void DrawGizmos( Transform aTarget, GizmoType aGizmoType )
    {
      return;
      if ( s_window == null )
        return;
      foreach ( var (bone, data) in s_window.data.BoneDatas ) {
        if ( !data.GenerateConstraint )
          continue;
        var pos = bone.position;
        var axis = data.GeneratedConstraintAxis switch
        {
          ConstraintAxis.Z => bone.forward,
          ConstraintAxis.X => bone.right,
          ConstraintAxis.Y => bone.up,
          _ => bone.forward
        };

        var preCol = Gizmos.color;
        float scale = 1.0f;
        if ( data.Selected ) {
          scale = 1.25f;
          Gizmos.color = Color.green;
        }
        if ( data.GeneratedConstraintType == ConstraintType.Hinge ) {
          var p1 = pos + axis/2 * scale;
          var p2 = pos - axis/2 * scale;
          Gizmos.DrawLine( p1, p2 );
        }
        else if ( data.GeneratedConstraintType == ConstraintType.Prismatic ) {
          var p1 = pos + axis/2 * scale;
          Gizmos.DrawLine( pos, p1 );
        }
        Gizmos.DrawSphere( pos, 0.1f * scale );
        Gizmos.color = preCol;
      }
    }
  }
}
