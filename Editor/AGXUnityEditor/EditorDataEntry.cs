using System;
using UnityEngine;
using UnityEditor;
using AGXUnity.Utils;

namespace AGXUnityEditor
{
  [Serializable]
  public class EditorDataEntry
  {
    public static uint CalculateKey( UnityEngine.Object target, string identifier )
    {
      return ( ( target == null ? "0" : target.GetInstanceID().ToString() ) + "_" + identifier ).To32BitFnv1aHash();
    }

    [SerializeField]
    private uint m_key = uint.MaxValue;
    [SerializeField]
    private int m_instanceId = int.MaxValue;
    [SerializeField]
    private bool m_isStatic = false;

    [SerializeField]
    private bool m_bool = false;
    [SerializeField]
    private int m_int = 0;
    [SerializeField]
    private float m_float = 0f;
    [SerializeField]
    private string m_string = string.Empty;
    [SerializeField]
    private UnityEngine.Object m_asset = null;
    [SerializeField]
    private Vector2 m_vector2;
    [SerializeField]
    private Vector3 m_vector3;
    [SerializeField]
    private Vector4 m_vector4;
    [SerializeField]
    private Color m_color;

    public bool Bool
    {
      get { return m_bool; }
      set
      {
        if ( m_bool == value )
          return;

        m_bool = value;

        OnValueChanged();
      }
    }

    public int Int
    {
      get { return m_int; }
      set
      {
        if ( m_int == value )
          return;

        m_int = value;

        OnValueChanged();
      }
    }

    public float Float
    {
      get { return m_float; }
      set
      {
        if ( m_float == value )
          return;

        m_float = value;

        OnValueChanged();
      }
    }

    public string String
    {
      get { return m_string; }
      set
      {
        if ( m_string == value )
          return;

        m_string = value;

        OnValueChanged();
      }
    }

    public UnityEngine.Object Asset
    {
      get { return m_asset; }
      set
      {
        m_asset = value;

        OnValueChanged();
      }
    }

    public Vector2 Vector2
    {
      get { return m_vector2; }
      set
      {
        if ( m_vector2 == value )
          return;

        m_vector2 = value;

        OnValueChanged();
      }
    }

    public Vector3 Vector3
    {
      get { return m_vector3; }
      set
      {
        if ( m_vector3 == value )
          return;

        m_vector3 = value;

        OnValueChanged();
      }
    }

    public Vector4 Vector4
    {
      get { return m_vector4; }
      set
      {
        if ( m_vector4 == value )
          return;

        m_vector4 = value;

        OnValueChanged();
      }
    }

    public Color Color
    {
      get { return m_color; }
      set
      {
        if ( m_color == value )
          return;

        m_color = value;

        OnValueChanged();
      }
    }

    public uint Key { get { return m_key; } private set { m_key = value; } }

    public int InstanceId { get { return m_instanceId; } private set { m_instanceId = value; } }

    public bool IsStatic { get { return m_isStatic; } }

    public EditorDataEntry( UnityEngine.Object target, uint key )
    {
      Key = key;
      if ( target != null )
        InstanceId = target.GetInstanceID();
      else
        m_isStatic = true;
    }

    public void SetIsStatic( bool isStatic )
    {
      m_isStatic = isStatic;
    }

    private void OnValueChanged()
    {
      // Saves our data file.
      EditorUtility.SetDirty( EditorData.Instance );

      if ( IsStatic )
        return;

      // This is to trigger an update of the target GUI when the value has been changed.
      // E.g., clicking expand/collapse on a foldout we'd like the GUI to instantly respond.
      UnityEngine.Object obj = EditorUtility.InstanceIDToObject( InstanceId );
      if ( obj != null )
        SceneView.RepaintAll();
    }
  }
}
