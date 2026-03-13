using UnityEngine;

public class PlayerInventoryHolder : MonoBehaviour
{
    public static PlayerInventoryHolder Instance { get; private set; }
    
    [SerializeField] private int maxSlots = Inventory.DefaultMaxSlots;
    public Inventory Inventory { get; private set; }

    private void Awake()
    {
        Instance = this;
        Inventory = new Inventory(maxSlots);
    }
}
