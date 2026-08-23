using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Small fail-closed navigation boundary for runtime enemies.  It keeps AI
/// controllers from touching path state until their agent is active and has a
/// valid NavMesh placement, and it can repair short placement drift without
/// writing warnings to the player log.
/// </summary>
public static class EnemyNavMeshSafety
{
    public static bool IsReady(NavMeshAgent agent)
    {
        return agent != null &&
               agent.isActiveAndEnabled &&
               agent.isOnNavMesh;
    }

    public static bool TryRecover(
        NavMeshAgent agent,
        Vector3 sourcePosition,
        float recoveryRadius)
    {
        if (agent == null || !agent.isActiveAndEnabled)
        {
            return false;
        }

        if (agent.isOnNavMesh)
        {
            return true;
        }

        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID,
            areaMask = agent.areaMask
        };
        if (!NavMesh.SamplePosition(
                sourcePosition,
                out NavMeshHit hit,
                Mathf.Max(0.1f, recoveryRadius),
                filter) ||
            !agent.Warp(hit.position) ||
            !agent.isOnNavMesh)
        {
            return false;
        }

        // A repaired placement must not continue following a path authored at
        // the invalid location.  The owning controller will choose its next
        // valid destination on its usual update cadence.
        agent.isStopped = true;
        agent.ResetPath();
        return true;
    }

    public static bool TrySetDestination(
        NavMeshAgent agent,
        Vector3 desiredPosition,
        float recoveryRadius,
        float destinationSampleRadius)
    {
        if (!TryRecover(agent, agent != null ? agent.transform.position : desiredPosition, recoveryRadius))
        {
            return false;
        }

        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID,
            areaMask = agent.areaMask
        };
        if (!NavMesh.SamplePosition(
                desiredPosition,
                out NavMeshHit hit,
                Mathf.Max(0.1f, destinationSampleRadius),
                filter))
        {
            Stop(agent);
            return false;
        }

        agent.isStopped = false;
        if (agent.SetDestination(hit.position))
        {
            return true;
        }

        Stop(agent);
        return false;
    }

    public static bool Stop(NavMeshAgent agent)
    {
        if (!IsReady(agent))
        {
            return false;
        }

        agent.isStopped = true;
        if (agent.hasPath)
        {
            agent.ResetPath();
        }

        return true;
    }
}
