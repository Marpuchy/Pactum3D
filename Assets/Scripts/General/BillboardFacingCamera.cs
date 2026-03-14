using UnityEngine;

[DisallowMultipleComponent]
public sealed class BillboardFacingCamera : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool yawOnly = true;
    [SerializeField] private bool invertForward = true;
    [SerializeField] private bool updateEveryFrame = true;

    private void OnEnable()
    {
        Align();
    }

    private void LateUpdate()
    {
        if (updateEveryFrame)
            Align();
    }

    public void Align()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null)
            return;

        Vector3 direction = targetCamera.transform.position - transform.position;
        if (yawOnly)
            direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        Vector3 forward = invertForward ? -direction.normalized : direction.normalized;
        transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }
}
