using UnityEngine;

public class Chest : MonoBehaviour
{
    private ItemDataSO item;

    public void Initialize(ItemDataSO itemData)
    {
        item = itemData;
    }

    public void Open()
    {
        if (item == null) return;

        // lógica de drop
    }
}