using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AGXUnity;

using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  public class SelectGameObjectDropdownMenuTool : Tool
  {
    public static GUIContent GetGUIContent( GameObject gameObject )
    {
      bool isNull       = gameObject == null;
      bool hasVisual    = !isNull && gameObject.GetComponent<MeshFilter>() != null;
      bool hasRigidBody = !isNull && gameObject.GetComponent<RigidBody>() != null;
      bool hasShape     = !isNull && gameObject.GetComponent<AGXUnity.Collide.Shape>() != null;
      bool hasWire      = !isNull && gameObject.GetComponent<Wire>() != null;
      bool hasCable     = !isNull && gameObject.GetComponent<Cable>() != null;
      bool hasTrack     = !isNull && gameObject.GetComponent<AGXUnity.Model.Track>() != null;
      bool hasTerrain   = !isNull && gameObject.GetComponent<AGXUnity.Model.DeformableTerrain>() != null;
      bool hasPager     = !isNull && gameObject.GetComponent<AGXUnity.Model.DeformableTerrainPager>() != null;

      string nullTag      = isNull       ? GUI.AddColorTag( "[null]", Color.red ) : "";
      string visualTag    = hasVisual    ? GUI.AddColorTag( "[Visual]", Color.yellow ) : "";
      string rigidBodyTag = hasRigidBody ? GUI.AddColorTag( "[RigidBody]", Color.Lerp( Color.blue, Color.white, 0.35f ) ) : "";
      string shapeTag     = hasShape     ? GUI.AddColorTag( "[" + gameObject.GetComponent<AGXUnity.Collide.Shape>().GetType().Name + "]", Color.Lerp( Color.green, Color.black, 0.4f ) ) : "";
      string wireTag      = hasWire      ? GUI.AddColorTag( "[Wire]", Color.Lerp( Color.cyan, Color.black, 0.35f ) ) : "";
      string cableTag     = hasCable     ? GUI.AddColorTag( "[Cable]", Color.Lerp( Color.yellow, Color.red, 0.65f ) ) : "";
      string trackTag     = hasTrack     ? GUI.AddColorTag( "[Track]", Color.Lerp( Color.yellow, Color.red, 0.45f ) ) : "";
      string terrainTag   = hasTerrain   ? GUI.AddColorTag( "[Terrain]", Color.Lerp( Color.green, Color.yellow, 0.25f ) ) : "";
      string pagingTag    = hasPager     ? GUI.AddColorTag( "[PaginTerrain]", Color.Lerp( Color.green, Color.yellow, 0.65f ) ) : "";

      string name = isNull ? "World" : gameObject.name;

      return GUI.MakeLabel( name + " " + nullTag + rigidBodyTag + shapeTag + visualTag + wireTag + cableTag + trackTag + terrainTag + pagingTag );
    }

    public class ObjectData
    {
      public GameObject GameObject = null;
      public bool MouseOver = false;
    }

    private List<ObjectData> m_gameObjectList = new List<ObjectData>();
    public ObjectData[] DropdownList { get { return m_gameObjectList.ToArray(); } }

    public string Title = "Select game object";

    private GameObject m_target = null;
    public GameObject Target
    {
      get { return m_target; }
      set { SetTarget( value ); }
    }

    public bool WindowIsActive { get { return Manager.SceneViewGUIWindowHandler.GetWindowData( OnWindowGUI ) != null; } }

    public Action<GameObject> OnSelect = delegate { };

    public bool RemoveOnKeyEscape     = true;
    public bool RemoveOnCameraControl = true;
    public bool RemoveOnClickMiss     = true;

    public SelectGameObjectDropdownMenuTool()
      : base( isSingleInstanceTool: true )
    {
    }

    public void SetTarget( GameObject target, Predicate<GameObject> pred = null )
    {
      m_target = target;
      BuildListGivenTarget( pred );
    }

    public void Show()
    {
      Show( Event.current.mousePosition );
    }

    public void Show( Vector2 position )
    {
      Manager.SceneViewGUIWindowHandler.Show( OnWindowGUI,
                                              new Vector2( m_windowWidth, 0 ),
                                              position + new Vector2( -0.5f * m_windowWidth, -10 ),
                                              WindowTitle.text );
    }

    public override void OnRemove()
    {
      m_selected = null;

      Manager.SceneViewGUIWindowHandler.Close( OnWindowGUI );
    }

    public override void OnSceneViewGUI( SceneView sceneView )
    {
      bool remove = (
                      ( RemoveOnKeyEscape && Manager.KeyEscapeDown ) ||
                      ( RemoveOnCameraControl && Manager.IsCameraControl ) ||
                      ( RemoveOnClickMiss &&
                        WindowIsActive &&
                        Manager.LeftMouseClick &&
                       !Manager.SceneViewGUIWindowHandler.GetWindowData( OnWindowGUI ).Contains( Event.current.mousePosition ) )
                    );

      if ( remove )
        PerformRemoveFromParent();
      else if ( m_selected != null ) {
        OnSelect( m_selected.Object );
        PerformRemoveFromParent();
      }
    }

    private GUIContent WindowTitle { get { return GUI.MakeLabel( Title ); } }

    private float m_windowWidth = 0f;

    private class SelectedObject { public GameObject Object = null; }
    private SelectedObject m_selected = null;

    private void BuildListGivenTarget( Predicate<GameObject> pred )
    {
      if ( pred == null )
        pred = go => { return true; };

      m_gameObjectList.Clear();

      m_windowWidth = Mathf.Max( 1.5f * GUI.Skin.label.CalcSize( WindowTitle ).x, GUI.Skin.button.CalcSize( GetGUIContent( Target ) ).x );

      if ( Target != null ) {
        if ( pred( Target ) )
          m_gameObjectList.Add( new ObjectData() { GameObject = Target, MouseOver = false } );

        Transform parent = Target.transform.parent;
        while ( parent != null ) {
          m_windowWidth = Mathf.Max( m_windowWidth, GUI.Skin.button.CalcSize( GetGUIContent( parent.gameObject ) ).x );

          if ( pred( parent.gameObject ) )
            m_gameObjectList.Add( new ObjectData() { GameObject = parent.gameObject, MouseOver = false } );
          parent = parent.parent;
        }
      }

      // Always adding world at end of list. If Target == null this will be the only entry.
      if ( pred( null ) )
        m_gameObjectList.Add( new ObjectData() { GameObject = null, MouseOver = false } );
    }

    private void OnWindowGUI( EventType eventType )
    {
      GameObject mouseOverObject = null;
      foreach ( var data in m_gameObjectList ) {
        if ( eventType == EventType.Repaint )
          data.MouseOver = false;

        if ( GUILayout.Button( GetGUIContent( data.GameObject ), GUI.Skin.button ) )
          m_selected = new SelectedObject() { Object = data.GameObject };

        if ( eventType == EventType.Repaint && GUILayoutUtility.GetLastRect().Contains( Event.current.mousePosition ) ) {
          mouseOverObject = data.GameObject;
          data.MouseOver = true;
        }
      }

      if ( mouseOverObject != HighlightObject && eventType == EventType.Repaint )
        HighlightObject = mouseOverObject;
    }
  }
}
