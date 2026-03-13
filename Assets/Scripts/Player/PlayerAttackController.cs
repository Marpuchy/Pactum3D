using Injections;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

public sealed class PlayerAttackController : MonoBehaviour
{
    [Header("Inputs")]
    [SerializeField] private InputActionReference attackAction; // Melee
    [SerializeField] private InputActionReference fireAction;   // Ranged

    [Header("Definitions")]
    [SerializeField] private PlayerDefinitionSO meleeDefinition;
    [SerializeField] private PlayerDefinitionSO rangedDefinition;
    [SerializeField] private bool applyDefinitionOnAwake = true;

    [Header("Runtime Refs")]
    [SerializeField] private CharacterStatResolver statResolver;
    [SerializeField] private HealthComponent healthComponent;
    [SerializeField] private AttackComponent attackComponent;

    // Legacy serialized field kept to avoid prefab data loss.
    [SerializeField] private PlayerAttackSelection attackSelection;
    
    private IPlayerDataService playerDataService;
    private bool statsSubscribed;

    [Inject]
    private void Construct([InjectOptional] IPlayerDataService injectedPlayerDataService)
    {
        playerDataService = injectedPlayerDataService;
    }

    private void Awake()
    {
        if (playerDataService == null)
            playerDataService = RuntimeServiceFallback.PlayerDataService;

        CacheComponents();

        if (applyDefinitionOnAwake)
            ApplySelectedDefinition();

        ConfigureInputs();
        RefreshRuntimeStateFromTags();
    }

    private void OnEnable()
    {
        TrySubscribeToStats();
        RefreshRuntimeStateFromTags();
        ApplySelectedAttack();
    }

    private void OnDisable()
    {
        UnsubscribeFromStats();
        UnsubscribeInputHandlers();
        DisableAllInputs();
    }

    private void ConfigureInputs()
    {
        ApplySelectedAttack();
    }

    private void OnMeleeAttack(InputAction.CallbackContext context)
    {
        if (GameplayUIState.IsGameplayInputBlocked)
            return;

        ExecuteMelee();
    }

    private void OnRangedAttack(InputAction.CallbackContext context)
    {
        if (GameplayUIState.IsGameplayInputBlocked)
            return;

        ExecuteRanged();
    }

    private void ExecuteMelee()
    {
        //Debug.Log("Melee attack executed");
    }

    private void ExecuteRanged()
    {
        //Debug.Log("Ranged attack executed");
    }
    
    public void ApplySelectedAttack()
    {
        AttackType selectedAttack = ResolveSelectedAttack();

        UnsubscribeInputHandlers();
        DisableAllInputs();

        if (selectedAttack == AttackType.Melee)
        {
            if (attackAction != null && attackAction.action != null)
            {
                attackAction.action.performed += OnMeleeAttack;
                attackAction.action.Enable();
            }
        }
        else
        {
            if (fireAction != null && fireAction.action != null)
            {
                fireAction.action.performed += OnRangedAttack;
                fireAction.action.Enable();
            }
        }
    }

    public void ApplySelectedDefinition()
    {
        AttackType selectedAttack = ResolveSelectedAttack();
        PlayerDefinitionSO definition = ResolveDefinitionForAttack(selectedAttack);
        if (definition == null)
        {
            Debug.LogWarning(
                $"PlayerAttackController: no PlayerDefinitionSO found for attack '{selectedAttack}'.",
                this);
            return;
        }

        ApplyDefinition(definition);
    }

    private AttackType ResolveSelectedAttack()
    {
        return playerDataService != null
            ? playerDataService.SelectedAttack
            : AttackType.Melee;
    }

    private void UnsubscribeInputHandlers()
    {
        if (attackAction != null && attackAction.action != null)
            attackAction.action.performed -= OnMeleeAttack;

        if (fireAction != null && fireAction.action != null)
            fireAction.action.performed -= OnRangedAttack;
    }

    private void DisableAllInputs()
    {
        if (attackAction != null && attackAction.action != null)
            attackAction.action.Disable();

        if (fireAction != null && fireAction.action != null)
            fireAction.action.Disable();
    }

    private void CacheComponents()
    {
        if (statResolver == null)
            statResolver = GetComponent<CharacterStatResolver>();

        if (healthComponent == null)
            healthComponent = GetComponent<HealthComponent>();

        if (attackComponent == null)
            attackComponent = GetComponent<AttackComponent>();
    }

    private void TrySubscribeToStats()
    {
        if (statResolver == null || statsSubscribed)
            return;

        statResolver.StatsChanged += HandleStatsChanged;
        statsSubscribed = true;
    }

    private void UnsubscribeFromStats()
    {
        if (!statsSubscribed)
            return;

        if (statResolver != null)
            statResolver.StatsChanged -= HandleStatsChanged;

        statsSubscribed = false;
    }

    private void HandleStatsChanged()
    {
        RefreshRuntimeStateFromTags();
    }

    private void RefreshRuntimeStateFromTags()
    {
        IReadOnlyList<GameplayTag> effectiveTags = ResolveEffectiveTags();

        AttackType resolvedAttack = ResolveAttackTypeFromTags(effectiveTags, ResolveSelectedAttack());
        if (playerDataService != null && playerDataService.SelectedAttack != resolvedAttack)
            playerDataService.SetSelectedAttack(resolvedAttack);

        ApplySelectedAttack();
    }

    private IReadOnlyList<GameplayTag> ResolveEffectiveTags()
    {
        IReadOnlyList<GameplayTag> baseTags = statResolver != null ? statResolver.Tags : null;

        PactManager manager = PactManager.Instance;
        if (manager == null || manager.Stats == null)
            return baseTags;

        return manager.Stats.GetEffectiveTags(baseTags);
    }

    private static AttackType ResolveAttackTypeFromTags(IReadOnlyList<GameplayTag> tags, AttackType fallback)
    {
        if (HasTag(tags, "Ranged"))
            return AttackType.Ranged;

        if (HasTag(tags, "Melee"))
            return AttackType.Melee;

        return fallback;
    }

    private static bool HasTag(IReadOnlyList<GameplayTag> tags, string expectedTag)
    {
        if (tags == null || tags.Count == 0 || string.IsNullOrWhiteSpace(expectedTag))
            return false;

        string expectedWithSuffix = $"{expectedTag}Tag";

        for (int i = 0; i < tags.Count; i++)
        {
            GameplayTag tag = tags[i];
            if (tag == null)
                continue;

            if (string.Equals(tag.TagName, expectedTag, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag.TagName, expectedWithSuffix, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag.name, expectedTag, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag.name, expectedWithSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private PlayerDefinitionSO ResolveDefinitionForAttack(AttackType attackType)
    {
        if (attackType == AttackType.Ranged)
        {
            if (rangedDefinition != null)
                return rangedDefinition;

            PlayerDefinitionSO rangedByTag = ResolveDefinitionByTypeTag("Ranged");
            if (rangedByTag != null)
                return rangedByTag;

            return meleeDefinition != null ? meleeDefinition : ResolveDefinitionByTypeTag("Melee");
        }

        if (meleeDefinition != null)
            return meleeDefinition;

        PlayerDefinitionSO meleeByTag = ResolveDefinitionByTypeTag("Melee");
        if (meleeByTag != null)
            return meleeByTag;

        return rangedDefinition != null ? rangedDefinition : ResolveDefinitionByTypeTag("Ranged");
    }

    private static PlayerDefinitionSO ResolveDefinitionByTypeTag(string typeTag)
    {
        if (string.IsNullOrWhiteSpace(typeTag))
            return null;

        PlayerDefinitionSO[] definitions = Resources.FindObjectsOfTypeAll<PlayerDefinitionSO>();
        for (int i = 0; i < definitions.Length; i++)
        {
            PlayerDefinitionSO definition = definitions[i];
            if (definition == null)
                continue;

            IReadOnlyList<GameplayTag> tags = definition.Tags;
            if (!HasTag(tags, typeTag))
                continue;

            return definition;
        }

        return null;
    }

    private void ApplyDefinition(PlayerDefinitionSO definition)
    {
        if (definition == null)
            return;

        CharacterStats definitionStats = definition.Stats;
        if (definitionStats != null)
        {
            if (statResolver != null)
                statResolver.SetBaseStats(definitionStats);

            if (healthComponent != null)
                healthComponent.SetBaseStats(definitionStats, resetCurrentHealth: false);

            if (attackComponent != null)
                attackComponent.SetBaseStats(definitionStats);
        }

        if (statResolver != null)
            statResolver.ReplaceTags(definition.Tags);
    }
}
