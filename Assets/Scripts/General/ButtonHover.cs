using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private PlaySoundButton playSoundButton;
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        playSoundButton.PlayHoverSound();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        //playSoundButton.PlayHoverSound();
    }
}
