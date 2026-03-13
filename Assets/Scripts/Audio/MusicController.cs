using System;
using System.Collections;
using UnityEngine;

public sealed class MusicController : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private float fadeDuration = 1f;
    
    [Header("Events")]
    [SerializeField] private PauseEventSO onMusicPauseEvent;
    [SerializeField] private PauseEventSO onMusicResumeEvent;

    private Coroutine fadeRoutine;

    private void Awake()
    {
        audioSource.loop = true;
    }

    public void Play(MusicTrack track)
    {
        if (audioSource.clip == track.Clip)
            return;

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeIn(track));
    }

    public void Stop()
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeOut());
    }

    private IEnumerator FadeIn(MusicTrack track)
    {
        yield return FadeOutInternal();

        audioSource.clip = track.Clip;
        audioSource.volume = 0f;
        audioSource.Play();

        float time = 0f;
        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(0f, track.Volume, time / fadeDuration);
            yield return null;
        }

        audioSource.volume = track.Volume;
    }

    private IEnumerator FadeOut()
    {
        yield return FadeOutInternal();
        audioSource.Stop();
    }

    private IEnumerator FadeOutInternal()
    {
        float startVolume = audioSource.volume;
        float time = 0f;

        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, time / fadeDuration);
            yield return null;
        }
    }
    
    private IEnumerator FadeOutDuringPause()
    {
        float startVolume = audioSource.volume;
        float time = 0f;

        while (time < fadeDuration)
        {
            time += Time.unscaledDeltaTime; 
            audioSource.volume = Mathf.Lerp(startVolume, 0f, time / fadeDuration);
            yield return null;
        }

        audioSource.volume = 0f;
        audioSource.Pause(); 
    }

    private IEnumerator FadeInDuringResume()
    {
        audioSource.UnPause();
        float targetVolume = 1f; 
        float time = 0f;
        audioSource.volume = 0f;

        while (time < fadeDuration)
        {
            time += Time.unscaledDeltaTime;
            audioSource.volume = Mathf.Lerp(0f, targetVolume, time / fadeDuration);
            yield return null;
        }

        audioSource.volume = targetVolume;
    }

    
    private void OnPaused()
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        // Inicia fade out pero mantiene el clip en memoria
        fadeRoutine = StartCoroutine(FadeOutDuringPause());
    }

    private void OnResumed()
    {
        if (audioSource.clip == null) return;

        // Si ya hay un fade en proceso, cancelarlo
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        // Inicia fade in desde 0 hasta volumen original
        fadeRoutine = StartCoroutine(FadeInDuringResume());
    }
    
    //Events
    private void OnEnable()
    {
        onMusicPauseEvent?.RegisterListener(OnPaused);
        onMusicResumeEvent?.RegisterListener(OnResumed);
    }
    
    
    private void OnDisable()
    {
        onMusicPauseEvent?.UnregisterListener(OnPaused);
        onMusicResumeEvent?.UnregisterListener(OnResumed);
    }
}