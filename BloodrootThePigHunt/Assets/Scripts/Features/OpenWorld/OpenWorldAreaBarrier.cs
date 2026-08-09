using System;
using UnityEngine;

namespace Bloodroot.OpenWorld
{
    public enum OpenWorldAreaId
    {
        StillwaterFeedMill = 1,
        HarrowEstate = 2,
        BloodrootHollow = 3
    }

    /// <summary>
    /// Owns the invisible colliders that keep a progression area locked.
    /// A future progression/save manager can call SetUnlocked after loading
    /// the player's completed-area state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OpenWorldAreaBarrier : MonoBehaviour
    {
        [SerializeField]
        private OpenWorldAreaId area;

        [SerializeField]
        private bool startsUnlocked;

        [SerializeField]
        private GameObject lockedFeedbackTrigger;

        [SerializeField]
        private Collider[] blockingColliders = Array.Empty<Collider>();

        private bool isUnlocked;

        public OpenWorldAreaId Area => area;

        public bool StartsUnlocked => startsUnlocked;

        public bool IsUnlocked => isUnlocked;

        public GameObject LockedFeedbackTrigger => lockedFeedbackTrigger;

        public int BlockingColliderCount => blockingColliders?.Length ?? 0;

        private void Awake()
        {
            SetUnlocked(startsUnlocked);
        }

        public void Unlock()
        {
            SetUnlocked(true);
        }

        public void Lock()
        {
            SetUnlocked(false);
        }

        public void SetUnlocked(bool unlocked)
        {
            isUnlocked = unlocked;

            Collider[] ownedColliders =
                blockingColliders ?? Array.Empty<Collider>();

            foreach (Collider barrierCollider in ownedColliders)
            {
                if (barrierCollider != null)
                {
                    barrierCollider.enabled = !unlocked;
                }
            }

            if (lockedFeedbackTrigger != null)
            {
                lockedFeedbackTrigger.SetActive(!unlocked);
            }
        }

        public void Configure(
            OpenWorldAreaId areaId,
            bool unlockedAtStart,
            GameObject feedbackTrigger,
            Collider[] ownedBlockingColliders)
        {
            area = areaId;
            startsUnlocked = unlockedAtStart;
            lockedFeedbackTrigger = feedbackTrigger;
            blockingColliders =
                ownedBlockingColliders ?? Array.Empty<Collider>();
        }

        public bool OwnsCollider(Collider candidate)
        {
            return candidate != null && blockingColliders != null &&
                   Array.IndexOf(blockingColliders, candidate) >= 0;
        }

        private void OnDrawGizmosSelected()
        {
            Color previousColor = Gizmos.color;
            Gizmos.color = new Color(0.85f, 0.12f, 0.2f, 0.8f);

            Collider[] ownedColliders =
                blockingColliders ?? Array.Empty<Collider>();

            foreach (Collider ownedCollider in ownedColliders)
            {
                if (ownedCollider is not BoxCollider barrierCollider)
                {
                    continue;
                }

                Matrix4x4 previousMatrix = Gizmos.matrix;
                Gizmos.matrix = barrierCollider.transform.localToWorldMatrix;
                Gizmos.DrawWireCube(
                    barrierCollider.center,
                    barrierCollider.size);
                Gizmos.matrix = previousMatrix;
            }

            Gizmos.color = previousColor;
        }
    }
}
