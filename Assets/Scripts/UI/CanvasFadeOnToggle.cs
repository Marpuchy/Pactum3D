using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class CanvasFadeOnToggle : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private bool fadeInOnEnable = true;
    [SerializeField] private bool fadeOutOnDisable = true;
    [SerializeField] private float fadeInDuration = 0.2f;
    [SerializeField] private float fadeOutDuration = 0.2f;
    [SerializeField] private float visibleAlpha = 1f;
    [SerializeField] private float hiddenAlpha = 0f;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool disableRaycastsDuringFade = true;

    private Coroutine fadeRoutine;
    private bool suppressEnable;
    private bool suppressDisable;
    private bool isFadingOut;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        if (suppressEnable)
        {
            suppressEnable = false;
            return;
        }

        if (!fadeInOnEnable)
            return;

        BeginFade(visibleAlpha, fadeInDuration, finalInteractable: true, resetToHidden: true);
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        if (suppressDisable)
        {
            suppressDisable = false;
            return;
        }

        if (!fadeOutOnDisable || isFadingOut)
            return;

        if (gameObject.activeSelf)
            return;

        if (fadeOutDuration <= 0f || canvasGroup == null)
            return;

        isFadingOut = true;
        suppressEnable = true;
        gameObject.SetActive(true);

        BeginFade(hiddenAlpha, fadeOutDuration, finalInteractable: false, resetToHidden: false, onComplete: () =>
        {
            isFadingOut = false;
            suppressDisable = true;
            gameObject.SetActive(false);
        });
    }

    private void BeginFade(float targetAlpha, float duration, bool finalInteractable, bool resetToHidden, Action onComplete = null)
    {
        if (canvasGroup == null)
            return;

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        if (resetToHidden)
            canvasGroup.alpha = hiddenAlpha;

        SetInteractable(false);

        if (duration <= 0f)
        {
            canvasGroup.alpha = targetAlpha;
            SetInteractable(finalInteractable);
            onComplete?.Invoke();
            return;
        }

        fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, duration, finalInteractable, onComplete));
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration, bool finalInteractable, Action onComplete)
    {
        float startAlpha = canvasGroup.alpha;
        float time = 0f;

        while (time < duration)
        {
            time += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = duration > 0f ? Mathf.Clamp01(time / duration) : 1f;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        SetInteractable(finalInteractable);
        onComplete?.Invoke();
        fadeRoutine = null;
    }

    private void SetInteractable(bool value)
    {
        if (canvasGroup == null)
            return;

        if (disableRaycastsDuringFade)
        {
            canvasGroup.interactable = value;
            canvasGroup.blocksRaycasts = value;
        }
    }
}
