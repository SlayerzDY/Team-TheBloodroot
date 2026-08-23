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
                    if (!unlocked)
                    {
                        RestoreLockedColliderContract(barrierCollider);
                    }

                    barrierCollider.enabled = !unlocked;
                }
            }

            if (lockedFeedbackTrigger != null)
            {
                lockedFeedbackTrigger.SetActive(!unlocked);
            }
        }

        /// <summary>
        /// Verifies both the logical state and the physical collider state.
        /// Campaign progression uses this as a fail-closed runtime integrity
        /// check so an unrelated component cannot silently disable a locked
        /// boundary.
        /// </summary>
        public bool MatchesUnlockedState(bool expectedUnlocked)
        {
            if (isUnlocked != expectedUnlocked)
            {
                return false;
            }

            Collider[] ownedColliders =
                blockingColliders ?? Array.Empty<Collider>();

            if (ownedColliders.Length == 0)
            {
                return false;
            }

            foreach (Collider barrierCollider in ownedColliders)
            {
                if (barrierCollider == null ||
                    barrierCollider.enabled == expectedUnlocked)
                {
                    return false;
                }

                if (!expectedUnlocked &&
                    !MatchesLockedColliderContract(barrierCollider))
                {
                    return false;
                }
            }

            return lockedFeedbackTrigger == null ||
                   lockedFeedbackTrigger.activeSelf != expectedUnlocked;
        }

        /// <summary>
        /// Supplies a deterministic second line of defense for the authored
        /// campaign cutlines. The physical colliders remain authoritative,
        /// but a locked player found beyond a cutline is placed back on its
        /// unlocked side even if a CharacterController contact was skipped.
        /// </summary>
        public bool TryConstrainToUnlockedSide(
            Vector3 worldPosition,
            float clearance,
            out Vector3 constrainedPosition)
        {
            constrainedPosition = worldPosition;

            if (isUnlocked || !TryGetBlockingBounds(out Bounds bounds))
            {
                return false;
            }

            float safeClearance = Mathf.Max(0f, clearance);

            switch (area)
            {
                case OpenWorldAreaId.StillwaterFeedMill:
                {
                    float maximumUnlockedX = bounds.min.x - safeClearance;
                    if (worldPosition.x <= maximumUnlockedX)
                    {
                        return false;
                    }

                    constrainedPosition.x = maximumUnlockedX;
                    return true;
                }
                case OpenWorldAreaId.HarrowEstate:
                case OpenWorldAreaId.BloodrootHollow:
                {
                    float maximumUnlockedZ = bounds.min.z - safeClearance;
                    if (worldPosition.z <= maximumUnlockedZ)
                    {
                        return false;
                    }

                    constrainedPosition.z = maximumUnlockedZ;
                    return true;
                }
                default:
                    return false;
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

        private static void RestoreLockedColliderContract(
            Collider barrierCollider)
        {
            Transform current = barrierCollider.transform;
            OpenWorldAreaBarrier owner =
                barrierCollider.GetComponentInParent<OpenWorldAreaBarrier>(
                    true);
            Transform highestOwnedTransform =
                owner != null && owner.transform.parent != null
                    ? owner.transform.parent
                    : owner != null
                        ? owner.transform
                        : barrierCollider.transform;

            while (current != null)
            {
                if (!current.gameObject.activeSelf)
                {
                    current.gameObject.SetActive(true);
                }

                if (current == highestOwnedTransform)
                {
                    break;
                }

                current = current.parent;
            }

            int borderLayer = LayerMask.NameToLayer("Border");
            int playerLayer = LayerMask.NameToLayer("Player");

            if (borderLayer >= 0)
            {
                barrierCollider.gameObject.layer = borderLayer;
            }

            barrierCollider.isTrigger = false;
            barrierCollider.includeLayers = 0;
            barrierCollider.excludeLayers = 0;

            if (borderLayer >= 0 && playerLayer >= 0 &&
                Physics.GetIgnoreLayerCollision(playerLayer, borderLayer))
            {
                Physics.IgnoreLayerCollision(
                    playerLayer,
                    borderLayer,
                    false);
            }
        }

        private static bool MatchesLockedColliderContract(
            Collider barrierCollider)
        {
            int borderLayer = LayerMask.NameToLayer("Border");
            int playerLayer = LayerMask.NameToLayer("Player");

            return barrierCollider.gameObject.activeInHierarchy &&
                   !barrierCollider.isTrigger &&
                   borderLayer >= 0 &&
                   playerLayer >= 0 &&
                   barrierCollider.gameObject.layer == borderLayer &&
                   barrierCollider.includeLayers.value == 0 &&
                   barrierCollider.excludeLayers.value == 0 &&
                   !Physics.GetIgnoreLayerCollision(
                       playerLayer,
                       borderLayer);
        }

        private bool TryGetBlockingBounds(out Bounds combinedBounds)
        {
            combinedBounds = default;
            bool hasBounds = false;
            Collider[] ownedColliders =
                blockingColliders ?? Array.Empty<Collider>();

            foreach (Collider ownedCollider in ownedColliders)
            {
                if (ownedCollider == null ||
                    !TryGetWorldBounds(ownedCollider, out Bounds worldBounds))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combinedBounds = worldBounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(worldBounds);
                }
            }

            return hasBounds;
        }

        private static bool TryGetWorldBounds(
            Collider ownedCollider,
            out Bounds worldBounds)
        {
            if (ownedCollider is BoxCollider boxCollider)
            {
                Vector3 halfSize = boxCollider.size * 0.5f;
                Vector3 firstCorner = boxCollider.transform.TransformPoint(
                    boxCollider.center - halfSize);
                worldBounds = new Bounds(firstCorner, Vector3.zero);

                for (int x = 0; x <= 1; x++)
                {
                    for (int y = 0; y <= 1; y++)
                    {
                        for (int z = 0; z <= 1; z++)
                        {
                            Vector3 localCorner = boxCollider.center +
                                new Vector3(
                                    x == 0 ? -halfSize.x : halfSize.x,
                                    y == 0 ? -halfSize.y : halfSize.y,
                                    z == 0 ? -halfSize.z : halfSize.z);
                            worldBounds.Encapsulate(
                                boxCollider.transform.TransformPoint(
                                    localCorner));
                        }
                    }
                }

                return true;
            }

            if (ownedCollider.enabled &&
                ownedCollider.gameObject.activeInHierarchy)
            {
                worldBounds = ownedCollider.bounds;
                return true;
            }

            worldBounds = default;
            return false;
        }

    }
}
