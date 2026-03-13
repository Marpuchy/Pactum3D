using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Items/Item Data", fileName = "NewItem")]
public class ItemDataSO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string id;
    [SerializeField] private string displayName;

    [Header("Visual")]
    [SerializeField] private Sprite icon;
    
    [TextArea]
    [SerializeField] private string description;

    [Header("Type")]
    [SerializeField] private ItemType itemType;

    [Header("Stats")] 
    [SerializeField] private WeaponStatsSO weaponStats;
    [SerializeField] private ArmorStatsSO armorStats;
    [SerializeField] private ConsumableStatsSO consumableStats;
    
    [Header("Classification")]
    [SerializeField] private ItemRaritySO rarity;

    [Header("Stacking")]
    [SerializeField] private bool stackable;
    [SerializeField] private bool isCurrency;

    [Header("Economy")]
    [SerializeField] private int baseSellValue;

    [Header("Audio")]
    [SerializeField] private AudioClip useSound;
    [Range(0f, 1f)] [SerializeField] private float volumeUse = 1f;
    
    [SerializeField] private AudioClip pickSound;
    [Range(0f, 1f)] [SerializeField] private float volumePick = 1f;
    
    [Header("Modifiers")]
    [SerializeField] private List<ModifierDataSO> modifiers = new List<ModifierDataSO>();

    private void OnValidate()
    {
        switch (itemType)
        {
            case ItemType.Weapon:
                armorStats = null;
                consumableStats = null;
                break;
            case ItemType.Armor:
                weaponStats = null;
                consumableStats = null;
                break;
            case ItemType.Consumable:
                weaponStats = null;
                armorStats = null;
                break;
            default:
                weaponStats = null;
                armorStats = null;
                consumableStats = null;
                break;
        }
    }

    public string Id => id;
    public string SaveId => string.IsNullOrWhiteSpace(id) ? name : id;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public string Description => description;
    public ItemRaritySO Rarity => rarity;
    public bool Stackable => stackable;
    public bool IsCurrency => isCurrency;
    public int SellValue => rarity != null ? 
        Mathf.RoundToInt(baseSellValue * rarity.SellValueMultiplier) : baseSellValue;
    public AudioClip UseSound => useSound;
    public AudioClip PickSound => pickSound;
    public float VolumeUse => volumeUse;
    public float VolumePick => volumePick;
    
    public List<ModifierDataSO> Modifiers => modifiers;
    
    public ItemType ItemType => itemType;
    public WeaponStatsSO WeaponStats => weaponStats;
    public ArmorStatsSO ArmorStats => armorStats;
    public ConsumableStatsSO ConsumableStats => consumableStats;
}
