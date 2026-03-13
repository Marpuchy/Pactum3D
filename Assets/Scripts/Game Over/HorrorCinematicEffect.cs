using UnityEngine;
using UnityEngine.UI;

public class HorrorCinematicEffect : MonoBehaviour
{
    [Header("Horror Settings")]
    public float zoomSpeed = 0.05f;      // How fast the image comes towards you (Slow is scary)
    public float shakeIntensity = 5.0f;  // How much the screen trembles (Nervous effect)
    
    private RectTransform rectTransform;
    private Vector2 originalPosition;
    private Vector3 initialScale;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    void OnEnable()
    {
        // 1. Save original position to shake around it
        originalPosition = rectTransform.anchoredPosition;

        // 2. Reset Scale slightly larger (1.1x) 
        initialScale = new Vector3(1.1f, 1.1f, 1f);
        rectTransform.localScale = initialScale;
    }

    void Update()
    {
        float zoomStep = zoomSpeed * Time.deltaTime;
        rectTransform.localScale += new Vector3(zoomStep, zoomStep, 0);
        Vector2 shakeOffset = Random.insideUnitCircle * shakeIntensity;
        rectTransform.anchoredPosition = originalPosition + shakeOffset;
    }
}