using UnityEngine;
using UnityEngine.UI;

public class PantallaParpadeante : MonoBehaviour
{
    private Image imagenNegra;

    [Header("Configuración de Miedo")]
    [Tooltip("Transparencia normal (0 = invisible, 1 = negro total)")]
    public float oscuridadBase = 0.0f; 
    
    [Tooltip("Hasta cuánto sube la oscuridad en un parpadeo")]
    public float oscuridadMaxima = 0.8f; 

    [Header("Ritmo del Caos")]
    [Tooltip("Cuanto más alto, más rápido tiembla la luz")]
    public float velocidadTemblores = 10.0f;

    [Tooltip("Probabilidad de que pegue un 'apagón' fuerte (0 a 100)")]
    public float probabilidadApagon = 5.0f; // 5% de probabilidad en cada frame

    private float semillaRandom;

    void Start()
    {
        imagenNegra = GetComponent<Image>();
        semillaRandom = Random.Range(0f, 100f);
        
        // Aseguramos que sea negra
        if (imagenNegra != null)
        {
            imagenNegra.color = new Color(0, 0, 0, 0); 
        }
    }

    void Update()
    {
        if (imagenNegra == null) return;

        // 1. Temblores suaves constantes (Luz inestable)
        // Usamos PerlinNoise para que no sea robótico
        float ruido = Mathf.PerlinNoise(semillaRandom, Time.time * velocidadTemblores);
        float alphaActual = Mathf.Lerp(oscuridadBase, oscuridadBase + 0.1f, ruido);

        // 2. ¿Toca susto? (Apagón repentino)
        // Tiramos un dado. Si sale el número, la pantalla se oscurece de golpe.
        if (Random.Range(0f, 100f) < probabilidadApagon)
        {
            alphaActual = Random.Range(oscuridadMaxima / 2, oscuridadMaxima);
        }

        // 3. Aplicar el color final
        imagenNegra.color = new Color(0, 0, 0, alphaActual);
    }
}