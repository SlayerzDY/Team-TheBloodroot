using System;
using UnityEngine;
using UnityEngine.Events;

namespace Bloodroot.Features.WorldMissions
{
    public enum WorldMissionState
    {
        Inactive,
        Running,
        CompletionPending,
        Completed
    }

    public enum WorldMissionObjectiveState
    {
        Inactive,
        Available,
        Completed
    }

    public enum WorldMissionInteractionKind
    {
        Generic,
        RelayRestoration,
        SampleRecovery,
        PowerRestoration,
        CursedObject,
        AltarActivation,
        HeartrootRecovery,
        Extraction
    }

    public enum WorldMissionDefenseCompletionMode
    {
        DurationOnly,
        KillCountOnly,
        DurationAndKillCount,
        DurationOrKillCount
    }

    [Serializable]
    public sealed class WorldMissionStateUnityEvent :
        UnityEvent<WorldMissionState>
    {
    }

    [Serializable]
    public sealed class WorldMissionObjectiveStateUnityEvent :
        UnityEvent<WorldMissionObjectiveState>
    {
    }

    [Serializable]
    public sealed class WorldMissionObjectiveUnityEvent :
        UnityEvent<WorldMissionObjective>
    {
    }

    [Serializable]
    public sealed class WorldMissionStringUnityEvent : UnityEvent<string>
    {
    }

    [Serializable]
    public sealed class WorldMissionBoolUnityEvent : UnityEvent<bool>
    {
    }

    [Serializable]
    public sealed class WorldMissionGameObjectUnityEvent :
        UnityEvent<GameObject>
    {
    }

    [Serializable]
    public sealed class WorldMissionObjectiveProgressUnityEvent :
        UnityEvent<string, int, int>
    {
    }

    [Serializable]
    public sealed class WorldMissionDefenseProgressUnityEvent :
        UnityEvent<float, float, int, int>
    {
    }

    /// <summary>
    /// Invokes authored and runtime listeners without allowing a presentation
    /// failure to interrupt authoritative mission state.
    /// </summary>
    internal static class WorldMissionEventUtility
    {
        public static void Invoke(
            UnityEvent callback,
            UnityEngine.Object context)
        {
            if (callback == null)
                return;
            callback.Invoke();
            //try
            //{

            //}
            //catch (Exception exception)
            //{

            //}
        }

        public static void Invoke<T>(
            UnityEvent<T> callback,
            T argument,
            UnityEngine.Object context)
        {
            if (callback == null)
                return;
            callback.Invoke(argument);
            //try
            //{

            //}
            //catch (Exception exception)
            //{

            //}
        }

        public static void Invoke<T1, T2, T3>(
            UnityEvent<T1, T2, T3> callback,
            T1 first,
            T2 second,
            T3 third,
            UnityEngine.Object context)
        {
            if (callback == null)
                return;
            callback.Invoke(first, second, third);
            //try
            //{

            //}
            //catch (Exception exception)
            //{

            //}
        }

        public static void Invoke<T1, T2, T3, T4>(
            UnityEvent<T1, T2, T3, T4> callback,
            T1 first,
            T2 second,
            T3 third,
            T4 fourth,
            UnityEngine.Object context)
        {
            if (callback == null)
                return;
            callback.Invoke(first, second, third, fourth);
            //try
            //{

            //}
            //catch (Exception exception)
            //{

            //}
        }

        public static void Invoke(
            Action callback,
            UnityEngine.Object context)
        {
            if (callback == null)
                return;

            foreach (Action listener in callback.GetInvocationList())
            {
                listener.Invoke();
                //try
                //{

                //}
                //catch (Exception exception)
                //{

                //}
            }
        }

        public static void Invoke<T>(
            Action<T> callback,
            T argument,
            UnityEngine.Object context)
        {
            if (callback == null)
                return;

            foreach (Action<T> listener in callback.GetInvocationList())
            {
                listener.Invoke(argument);
                //try
                //{

                //}
                //catch (Exception exception)
                //{

                //}
            }
        }

        public static void Invoke<T1, T2, T3>(
            Action<T1, T2, T3> callback,
            T1 first,
            T2 second,
            T3 third,
            UnityEngine.Object context)
        {
            if (callback == null)
                return;

            foreach (Action<T1, T2, T3> listener in
                     callback.GetInvocationList())
            {
                listener.Invoke(first, second, third);
                //try
                //{

                //}
                //catch (Exception exception)
                //{

                //}
            }
        }

        public static void Invoke<T1, T2, T3, T4>(
            Action<T1, T2, T3, T4> callback,
            T1 first,
            T2 second,
            T3 third,
            T4 fourth,
            UnityEngine.Object context)
        {
            if (callback == null)
                return;

            foreach (Action<T1, T2, T3, T4> listener in
                     callback.GetInvocationList())
            {
                listener.Invoke(first, second, third, fourth);
                //try
                //{

                //}
                //catch (Exception exception)
                //{

                //}
            }
        }
    }
}
