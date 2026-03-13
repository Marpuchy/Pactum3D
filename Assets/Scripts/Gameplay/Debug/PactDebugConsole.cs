using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class PactDebugConsole : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] private CharacterStatResolver statResolver;
    [SerializeField] private CharacterStats baseStats;

    [Header("Logging")]
    [SerializeField] private List<StatType> statsToLog = new List<StatType>();
    [SerializeField] private bool logOnStart = true;
    [SerializeField] private Key logKey = Key.O;

    [Header("Pact Apply")]
    [SerializeField] private List<PactDefinition> pactsToApply = new List<PactDefinition>();
    [SerializeField] private bool applyFirstPactOnStart = false;
    [SerializeField] private Key applyNextPactKey = Key.P;

    private int nextPactIndex;

    private void Awake()
    {
        if (statResolver == null)
            statResolver = GetComponent<CharacterStatResolver>();
    }

    private void Start()
    {
        if (logOnStart)
            LogStats("Start");

        if (applyFirstPactOnStart)
            ApplyNextPactAndLog();
    }

    private void Update()
    {
        if (WasKeyPressedThisFrame(logKey))
            LogStats("Manual");

        if (WasKeyPressedThisFrame(applyNextPactKey))
            ApplyNextPactAndLog();
    }

    private void ApplyNextPactAndLog()
    {
        if (pactsToApply == null || pactsToApply.Count == 0)
        {
            Debug.LogWarning("PactDebugConsole: No pacts configured to apply.", this);
            return;
        }

        PactManager manager = PactManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("PactDebugConsole: PactManager.Instance not found.", this);
            return;
        }

        PactDefinition pact = pactsToApply[nextPactIndex];
        nextPactIndex = (nextPactIndex + 1) % pactsToApply.Count;

        manager.ApplyPact(pact);
        Debug.Log($"PactDebugConsole: Applied pact '{pact.name}'.", this);
        LogStats($"After {pact.name}");
    }

    private void LogStats(string context)
    {
        StatType[] statTypes = GetStatTypes();
        StringBuilder builder = new StringBuilder();
        builder.Append("PactDebugConsole: ");
        builder.Append(context);
        builder.Append(" stats");

        for (int i = 0; i < statTypes.Length; i++)
        {
            StatType type = statTypes[i];
            float baseValue = baseStats != null ? baseStats.GetBaseValue(type) : 0f;
            float resolvedValue = statResolver != null ? statResolver.Get(type, baseValue) : baseValue;

            builder.Append(" | ");
            builder.Append(type);
            builder.Append(": base=");
            builder.Append(baseValue.ToString("0.###"));
            builder.Append(", final=");
            builder.Append(resolvedValue.ToString("0.###"));
        }

        Debug.Log(builder.ToString(), this);
    }

    private StatType[] GetStatTypes()
    {
        if (statsToLog != null && statsToLog.Count > 0)
            return statsToLog.ToArray();

        return (StatType[])Enum.GetValues(typeof(StatType));
    }

    private static bool WasKeyPressedThisFrame(Key key)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        KeyControl keyControl = keyboard[key];
        if (keyControl == null)
            return false;

        return keyControl.wasPressedThisFrame;
    }
}
