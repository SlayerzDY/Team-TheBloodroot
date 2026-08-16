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
                transform.position += flightDirection * (currentSpeed * Time.deltaTime);
            }
        }

        private void FixedUpdate()
        {
            if (!resolved && projectileBody != null && !projectileBody.isKinematic)
            {
                ApplyBodyVelocity();
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
            if (resolved || other == null || other.isTrigger && !AlphaEnemyEventUtility.IsSameHierarchy(other.transform, assignedTarget))
            {
                return;
            }

            if (owner != null && AlphaEnemyEventUtility.IsSameHierarchy(other.transform, owner.transform))
            {
                return;
            }

            bool isAssignedTarget = assignedTarget != null &&
                                    AlphaEnemyEventUtility.IsSameHierarchy(other.transform, assignedTarget);
            if (!onlyDamageAssignedTarget || isAssignedTarget)
            {
                global::IDamage receiver = AlphaEnemyEventUtility.FindDamageReceiver(other.transform);
                if (receiver != null && damage > 0)
                {
                    receiver.TakeDamage(damage);
                    resolved = true;
                    Destroy(gameObject);
                    return;
                }
            }

            if (destroyOnAnySolidHit && !other.isTrigger)
            {
                resolved = true;
                Destroy(gameObject);
            }
        }

        private void OnValidate()
        {
            baseSpeed = Mathf.Max(0.01f, baseSpeed);
            turnRateDegrees = Mathf.Max(0f, turnRateDegrees);
            lifetime = Mathf.Max(0.05f, lifetime);
        }
    }
}
