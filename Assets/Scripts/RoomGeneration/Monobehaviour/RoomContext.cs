using UnityEngine;

public class RoomContext : MonoBehaviour
{
    public static RoomContext Current { get; private set; }

    public Transform ItemsRoot { get; private set; }

    public void Initialize(Transform itemsRoot)
    {
        ItemsRoot = itemsRoot;
        Current = this;
    }

    private void OnDestroy()
    {
        if (Current == this)
            Current = null;
    }
}
