using System;
using UnityEngine;

[Serializable]
public readonly struct OpenDialogueRequest
{
    public Interactor Interactor { get; }
    public DialogueDefinition Dialogue { get; }

    public OpenDialogueRequest(Interactor interactor, DialogueDefinition dialogue)
    {
        Interactor = interactor;
        Dialogue = dialogue;
    }
}

[CreateAssetMenu(menuName = "Events/OpenDialogue", fileName = "OpenDialogueEventChannel")]
public sealed class OpenDialogueEventChannelSO : ScriptableObject
{
    public event Action<OpenDialogueRequest> OnRaised;

    public void Raise(OpenDialogueRequest request)
    {
        OnRaised?.Invoke(request);
    }
}
