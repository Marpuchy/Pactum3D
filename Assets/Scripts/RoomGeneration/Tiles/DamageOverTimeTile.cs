using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public abstract class DamageOverTimeTile : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] protected int damagePerTick = 1;
    [SerializeField] protected float tickInterval = 1f;
    [SerializeField] protected DamageEventSO damageEvent;
    
    [Header("Audio")]
    [SerializeField] private AudioClip damageLoopClip;

    private Coroutine damageRoutine;
    private bool isInside;
    private AudioSource audioSource;
    
    protected virtual void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.clip = damageLoopClip;
    }

    protected virtual bool IsValidTarget(Collider2D col)
    {
        return col.CompareTag("Player");
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (!IsValidTarget(col))
            return;

        StopDamage(); 

        isInside = true;
        PlayAudio();
        damageRoutine = StartCoroutine(DamageLoop(col.gameObject));
    }


    private void OnTriggerExit2D(Collider2D col)
    {
        if (!IsValidTarget(col))
            return;

        StopDamage();
    }

    private IEnumerator DamageLoop(GameObject target)
    {
        var wait = new WaitForSeconds(tickInterval);

        while (isInside)
        {
            if (!ShouldApplyDamage(target))
            {
                StopDamage(); 
                yield break;
            }

            damageEvent.Raise(target, damagePerTick);
            yield return wait;
        }
    }





    
    protected virtual bool CanDamage(GameObject target)
    {
        return true;
    }
    
    protected virtual bool ShouldApplyDamage(GameObject target)
    {
        return true;
    }


    private void StopDamage()
    {
        isInside = false;
        
        StopAudio();

        if (damageRoutine != null)
        {
            StopCoroutine(damageRoutine);
            damageRoutine = null;
        }
    }
    
    
    protected virtual void PlayAudio()
    {
        if (audioSource != null && damageLoopClip != null && !audioSource.isPlaying)
            audioSource.Play();
    }

    protected virtual void StopAudio()
    {
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }

    private void OnDisable()
    {
        StopDamage();
    }
}