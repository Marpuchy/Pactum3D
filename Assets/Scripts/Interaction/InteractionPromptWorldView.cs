using TMPro;
using UnityEngine;

public sealed class InteractionPromptWorldView : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private TMP_Text label;

    private void Awake()
    {
        if (canvas == null) canvas = GetComponentInChildren<Canvas>(true);
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);

        Hide();
    }

    public void Show(string text)
    {
        if (label != null)
        {
            if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);
            label.text = text;
        }
       
        gameObject.SetActive(true);
      
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
