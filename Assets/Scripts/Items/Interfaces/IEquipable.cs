using UnityEngine;

public interface IEquipable : IItem
{
    void Equip();
    void Unequip();
}
