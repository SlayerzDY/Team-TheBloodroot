using System;
using UnityEngine;
using UnityEngine.Events;

namespace Bloodroot.Features.AlphaEnemies
{
    public enum WereBoarState
    {
        Patrol,
        Investigate,
        Circle,
        Rush,
        Retreat,
        ReEntry,
        Dead
    }

    public enum WitchVariant
    {
        ShieldBearer,
        Summoner,
        Matriarch
    }

    /// <summary>
    /// Runtime moments exposed by <see cref="WitchController"/> for the
    /// audio pass. A cue is raised only after its underlying gameplay action
    /// has succeeded; it does not play, own, or require an audio asset.
    /// </summary>
    public enum WitchCombatAudioCue
    {
        DamageTaken,
        ProjectileAttack,
        MinionSummoned,
        ShieldBroken,
        HeartrootPulse,
        DeathStarted,
        AmbientStarted
    }

    public enum WitchEncounterState
    {
        Idle,
        Defending,
        AwaitingExtraction,
        Completed,
        Failed
    }

    public enum WitchMatriarchPhase
    {
        Ward,
        Conjure,
        HeartrootFury,
        Dead
    }

    [Serializable]
    public sealed class WereBoarControllerEvent : UnityEvent<WereBoarController> { }

    [Serializable]
    public sealed class WereBoarStateEvent : UnityEvent<WereBoarState> { }

    [Serializable]
    public sealed class WitchControllerEvent : UnityEvent<WitchController> { }

    [Serializable]
    public sealed class WitchMatriarchPhaseEvent : UnityEvent<WitchMatriarchPhase> { }

    [Serializable]
    public sealed class WitchSummonedHogEvent : UnityEvent<WitchSummonedHogAI> { }

    [Serializable]
    public sealed class WitchRootFragmentEvent : UnityEvent<WitchRootFragment> { }

    [Serializable]
    public sealed class WitchEncounterDirectorEvent : UnityEvent<WitchEncounterDirector> { }

    [Serializable]
    public sealed class WitchEncounterStateEvent : UnityEvent<WitchEncounterState> { }

    [Serializable]
    public sealed class GameObjectEvent : UnityEvent<GameObject> { }

    internal static class AlphaEnemyEventUtility
    {
        public static void Invoke(UnityEvent callback, UnityEngine.Object context, string eventName)
        {
            if (callback == null)
            {
                return;
            }

            try
            {
                callback.Invoke();
            }
            catch (Exception exception)
            {
                LogSubscriberException(context, eventName, exception);
            }
        }

        public static void Invoke<T>(UnityEvent<T> callback, T value, UnityEngine.Object context, string eventName)
        {
            if (callback == null)
            {
                return;
            }

            try
            {
                callback.Invoke(value);
            }
            catch (Exception exception)
            {
                LogSubscriberException(context, eventName, exception);
            }
        }

        public static void Invoke(Action callback, UnityEngine.Object context, string eventName)
        {
            if (callback == null)
            {
                return;
            }

            foreach (Delegate subscriber in callback.GetInvocationList())
            {
                try
                {
                    ((Action)subscriber).Invoke();
                }
                catch (Exception exception)
                {
                    LogSubscriberException(context, eventName, exception);
                }
            }
        }

        public static void Invoke<T>(Action<T> callback, T value, UnityEngine.Object context, string eventName)
        {
            if (callback == null)
            {
                return;
            }

            foreach (Delegate subscriber in callback.GetInvocationList())
            {
                try
                {
                    ((Action<T>)subscriber).Invoke(value);
                }
                catch (Exception exception)
                {
                    LogSubscriberException(context, eventName, exception);
                }
            }
        }

        public static bool IsSameHierarchy(Transform first, Transform second)
        {
            return first != null &&
                   second != null &&
                   (first == second || first.IsChildOf(second) || second.IsChildOf(first));
        }

        public static global::IDamage FindDamageReceiver(Transform target)
        {
            if (target == null)
            {
                return null;
            }

            global::IDamage receiver = target.GetComponent<global::IDamage>();
            if (receiver != null)
            {
                return receiver;
            }

            receiver = target.GetComponentInParent<global::IDamage>();
            return receiver ?? target.GetComponentInChildren<global::IDamage>();
        }

        private static void LogSubscriberException(UnityEngine.Object context, string eventName, Exception exception)
        {
            Debug.LogError($"{context?.name ?? "Alpha enemy"}: subscriber for {eventName} threw an exception. Remaining subscribers were protected.", context);
            Debug.LogException(exception, context);
        }
    }
}
