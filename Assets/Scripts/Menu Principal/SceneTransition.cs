using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class SceneTransition : MonoBehaviour
{
    public static SceneTransition Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    [Header("Visual Configuration")]
    public CanvasGroup blackCurtain;
    public float fadeSpeed = 1.0f;

    [Header("Objects to Disable")]
    public GameObject fireParticles;

    public void FadeAndLoadScene(string sceneName)
    {
        if (fireParticles != null)
        {
            fireParticles.SetActive(false);
        }

        StartCoroutine(FadeAndLoadRoutine(sceneName));
    }

    IEnumerator FadeAndLoadRoutine(string targetScene)
    {
        if (blackCurtain != null)
        {
            blackCurtain.blocksRaycasts = true;
        }

        float time = 0;
        while (time < 1)
        {
            time += Time.deltaTime / fadeSpeed;
            if (blackCurtain != null) blackCurtain.alpha = time;
            yield return null;
        }

        if (blackCurtain != null) blackCurtain.alpha = 1f;

        yield return new WaitForSeconds(0.5f);

        SceneManager.LoadScene(targetScene);
    }
}