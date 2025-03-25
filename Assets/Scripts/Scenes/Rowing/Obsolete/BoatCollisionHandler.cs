using UnityEngine;

namespace Rowing
{
    [RequireComponent(typeof(Rigidbody))]
    public class BoatCollisionHandler : MonoBehaviour
    {
        [Header("Collision Detection")]
        [Tooltip("The same float points used by BoatBuoyancy")]
        public Transform[] collisionPoints;
        [Tooltip("Distance to check for terrain and water boundaries")]
        public float checkDistance = 0.5f;

        [Header("Terrain Collision Settings")]
        [Tooltip("Force applied to push boat away from terrain")]
        public float terrainBounceForce = 20f;
        [Tooltip("Layer mask for terrain collision detection")]
        public LayerMask terrainLayer;
        [Tooltip("How much velocity is preserved after terrain collision (0-1)")]
        public float terrainBounceFactor = 0.3f;

        [Header("Water Collision Settings")]
        [Tooltip("Force applied to keep boat on water surface")]
        public float waterSurfaceForce = 50f;  // Increased from 15f
        [Tooltip("Layer mask for water surface collision detection")]
        public LayerMask waterLayer;
        [Tooltip("How much velocity is preserved after water surface collision (0-1)")]
        public float waterBounceFactor = 0.2f;  // Decreased to provide stronger damping
        [Tooltip("Water surface height from scene")]
        public float waterHeight = 0f;
        [Tooltip("Additional offset for water surface collision detection")]
        public float waterSurfaceOffset = 0.5f;  // Increased from 0.15f
        [Tooltip("Additional force to prevent sinking")]
        public float antiSinkForce = 80f;  // New parameter

        [Header("Feedback")]
        [Tooltip("Minimum impact velocity to trigger collision effects")]
        public float minImpactVelocity = 2.0f;

        // References
        private Rigidbody rb;
        private BoatBuoyancy buoyancy;

        // Debugging
        public bool debugMode = true;
        private int belowWaterPoints = 0;

        private void Start()
        {
            rb = GetComponent<Rigidbody>();
            buoyancy = GetComponent<BoatBuoyancy>();

            // Ensure proper physics setup for collisions
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Ensure the boat has a collider
            if (GetComponent<Collider>() == null)
            {
                Debug.LogWarning("Boat is missing a collider - adding BoxCollider");
                BoxCollider collider = gameObject.AddComponent<BoxCollider>();
                collider.center = Vector3.zero;
                Renderer rend = GetComponent<Renderer>();
                if (rend != null)
                {
                    collider.size = rend.bounds.size;
                }
            }

            // Share float points with buoyancy component if not set
            if (collisionPoints == null || collisionPoints.Length == 0)
            {
                if (buoyancy != null && buoyancy.floatPoints.Length > 0)
                {
                    collisionPoints = buoyancy.floatPoints;
                }
            }

            // Initialize layers if not set
            if (terrainLayer.value == 0)
            {
                terrainLayer = LayerMask.GetMask("Terrain");
            }
            if (waterLayer.value == 0)
            {
                waterLayer = LayerMask.GetMask("Water");
            }

            // Get water height from buoyancy if available
            if (buoyancy != null)
            {
                waterHeight = buoyancy.waterHeight;
            }
        }

        private void FixedUpdate()
        {
            HandleTerrainCollision();
            HandleWaterSurfaceCollision();

            // Emergency anti-sink measure
            if (transform.position.y < waterHeight - 1.0f)
            {
                ApplyEmergencyBuoyancy();
            }
        }

        private void ApplyEmergencyBuoyancy()
        {
            // Apply a strong upward force to prevent complete sinking
            rb.AddForce(Vector3.up * antiSinkForce, ForceMode.Acceleration);

            if (debugMode)
            {
                Debug.Log("Emergency buoyancy applied - boat is too far below water!");
            }
        }

        private void HandleTerrainCollision()
        {
            if (collisionPoints == null || collisionPoints.Length == 0)
                return;

            // Cast rays from each collision point to detect terrain
            foreach (Transform point in collisionPoints)
            {
                if (point == null) continue;

                // Cast ray downward
                if (Physics.Raycast(point.position, Vector3.down, out RaycastHit hitDown, checkDistance, terrainLayer))
                {
                    ApplyTerrainCollisionResponse(hitDown, point);
                }

                // Cast ray in the movement direction
                Vector3 forwardDir = rb.linearVelocity.normalized;
                if (forwardDir.magnitude > 0.01f &&
                    Physics.Raycast(point.position, forwardDir, out RaycastHit hitForward, checkDistance, terrainLayer))
                {
                    ApplyTerrainCollisionResponse(hitForward, point);
                }
            }
        }

        private void ApplyTerrainCollisionResponse(RaycastHit hit, Transform collisionPoint)
        {
            // Calculate reflection direction
            Vector3 reflectionDir = Vector3.Reflect(rb.linearVelocity, hit.normal);

            // Apply bounce force away from terrain
            Vector3 bounceForce = hit.normal * terrainBounceForce;
            rb.AddForceAtPosition(bounceForce, collisionPoint.position, ForceMode.Impulse);

            // Reduce velocity based on bounce factor
            rb.linearVelocity *= terrainBounceFactor;

            // Play collision sound if impact is significant
            if (rb.linearVelocity.magnitude > minImpactVelocity)
            {
                // If you have an audio component, you could play a collision sound here
                Debug.Log("Terrain collision detected at velocity: " + rb.linearVelocity.magnitude);
            }
        }

        private void HandleWaterSurfaceCollision()
        {
            if (collisionPoints == null || collisionPoints.Length == 0)
                return;

            belowWaterPoints = 0;
            foreach (Transform point in collisionPoints)
            {
                if (point == null) continue;

                // Check if point is below water level
                if (point.position.y < waterHeight)
                {
                    belowWaterPoints++;
                    ApplyWaterSurfaceForce(point);
                }
                else
                {
                    // For points above water, still check for water surface below
                    if (Physics.Raycast(point.position, Vector3.down, out RaycastHit hitWater, checkDistance, waterLayer))
                    {
                        // We're close to water but not yet below - apply gentle upward force if moving down
                        if (rb.linearVelocity.y < -0.5f)
                        {
                            Vector3 dampingForce = -rb.linearVelocity.y * Vector3.up * waterSurfaceForce * 0.5f;
                            rb.AddForceAtPosition(dampingForce, point.position, ForceMode.Force);
                        }
                    }
                }
            }

            // Dampen downward velocity more aggressively when any point is below water
            if (belowWaterPoints > 0 && rb.linearVelocity.y < 0)
            {
                Vector3 currentVel = rb.linearVelocity;
                currentVel.y *= waterBounceFactor;
                rb.linearVelocity = currentVel;
            }

            if (debugMode && belowWaterPoints > 0)
            {
                Debug.Log($"Points below water: {belowWaterPoints}");
            }
        }

        private void ApplyWaterSurfaceForce(Transform point)
        {
            // Calculate depth below water surface
            float depthBelowSurface = waterHeight - point.position.y;

            // Apply stronger force the deeper we are
            float forceMagnitude = Mathf.Clamp(depthBelowSurface / waterSurfaceOffset, 0.2f, 2.0f) * waterSurfaceForce;

            // Apply upward force to keep boat on water
            Vector3 surfaceForce = Vector3.up * forceMagnitude;
            rb.AddForceAtPosition(surfaceForce, point.position, ForceMode.Force);
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Check if we're colliding with water (in case the water has a collider)
            if (((1 << collision.gameObject.layer) & waterLayer) != 0)
            {
                // Dampen velocity on water impact
                rb.linearVelocity *= waterBounceFactor;

                if (debugMode)
                {
                    Debug.Log("Direct water collision detected");
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (collisionPoints == null) return;

            // Draw terrain collision check rays
            Gizmos.color = Color.red;
            foreach (Transform point in collisionPoints)
            {
                if (point != null)
                {
                    // Terrain check rays
                    Gizmos.DrawLine(point.position, point.position + Vector3.down * checkDistance);

                    // Forward check rays (if in play mode)
                    if (Application.isPlaying && rb != null && rb.linearVelocity.magnitude > 0.01f)
                    {
                        Vector3 forwardDir = rb.linearVelocity.normalized;
                        Gizmos.DrawLine(point.position, point.position + forwardDir * checkDistance);
                    }

                    // Water surface visualization
                    Gizmos.color = Color.blue;
                    Vector3 waterSurfacePos = point.position;
                    waterSurfacePos.y = waterHeight;
                    Gizmos.DrawSphere(waterSurfacePos, 0.05f);

                    // Reset color for next iteration
                    Gizmos.color = Color.red;
                }
            }

            // Draw water level plane
            Gizmos.color = new Color(0, 0.5f, 1f, 0.3f);
            Vector3 center = transform.position;
            center.y = waterHeight;
            Gizmos.DrawCube(center, new Vector3(3f, 0.01f, 3f));
        }
    }
}
