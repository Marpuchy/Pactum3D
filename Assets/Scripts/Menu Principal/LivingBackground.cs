using UnityEngine;

public class LivingBackgroundPro : MonoBehaviour
{
    [Header("Configuración Profesional")]
    [Tooltip("Escala inicial (ej: 1)")]
    public float escalaMinima = 1.0f;

    [Tooltip("Escala máxima (ej: 1.05 para sutil, 1.1 para notorio)")]
    public float escalaMaxima = 1.05f;

    [Tooltip("Duración de una respiración completa en segundos")]
    public float tiempoRespiracion = 6.0f; // 6 segundos es un ritmo relajado/tenso

    void Update()
    {
        // FÓRMULA PRO: Onda Senoidal (Sine Wave)
        // Esto crea una curva suave: empieza lento, acelera, y frena al final.
        // Mueve el valor entre 0 y 1 suavemente.
        float ciclo = (Mathf.Sin(Time.time * (2 * Mathf.PI / tiempoRespiracion)) + 1.0f) / 2.0f;

        // Aplicamos el tamaño usando esa curva suave
        transform.localScale = Vector3.Lerp(
            new Vector3(escalaMinima, escalaMinima, 1), 
            new Vector3(escalaMaxima, escalaMaxima, 1), 
            ciclo
        );
    }
}