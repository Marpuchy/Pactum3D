using UnityEngine;

[CreateAssetMenu(fileName = "NewItemRarity", menuName = "Items/ItemRarity")]
public class ItemRaritySO : ScriptableObject
{
    [Header("Identiry")] 
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    
    [Header("Visual")]
    [SerializeField] private Color uiColor = Color.white;

    [Header("Economy")] 
    [SerializeField] private float sellValueMultiplier = 1f;
    
    public string Id => id;
    public string DisplayName => displayName;
    public Color UIColor => uiColor;
    public float SellValueMultiplier => sellValueMultiplier;

}
