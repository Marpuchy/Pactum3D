using UnityEngine;

public class DoorView : MonoBehaviour
{
    public DoorDirection direction;

    public void Init(DoorData data)
    {
        direction = data.Direction;
        DoorController controller = GetComponent<DoorController>();
        if (controller != null)
            controller.SyncFromView();
    }
}
