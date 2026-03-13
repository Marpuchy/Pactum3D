using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public static class EnemyMovementAgentTypeMapper
{
    private const string LandingTagName = "Landing";
    private const string FlyingTagName = "Flying";
    private const string FloatingTagName = "Floating";
    private const string HumanoidAgentTypeName = "Humanoid";
    private const string FlyingAgentTypeName = "Flying";
    private const float NavMeshRebindMaxDistance = 128f;

    private static readonly HashSet<string> MissingAgentTypeWarnings =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public static void Apply(GameObject enemyInstance, IReadOnlyList<GameplayTag> tags)
    {
        if (enemyInstance == null)
            return;

        if (!enemyInstance.TryGetComponent(out NavMeshAgent agent))
            return;

        string targetAgentType = ResolveTargetAgentTypeName(tags);
        if (!TryGetAgentTypeId(targetAgentType, out int targetAgentTypeId))
        {
            if (MissingAgentTypeWarnings.Add(targetAgentType))
            {
                Debug.LogWarning(
                    $"EnemyMovementAgentTypeMapper: NavMesh Agent Type '{targetAgentType}' no existe en Navigation settings.",
                    enemyInstance);
            }

            return;
        }

        if (agent.agentTypeID == targetAgentTypeId)
        {
            EnsureBoundToAgentNavMesh(agent, enemyInstance.transform.position, targetAgentTypeId);
            return;
        }

        bool wasEnabled = agent.enabled;
        if (wasEnabled)
            agent.enabled = false;

        agent.agentTypeID = targetAgentTypeId;

        if (wasEnabled)
            agent.enabled = true;

        EnsureBoundToAgentNavMesh(agent, enemyInstance.transform.position, targetAgentTypeId);
    }

    private static string ResolveTargetAgentTypeName(IReadOnlyList<GameplayTag> tags)
    {
        if (HasTag(tags, FlyingTagName) || HasTag(tags, FloatingTagName))
            return FlyingAgentTypeName;

        if (HasTag(tags, LandingTagName))
            return HumanoidAgentTypeName;

        return HumanoidAgentTypeName;
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
                return true;
        }

        return false;
    }

    private static bool TryGetAgentTypeId(string agentTypeName, out int agentTypeId)
    {
        agentTypeId = -1;
        if (string.IsNullOrWhiteSpace(agentTypeName))
            return false;

        int settingsCount = NavMesh.GetSettingsCount();
        for (int i = 0; i < settingsCount; i++)
        {
            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(i);
            string currentName = NavMesh.GetSettingsNameFromID(settings.agentTypeID);
            if (!string.Equals(currentName, agentTypeName, StringComparison.OrdinalIgnoreCase))
                continue;

            agentTypeId = settings.agentTypeID;
            return true;
        }

        return false;
    }

    private static void EnsureBoundToAgentNavMesh(NavMeshAgent agent, Vector3 currentPosition, int agentTypeId)
    {
        if (agent == null || !agent.enabled)
            return;

        if (agent.isOnNavMesh)
            return;

        var filter = new NavMeshQueryFilter
        {
            agentTypeID = agentTypeId,
            areaMask = NavMesh.AllAreas
        };

        if (!NavMesh.SamplePosition(currentPosition, out NavMeshHit hit, NavMeshRebindMaxDistance, filter))
            return;

        if (!agent.Warp(hit.position))
            return;

        agent.nextPosition = hit.position;
        agent.ResetPath();
    }
}
