using UnityEngine;

public abstract class PactModifierAsset : ScriptableObject
{
    [Header("Description")]
    [TextArea]
    [SerializeField] private string description;

    public string Description
    {
        get
        {
            string explicitDescription = NormalizeDescription(description);
            if (explicitDescription.Length > 0)
                return explicitDescription;

            return NormalizeDescription(BuildAutoDescription());
        }
    }

    protected virtual string BuildAutoDescription()
    {
        return string.Empty;
    }

    private static string NormalizeDescription(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
