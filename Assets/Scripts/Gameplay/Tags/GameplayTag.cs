using UnityEngine;

[CreateAssetMenu(fileName = "GameplayTag", menuName = "Gameplay/Tags/GameplayTag")]
public class GameplayTag : ScriptableObject
{
    [SerializeField] private string tagName;

    public string TagName => string.IsNullOrWhiteSpace(tagName) ? name : tagName;
}
