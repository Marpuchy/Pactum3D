using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class MenuButtonFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Configuración Visual")]
    public float zoomSize = 1.1f;      
    public float animationSpeed = 15f; 

    [Header("Colores")]
    public Color normalColor = Color.white; 
    public Color hoverColor = new Color32(255, 50, 50, 255); 

    private Vector3 originalScale;
    private Vector3 targetScale;
    private Image imagenBoton;
    private TextMeshProUGUI textoBoton;

    void Start()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;
        
        imagenBoton = GetComponent<Image>();
        textoBoton = GetComponentInChildren<TextMeshProUGUI>();

        // Forzamos el color normal al inicio
        ResetearColores();
    }

    void Update()
    {
        // 👇 AQUÍ ESTÁ EL TRUCO: "unscaledDeltaTime" funciona aunque el juego esté en PAUSA
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * animationSpeed);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        targetScale = originalScale * zoomSize; 
        if (imagenBoton != null) imagenBoton.color = hoverColor;
        if (textoBoton != null) textoBoton.color = hoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = originalScale; 
        ResetearColores();
    }

    void ResetearColores()
    {
        if (imagenBoton != null) imagenBoton.color = normalColor;
        if (textoBoton != null) textoBoton.color = normalColor;
    }
}