using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ItemDataSO))]
public class ItemDataSOEditor : Editor
{
    private SerializedProperty idProp;
    private SerializedProperty displayNameProp;
    private SerializedProperty iconProp;
    private SerializedProperty descriptionProp;
    private SerializedProperty itemTypeProp;
    private SerializedProperty weaponStatsProp;
    private SerializedProperty armorStatsProp;
    private SerializedProperty consumableStatsProp;
    private SerializedProperty rarityProp;
    private SerializedProperty stackableProp;
    private SerializedProperty isCurrencyProp;
    private SerializedProperty baseSellValueProp;
    private SerializedProperty useSoundProp;
    private SerializedProperty volumeUseProp;
    private SerializedProperty pickSoundProp;
    private SerializedProperty volumePickProp;
    private SerializedProperty modifiersProp;

    private void OnEnable()
    {
        idProp = serializedObject.FindProperty("id");
        displayNameProp = serializedObject.FindProperty("displayName");
        iconProp = serializedObject.FindProperty("icon");
        descriptionProp = serializedObject.FindProperty("description");
        itemTypeProp = serializedObject.FindProperty("itemType");
        weaponStatsProp = serializedObject.FindProperty("weaponStats");
        armorStatsProp = serializedObject.FindProperty("armorStats");
        consumableStatsProp = serializedObject.FindProperty("consumableStats");
        rarityProp = serializedObject.FindProperty("rarity");
        stackableProp = serializedObject.FindProperty("stackable");
        isCurrencyProp = serializedObject.FindProperty("isCurrency");
        baseSellValueProp = serializedObject.FindProperty("baseSellValue");
        useSoundProp = serializedObject.FindProperty("useSound");
        volumeUseProp = serializedObject.FindProperty("volumeUse");
        pickSoundProp = serializedObject.FindProperty("pickSound");
        volumePickProp = serializedObject.FindProperty("volumePick");
        modifiersProp = serializedObject.FindProperty("modifiers");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(idProp);
        EditorGUILayout.PropertyField(displayNameProp);
        EditorGUILayout.PropertyField(iconProp);
        EditorGUILayout.PropertyField(descriptionProp);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(itemTypeProp);
        bool typeChanged = EditorGUI.EndChangeCheck();
        if (typeChanged && !itemTypeProp.hasMultipleDifferentValues)
        {
            ClearStatsForType((ItemType)itemTypeProp.enumValueIndex);
        }

        DrawStatsSection();

        EditorGUILayout.PropertyField(rarityProp);
        EditorGUILayout.PropertyField(stackableProp);
        EditorGUILayout.PropertyField(isCurrencyProp);
        EditorGUILayout.PropertyField(baseSellValueProp);
        EditorGUILayout.PropertyField(useSoundProp);
        EditorGUILayout.PropertyField(volumeUseProp);
        EditorGUILayout.PropertyField(pickSoundProp);
        EditorGUILayout.PropertyField(volumePickProp);
        EditorGUILayout.PropertyField(modifiersProp, true);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawStatsSection()
    {
        if (itemTypeProp.hasMultipleDifferentValues)
        {
            EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Select a single ItemDataSO to edit stats.", MessageType.Info);
            return;
        }

        ItemType itemType = (ItemType)itemTypeProp.enumValueIndex;
        switch (itemType)
        {
            case ItemType.Weapon:
                EditorGUILayout.PropertyField(weaponStatsProp);
                break;
            case ItemType.Armor:
                EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(armorStatsProp);
                break;
            case ItemType.Consumable:
                EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(consumableStatsProp);
                break;
            default:
                EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("This item type does not use a stats asset.", MessageType.Info);
                break;
        }
    }

    private void ClearStatsForType(ItemType itemType)
    {
        switch (itemType)
        {
            case ItemType.Weapon:
                armorStatsProp.objectReferenceValue = null;
                consumableStatsProp.objectReferenceValue = null;
                break;
            case ItemType.Armor:
                weaponStatsProp.objectReferenceValue = null;
                consumableStatsProp.objectReferenceValue = null;
                break;
            case ItemType.Consumable:
                weaponStatsProp.objectReferenceValue = null;
                armorStatsProp.objectReferenceValue = null;
                break;
            default:
                weaponStatsProp.objectReferenceValue = null;
                armorStatsProp.objectReferenceValue = null;
                consumableStatsProp.objectReferenceValue = null;
                break;
        }
    }
}
