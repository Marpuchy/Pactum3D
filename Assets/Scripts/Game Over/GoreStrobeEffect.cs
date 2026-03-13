using UnityEngine;
using UnityEngine.UI;

public class GoreStrobeEffect : MonoBehaviour
{
    [Header("Strobe Settings")]
    [Tooltip("How transparent it gets during flashes (0 = Clear view)")]
    public float minAlpha = 0.1f;
    
    [Tooltip("How dark it gets (1 = Total Darkness)")]
    public float maxAlpha = 0.9f;

    [Tooltip("How violently it flashes (Higher = More seizure-inducing)")]
    public float strobeSpeed = 20f; 

    private CanvasGroup canvasGroup;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    void Update()
    {
    
        float noise = Mathf.PerlinNoise(Time.time * strobeSpeed, 0f);


        canvasGroup.alpha = Mathf.Lerp(minAlpha, maxAlpha, noise);
    }
}