using UnityEngine;
using TMPro;

public class InventoryStatsUI : MonoBehaviour
{
    [Header("Labels")]
    [SerializeField] private TextMeshProUGUI healthLabel;
    [SerializeField] private TextMeshProUGUI damageLabel;
    [SerializeField] private TextMeshProUGUI armorLabel;
    [SerializeField] private TextMeshProUGUI attackSpeedLabel;
    [SerializeField] private TextMeshProUGUI speedLabel;

    [Header("Event")]
    [SerializeField] private InventoryOpenEvent inventoryOpenEvent;

    private void OnEnable()
    {
        if (inventoryOpenEvent != null)
            inventoryOpenEvent.OnRaise += UpdateLabels;
    }

    private void OnDisable()
    {
        if (inventoryOpenEvent != null)
            inventoryOpenEvent.OnRaise -= UpdateLabels;
    }

    /// <summary>
    /// Actualiza las labels de la UI con los valores de stats.
    /// </summary>
    private void UpdateLabels(float health, float damage, float armor, float attackSpeed, float speed)
    {
        if (healthLabel != null) healthLabel.text = $"Health: {health:0}";
        if (damageLabel != null) damageLabel.text = $"Damage: {damage:0}";
        if (armorLabel != null) armorLabel.text = $"Armor: {armor:0}";
        if (attackSpeedLabel != null) attackSpeedLabel.text = $"Attack Speed: {attackSpeed:0.00}";
        if (speedLabel != null) speedLabel.text = $"Speed: {speed:0.00}";
    }
}