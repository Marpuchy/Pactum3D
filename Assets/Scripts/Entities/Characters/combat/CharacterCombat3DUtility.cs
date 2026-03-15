using UnityEngine;

public static class CharacterCombat3DUtility
{
    private const string Runtime3DHurtboxName = "Runtime3DHurtbox";
    private const float DefaultHurtboxDepth = 0.45f;

    public static void EnsureHurtbox(GameObject character, float localVerticalOffset = 0f)
    {
        RoomWorldSpaceSettings worldSpace = RoomWorldSpaceSettings.Current;
        if (character == null || worldSpace == null || !worldSpace.UsesXZPlane)
            return;

        Collider2D sourceCollider = character.GetComponent<Collider2D>();
        if (sourceCollider == null)
            return;

        Transform existingHost = character.transform.Find(Runtime3DHurtboxName);
        GameObject host = existingHost != null
            ? existingHost.gameObject
            : new GameObject(Runtime3DHurtboxName);

        if (existingHost == null)
        {
            host.transform.SetParent(character.transform, false);
        }

        host.layer = character.layer;
        host.transform.localPosition = new Vector3(0f, localVerticalOffset, 0f);
        host.transform.localRotation = Quaternion.identity;
        host.transform.localScale = Vector3.one;

        BoxCollider hurtbox = host.GetComponent<BoxCollider>();
        if (hurtbox == null)
            hurtbox = host.AddComponent<BoxCollider>();

        hurtbox.isTrigger = false;
        hurtbox.enabled = sourceCollider.enabled;
        Copy2DShapeTo3D(sourceCollider, hurtbox);
    }

    private static void Copy2DShapeTo3D(Collider2D source, BoxCollider destination)
    {
        if (source == null || destination == null)
            return;

        if (source is BoxCollider2D box2D)
        {
            destination.center = new Vector3(box2D.offset.x, box2D.offset.y, 0f);
            destination.size = new Vector3(
                Mathf.Max(0.05f, box2D.size.x),
                Mathf.Max(0.05f, box2D.size.y),
                DefaultHurtboxDepth);
            return;
        }

        if (source is CapsuleCollider2D capsule2D)
        {
            destination.center = new Vector3(capsule2D.offset.x, capsule2D.offset.y, 0f);
            destination.size = new Vector3(
                Mathf.Max(0.05f, capsule2D.size.x),
                Mathf.Max(0.05f, capsule2D.size.y),
                DefaultHurtboxDepth);
            return;
        }

        Bounds bounds = source.bounds;
        destination.center = Vector3.zero;
        destination.size = new Vector3(
            Mathf.Max(0.05f, bounds.size.x),
            Mathf.Max(0.05f, bounds.size.y),
            DefaultHurtboxDepth);
    }
}
