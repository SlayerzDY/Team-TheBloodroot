using UnityEngine;

namespace Bloodroot.Features.AlphaEnemies
{
    [DisallowMultipleComponent]
    public sealed class AlphaEnemyProjectile : MonoBehaviour
    {
        [Header("Authored References")]
        [SerializeField] private Rigidbody projectileBody;

        [Header("Flight")]
        [SerializeField, Min(0.01f)] private float baseSpeed = 16f;
        [SerializeField] private bool homeOnAssignedTarget = true;
        [SerializeField, Min(0f)] private float turnRateDegrees = 160f;
        [SerializeField, Min(0.05f)] private float lifetime = 8f;

        [Header("Collision")]
        [SerializeField] private bool onlyDamageAssignedTarget = true;
        [SerializeField] private bool destroyOnAnySolidHit = true;

        private GameObject owner;
        private Transform assignedTarget;
        private int damage;
        private float currentSpeed;
        private Vector3 flightDirection;
        private float expiresAt;
        private bool resolved;
        private bool homingEnabled;
        private SphereCollider projectileCollider;
        private readonly RaycastHit[] sweepHits = new RaycastHit[16];

        public void Configure(GameObject projectileOwner, Transform target, int configuredDamage, float speedScalar = 1f)
        {
            ConfigureDirectional(
                projectileOwner,
                target,
                configuredDamage,
                GetDirectionToTarget(target),
                speedScalar,
                homeOnAssignedTarget);
        }

        public void ConfigureDirectional(
            GameObject projectileOwner,
            Transform target,
            int configuredDamage,
            Vector3 initialDirection,
            float speedScalar = 1f,
            bool homeOnTarget = false)
        {
            owner = projectileOwner;
            assignedTarget = target;
            damage = Mathf.Max(0, configuredDamage);
            currentSpeed = baseSpeed * Mathf.Max(0.01f, speedScalar);
            flightDirection = initialDirection.sqrMagnitude > 0.0001f
                ? initialDirection.normalized
                : GetInitialDirection();
            homingEnabled = homeOnTarget;
            expiresAt = Time.time + lifetime;
            transform.rotation = Quaternion.LookRotation(flightDirection, Vector3.up);
            ApplyBodyVelocity();
        }

        private void Awake()
        {
            if (projectileBody == null)
            {
                projectileBody = GetComponent<Rigidbody>();
            }

            projectileCollider = GetComponent<SphereCollider>();
            currentSpeed = baseSpeed;
            flightDirection = transform.forward;
            homingEnabled = homeOnAssignedTarget;
            expiresAt = Time.time + lifetime;
        }

        private void Update()
        {
            if (resolved)
            {
                return;
            }

            if (Time.time >= expiresAt)
            {
                resolved = true;
                Destroy(gameObject);
                return;
            }

            UpdateDirection(Time.deltaTime);
            if (projectileBody == null || projectileBody.isKinematic)
            {
                Vector3 movement = flightDirection * (currentSpeed * Time.deltaTime);
                CheckFlightPath(movement);
                if (!resolved)
                {
                    transform.position += movement;
                }
            }
        }

        private void FixedUpdate()
        {
            if (!resolved && projectileBody != null && !projectileBody.isKinematic)
            {
                CheckFlightPath(flightDirection * (currentSpeed * Time.fixedDeltaTime));
                if (!resolved)
                {
                    ApplyBodyVelocity();
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            ResolveHit(other);
        }

        private void OnCollisionEnter(Collision collision)
        {
            ResolveHit(collision == null ? null : collision.collider);
        }

        private void UpdateDirection(float deltaTime)
        {
            if (!homingEnabled || assignedTarget == null)
            {
                return;
            }

            Vector3 desiredDirection = assignedTarget.position - transform.position;
            if (desiredDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            desiredDirection.Normalize();
            flightDirection = Vector3.RotateTowards(
                flightDirection,
                desiredDirection,
                turnRateDegrees * Mathf.Deg2Rad * deltaTime,
                0f).normalized;
            transform.rotation = Quaternion.LookRotation(flightDirection, Vector3.up);
        }

        private Vector3 GetInitialDirection()
        {
            if (assignedTarget == null)
            {
                return transform.forward;
            }

            Vector3 direction = assignedTarget.position - transform.position;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        }

        private Vector3 GetDirectionToTarget(Transform target)
        {
            if (target == null)
            {
                return transform.forward;
            }

            Vector3 direction = target.position - transform.position;
            return direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : transform.forward;
        }

        private void ApplyBodyVelocity()
        {
            if (projectileBody != null && !projectileBody.isKinematic)
            {
                projectileBody.linearVelocity = flightDirection * currentSpeed;
            }
        }

        private void ResolveHit(Collider other)
        {
            if (resolved || !TryGetHitReceiver(other, out global::IDamage receiver))
            {
                return;
            }

            // Commit the hit before damage callbacks or another collider can resolve it again.
            resolved = true;
            if (projectileBody != null && !projectileBody.isKinematic)
            {
                projectileBody.linearVelocity = Vector3.zero;
            }
            Destroy(gameObject);
            if (receiver != null && damage > 0)
            {
                receiver.TakeDamage(damage);
            }
        }

        private void CheckFlightPath(Vector3 movement)
        {
            if (projectileCollider == null || !projectileCollider.enabled)
            {
                return;
            }

            float distance = movement.magnitude;
            Vector3 scale = projectileCollider.transform.lossyScale;
            float radius = projectileCollider.radius * Mathf.Max(
                Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            if (distance <= 0f || radius <= 0f)
            {
                return;
            }

            Vector3 origin = projectileCollider.transform.TransformPoint(projectileCollider.center);
            if (projectileBody != null && !projectileBody.isKinematic)
            {
                origin += projectileBody.position - transform.position;
            }

            // Trigger callbacks can miss a whole crossing between physics steps.
            // Include triggers explicitly: this project's global queries ignore them.
            Vector3 direction = movement / distance;
            RaycastHit[] hits = sweepHits;
            int hitCount = Physics.SphereCastNonAlloc(
                origin, radius, direction, hits, distance, Physics.AllLayers, QueryTriggerInteraction.Collide);
            if (hitCount == hits.Length)
            {
                // A full buffer is not guaranteed to contain the nearest obstruction.
                hits = Physics.SphereCastAll(
                    origin, radius, direction, distance, Physics.AllLayers, QueryTriggerInteraction.Collide);
                hitCount = hits.Length;
            }

            Collider nearestCollider = null;
            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = hits[index];
                if (hit.distance < nearestDistance && TryGetHitReceiver(hit.collider, out _))
                {
                    nearestCollider = hit.collider;
                    nearestDistance = hit.distance;
                }
            }

            ResolveHit(nearestCollider);
        }

        private bool TryGetHitReceiver(Collider other, out global::IDamage receiver)
        {
            receiver = null;
            if (other == null || AlphaEnemyEventUtility.IsSameHierarchy(other.transform, transform))
            {
                return false;
            }

            if (owner != null && AlphaEnemyEventUtility.IsSameHierarchy(other.transform, owner.transform))
            {
                return false;
            }

            if (projectileCollider != null &&
                (Physics.GetIgnoreLayerCollision(projectileCollider.gameObject.layer, other.gameObject.layer) ||
                 Physics.GetIgnoreCollision(projectileCollider, other)))
            {
                return false;
            }

            bool isAssignedTarget = assignedTarget != null &&
                                    AlphaEnemyEventUtility.IsSameHierarchy(other.transform, assignedTarget);
            if (other.isTrigger && !isAssignedTarget)
            {
                return false;
            }

            if (!onlyDamageAssignedTarget || isAssignedTarget)
            {
                receiver = AlphaEnemyEventUtility.FindDamageReceiver(other.transform);
            }

            return receiver != null && damage > 0 || destroyOnAnySolidHit && !other.isTrigger;
        }

        private void OnValidate()
        {
            baseSpeed = Mathf.Max(0.01f, baseSpeed);
            turnRateDegrees = Mathf.Max(0f, turnRateDegrees);
            lifetime = Mathf.Max(0.05f, lifetime);
        }
    }
}
