using UnityEngine;

public interface IItem
{
    string Name { get; }
    Sprite Icon { get; }
    string Description { get; }
    ItemRaritySO Rarity { get; }
    int SellValue { get; }
    AudioClip UseSound { get; }
    
    void Use();
}
