using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(SOStorageModeAttribute))]
public sealed class SOStorageModeDrawer : PropertyDrawer
{
    private static bool ReadSelection(SerializedProperty property, out SODataMode mode, out bool mixed)
    {
        mode = ((SODataController)property.serializedObject.targetObject).Mode;
        mixed = false;
        bool locked = false;
        foreach (Object selected in property.serializedObject.targetObjects)
        {
            SODataController domain = (SODataController)selected;
            mixed |= domain.Mode != mode;
            locked |= domain.RequiredMode.HasValue;
        }
        return locked;
    }

    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        bool locked = ReadSelection(property, out SODataMode mode, out bool mixed);
        EnumField field = new EnumField(property.displayName, mode) { showMixedValue = mixed, tooltip = property.tooltip };
        field.AddToClassList(BaseField<System.Enum>.alignedFieldUssClassName);
        if (locked) field.SetEnabled(false);
        else field.BindProperty(property);
        return field;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        bool locked = ReadSelection(property, out SODataMode mode, out bool mixed);
        EditorGUI.BeginProperty(position, label, property);
        bool previousMixed = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = mixed;
        using (new EditorGUI.DisabledScope(locked))
        {
            EditorGUI.BeginChangeCheck();
            SODataMode selected = (SODataMode)EditorGUI.EnumPopup(position, label, mode);
            if (EditorGUI.EndChangeCheck() && !locked) property.intValue = (int)selected;
        }
        EditorGUI.showMixedValue = previousMixed;
        EditorGUI.EndProperty();
    }
}
