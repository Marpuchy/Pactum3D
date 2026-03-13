using UnityEngine;
using UnityEngine.SceneManagement;

public class ControladorEscenas : MonoBehaviour
{
    // Nombre de tu escena de HUD
    public string hudSceneName = "UI_HUD"; 

    void Start()
    {
        // Buscamos si la escena ya está en la lista de cargadas
        Scene scene = SceneManager.GetSceneByName(hudSceneName);

        // SIMPLIFICADO: Si la propiedad isLoaded es falsa, significa que
        // o bien no existe, o bien no está cargada. En ambos casos, intentamos cargarla.
        if (!scene.isLoaded)
        {
            SceneManager.LoadScene(hudSceneName, LoadSceneMode.Additive);
        }
    }
}