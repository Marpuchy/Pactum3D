using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/Dialogue", fileName = "Dialogue")]
public sealed class DialogueDefinition : ScriptableObject
{
    [SerializeField] private string title;
    [SerializeField] private Sprite portrait;
    [SerializeField] private List<string> lines = new();

    public string Title => string.IsNullOrWhiteSpace(title) ? name : title;
    public Sprite Portrait => portrait;
    public IReadOnlyList<string> Lines => lines;
}
