using System;
using UnityEngine;

namespace Bloodroot.Campaign
{
    /// <summary>
    /// Dispatches lifecycle notifications without allowing one presentation or
    /// integration listener to interrupt authoritative gameplay state.
    /// </summary>
    internal static class CampaignEventUtility
    {
        public static void Invoke(Action callback, UnityEngine.Object context)
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
                    Debug.LogException(exception, context);
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
                    Debug.LogException(exception, context);
                }
            }
        }

        public static void Invoke<T1, T2>(
            Action<T1, T2> callback,
            T1 first,
            T2 second,
            UnityEngine.Object context)
        {
            if (callback == null)
                return;

            foreach (Action<T1, T2> listener in callback.GetInvocationList())
            {
                try
                {
                    listener.Invoke(first, second);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, context);
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
                    Debug.LogException(exception, context);
                }
            }
        }
    }
}
