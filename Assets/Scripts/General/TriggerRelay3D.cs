using UnityEngine;

public interface ITriggerRelay3DReceiver
{
    void HandleTriggerEnter3D(Collider other);
    void HandleTriggerExit3D(Collider other);
}

public sealed class TriggerRelay3D : MonoBehaviour
{
    [SerializeField] private MonoBehaviour targetBehaviour;

    private ITriggerRelay3DReceiver receiver;

    public void Configure(Component target)
    {
        targetBehaviour = target as MonoBehaviour;
        receiver = target as ITriggerRelay3DReceiver;
    }

    private void Awake()
    {
        ResolveReceiver();
    }

    private void OnValidate()
    {
        ResolveReceiver();
    }

    private void OnTriggerEnter(Collider other)
    {
        ResolveReceiver();
        receiver?.HandleTriggerEnter3D(other);
    }

    private void OnTriggerExit(Collider other)
    {
        ResolveReceiver();
        receiver?.HandleTriggerExit3D(other);
    }

    private void ResolveReceiver()
    {
        receiver = targetBehaviour as ITriggerRelay3DReceiver;
        if (receiver != null)
            return;

        MonoBehaviour[] behaviours = GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is ITriggerRelay3DReceiver candidate)
            {
                targetBehaviour = behaviours[i];
                receiver = candidate;
                return;
            }
        }
    }
}
