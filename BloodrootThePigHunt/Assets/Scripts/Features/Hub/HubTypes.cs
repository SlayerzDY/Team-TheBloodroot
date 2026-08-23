using System;
using UnityEngine;
using UnityEngine.Events;

namespace Bloodroot.Features.Hub
{
    public enum HubStationId
    {
        MissionBoard = 0,
        Loadout = 1,
        Storage = 2,
        Upgrade = 3,
        Investigation = 4
    }

    [Serializable]
    public sealed class HubStationStateUnityEvent :
        UnityEvent<HubStationId, bool>
    {
    }

    [Serializable]
    public sealed class HubStringUnityEvent : UnityEvent<string>
    {
    }

    [Serializable]
    public sealed class HubLoadoutResultUnityEvent :
        UnityEvent<string, bool, string>
    {
    }

    internal static class HubEventUtility
    {
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
            T value,
            UnityEngine.Object context)
        {
            if (callback == null)
                return;

            foreach (Action<T> listener in callback.GetInvocationList())
            {
                try
                {
                    listener.Invoke(value);
                }
                catch (Exception exception)
                {

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

            foreach (Action<T1, T2> listener in
                     callback.GetInvocationList())
            {
                try
                {
                    listener.Invoke(first, second);
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
            T value,
            UnityEngine.Object context)
        {
            if (callback == null)
                return;

            try
            {
                callback.Invoke(value);
            }
            catch (Exception exception)
            {

            }
        }

        public static void Invoke<T1, T2>(
            UnityEvent<T1, T2> callback,
            T1 first,
            T2 second,
            UnityEngine.Object context)
        {
            if (callback == null)
                return;

            try
            {
                callback.Invoke(first, second);
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
    }
}
