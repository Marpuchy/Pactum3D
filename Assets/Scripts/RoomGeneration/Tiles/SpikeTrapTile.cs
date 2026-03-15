using System.Collections;
using UnityEngine;

public sealed class SpikeTrapTile : MonoBehaviour, ITriggerRelay3DReceiver
{
    private enum AnimationMode
    {
        Trigger,
        Bool
    }

    [Header("Damage")]
    [SerializeField] private int damagePerTick = 1;
    [SerializeField] private float tickInterval = 1f;
    [SerializeField] private DamageEventSO damageEvent;

    [Header("Cycle")]
    [SerializeField] private float openDuration = 1f;
    [SerializeField] private float closedDuration = 1f;
    [SerializeField] private bool startOpen;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private AnimationMode animationMode = AnimationMode.Trigger;
    [SerializeField] private string openParameter = "Open";
    [SerializeField] private string closeParameter = "Close";
    [SerializeField] private string openBoolParameter = "IsOpen";

    private Coroutine cycleRoutine;
    private Coroutine damageRoutine;
    private bool isOpen;
    private bool isInside;
    private GameObject currentTarget;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        StartCycle();
    }

    private void OnDisable()
    {
        StopCycle();
        StopDamageRoutine();
        isInside = false;
        currentTarget = null;
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (!IsValidTarget(col))
            return;

        isInside = true;
        currentTarget = col.gameObject;

        if (isOpen)
            StartDamageRoutine();
    }

    private void OnTriggerExit2D(Collider2D col)
    {
        if (!IsValidTarget(col))
            return;

        isInside = false;
        currentTarget = null;
        StopDamageRoutine();
    }

    private void OnTriggerEnter(Collider col)
    {
        HandleTriggerEnter3D(col);
    }

    public void HandleTriggerEnter3D(Collider col)
    {
        if (!IsValidTarget(col))
            return;

        isInside = true;
        currentTarget = col.gameObject;

        if (isOpen)
            StartDamageRoutine();
    }

    private void OnTriggerExit(Collider col)
    {
        HandleTriggerExit3D(col);
    }

    public void HandleTriggerExit3D(Collider col)
    {
        if (!IsValidTarget(col))
            return;

        isInside = false;
        currentTarget = null;
        StopDamageRoutine();
    }

    private void StartCycle()
    {
        if (cycleRoutine != null)
            StopCoroutine(cycleRoutine);

        cycleRoutine = StartCoroutine(CycleLoop());
    }

    private void StopCycle()
    {
        if (cycleRoutine == null)
            return;

        StopCoroutine(cycleRoutine);
        cycleRoutine = null;
    }

    private IEnumerator CycleLoop()
    {
        SetOpenState(startOpen);

        while (true)
        {
            float waitTime = isOpen ? openDuration : closedDuration;
            if (waitTime > 0f)
                yield return new WaitForSeconds(waitTime);
            else
                yield return null;

            SetOpenState(!isOpen);
        }
    }

    private void SetOpenState(bool open)
    {
        isOpen = open;
        UpdateAnimation(open);

        if (open)
        {
            if (isInside)
                StartDamageRoutine();
        }
        else
        {
            StopDamageRoutine();
        }
    }

    private void StartDamageRoutine()
    {
        if (damageRoutine != null)
            return;

        damageRoutine = StartCoroutine(DamageLoop());
    }

    private void StopDamageRoutine()
    {
        if (damageRoutine == null)
            return;

        StopCoroutine(damageRoutine);
        damageRoutine = null;
    }

    private IEnumerator DamageLoop()
    {
        var wait = new WaitForSeconds(tickInterval);

        while (isOpen && isInside)
        {
            ApplyDamage();
            yield return wait;
        }

        damageRoutine = null;
    }

    private void ApplyDamage()
    {
        if (damageEvent == null || currentTarget == null)
            return;

        damageEvent.Raise(currentTarget, damagePerTick);
    }

    private bool IsValidTarget(Collider2D col)
    {
        return col.CompareTag("Player");
    }

    private bool IsValidTarget(Collider col)
    {
        return col.CompareTag("Player");
    }

    private void UpdateAnimation(bool open)
    {
        if (animator == null)
            return;

        switch (animationMode)
        {
            case AnimationMode.Trigger:
                if (open)
                {
                    if (!string.IsNullOrEmpty(closeParameter))
                        animator.ResetTrigger(closeParameter);
                    if (!string.IsNullOrEmpty(openParameter))
                        animator.SetTrigger(openParameter);
                }
                else
                {
                    if (!string.IsNullOrEmpty(openParameter))
                        animator.ResetTrigger(openParameter);
                    if (!string.IsNullOrEmpty(closeParameter))
                        animator.SetTrigger(closeParameter);
                }
                break;

            case AnimationMode.Bool:
                if (!string.IsNullOrEmpty(openBoolParameter))
                    animator.SetBool(openBoolParameter, open);
                break;
        }
    }
}
