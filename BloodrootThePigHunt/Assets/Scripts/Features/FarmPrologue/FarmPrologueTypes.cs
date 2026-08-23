using System;
using UnityEngine;
using UnityEngine.Events;

namespace Bloodroot.Features.FarmPrologue
{
    public enum FarmProloguePhase
    {
        Inactive,
        WakeUp,
        Chores,
        Rumble,
        Combat,
        FadeToHub,
        Hub,
        CompletionPending,
        // Appended to preserve every serialized numeric phase value above.
        AwaitingOffering
    }

    public enum FarmInventoryConsumptionMode
    {
        KeepItems,
        ConsumeRequiredQuantity,
        ConsumeAllMatchingItems
    }

    [Serializable]
    public sealed class FarmProloguePhaseUnityEvent :
        UnityEvent<FarmProloguePhase>
    {
    }

    [Serializable]
    public sealed class FarmStringUnityEvent : UnityEvent<string>
    {
    }

    [Serializable]
    public sealed class FarmObjectiveProgressUnityEvent :
        UnityEvent<string, int, int>
    {
    }

    [Serializable]
    public sealed class FarmGameObjectUnityEvent : UnityEvent<GameObject>
    {
    }

    /// <summary>
    /// Keeps authored and runtime presentation failures from interrupting the
    /// authoritative prologue state machine. A broken listener is logged while
    /// the remaining lifecycle work continues.
    /// </summary>
    internal static class FarmPrologueEventUtility
    {
        public static void Invoke(
            UnityEvent callback,
            UnityEngine.Object context)
        {
            if (callback == null)
                return;

            try
            {
                callback.Invoke();
            }
            catch (Exception exception)
            {

            }
        }

        public static void Invoke<T>(
            UnityEvent<T> callback,
            T argument,
            UnityEngine.Object context)
        {
            if (callback == null)
                return;

            try
            {
                callback.Invoke(argument);
            }
            catch (Exception exception)
            {

            }
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

            try
            {
                callback.Invoke(first, second, third);
            }
            catch (Exception exception)
            {

            }
        }

        public static void Invoke(
            Action callback,
            UnityEngine.Object context)
        {
            if (callback == null)
                return;

            foreach (Action listener in callback.GetInvocationList())
            {
                try
                {
                    listener.Invoke();
                }
                catch (Exception exception)
                {

                }
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
                try
                {
                    listener.Invoke(argument);
                }
                catch (Exception exception)
                {

                }
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
                try
                {
                    listener.Invoke(first, second, third);
                }
                catch (Exception exception)
                {

                }
            }
        }
    }
}
