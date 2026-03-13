using UnityEngine;

[CreateAssetMenu(fileName = "PactLine", menuName = "Gameplay/Pacts/PactLine")]
public sealed class PactLineSO : ScriptableObject
{
    [SerializeField] private string lineId;
    [SerializeField] private string title;

    public string Id => string.IsNullOrWhiteSpace(lineId) ? name : lineId;
    public string Title => string.IsNullOrWhiteSpace(title) ? name : title;
}
