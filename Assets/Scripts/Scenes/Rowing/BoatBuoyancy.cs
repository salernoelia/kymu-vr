using UnityEngine;

namespace Rowing
{
    [RequireComponent(typeof(Rigidbody))]
    public class BoatBuoyancy : MonoBehaviour
    {
        [Header("Buoyancy Settings")]
        public Transform[] floatPoints;
        public float buoyancyForce = 20f;
        public float waterHeight = 0f;
        public Rigidbody rb;
        public float waterLevelOffset = 0f;
        [Tooltip("Stronger damping makes the boat more stable")]
        public float dampingFactor = 0.05f;
        [Tooltip("Affects force calculation - higher values provide more lift")]
        public float waterDensity = 1000f;

        [Header("Paddling Controls")]
        public Transform leftPaddle;
        public Transform rightPaddle;
        public float paddleForce = 500f;
        public float steeringFactor = 2.0f;
        public float maxSpeed = 50f;
        public float paddleMultiplier = 25f;
        public float rotationSpeed = 30f;

        [Header("Advanced Steering")]
        [Tooltip("Controls how boat turns when paddling on one side only")]
        public float asymmetricSteeringFactor = 1.5f;
        [Tooltip("Controls how quickly the boat responds to steering input")]
        public float steeringResponseTime = 0.1f;
        [Tooltip("Reduces forward speed while turning")]
        public float turnDragFactor = 0.8f;

        [Header("Stabilization")]
        public float waterDrag = 0.8f;
        public float waterAngularDrag = 5.0f;

        // Advanced buoyancy
        private float _baseDrag;
        private float _baseAngularDrag;
        private Vector3[] _velocities;
        private float _percentSubmerged = 0f;

        private Vector3 previousLeftPaddlePos;
        private Vector3 previousRightPaddlePos;
        private bool isLeftPaddleInWater = false;
        private bool isRightPaddleInWater = false;
        private float turnDirection = 0f;
        private float currentTurnVelocity = 0f;
        private float forwardSpeed = 0f;

        private void Start()
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody>();
            }

            _baseDrag = rb.linearDamping;
            _baseAngularDrag = rb.angularDamping;

            // Ensure we have some float points
            if (floatPoints == null || floatPoints.Length == 0)
            {
                Debug.LogWarning("No float points defined - adding default point at center");
                GameObject pointObj = new GameObject("FloatPoint");
                pointObj.transform.parent = transform;
                pointObj.transform.localPosition = Vector3.zero;
                floatPoints = new Transform[] { pointObj.transform };
            }

            // Initialize velocity array to track each point's velocity
            _velocities = new Vector3[floatPoints.Length];

            previousLeftPaddlePos = leftPaddle ? leftPaddle.position : Vector3.zero;
            previousRightPaddlePos = rightPaddle ? rightPaddle.position : Vector3.zero;

            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            // Find water height from Water object if possible
            var waterSystem = FindObjectOfType<WaterSystem.Water>();
            if (waterSystem != null)
            {
                waterHeight = waterSystem.transform.position.y;
                Debug.Log($"Found water system at height: {waterHeight}");
            }
            else
            {
                Debug.LogWarning("No water system found - using default water height: " + waterHeight);
            }

            // Make sure water layer exists
            if (LayerMask.NameToLayer("Water") == -1)
            {
                Debug.LogError("Water layer doesn't exist! Please add 'Water' layer in Project Settings.");
            }
        }


        private void FixedUpdate()
        {
            HandleBuoyancy();
            CheckPaddlesInWater();
            HandlePaddling();

            turnDirection = 0f;

            if (leftPaddle) previousLeftPaddlePos = leftPaddle.position;
            if (rightPaddle) previousRightPaddlePos = rightPaddle.position;
        }

        public void HandleBuoyancy()
        {
            float submergedAmount = 0f;

            if (floatPoints.Length == 0)
                return;

            // Calculate point velocities
            for (int i = 0; i < floatPoints.Length; i++)
            {
                if (floatPoints[i] != null)
                    _velocities[i] = rb.GetPointVelocity(floatPoints[i].position);
            }

            // Apply buoyancy forces at each point
            for (int i = 0; i < floatPoints.Length; i++)
            {
                Transform point = floatPoints[i];
                if (point == null) continue;

                float pointHeight = waterHeight + waterLevelOffset;

                // Skip if point is above water
                if (point.position.y - 0.1f >= pointHeight) continue;

                // Calculate submersion factor (0-1)
                float k = Mathf.Clamp01((pointHeight - point.position.y) / 0.2f);
                submergedAmount += k / floatPoints.Length;

                // Apply damping force (resists velocity)
                Vector3 dampingForce = dampingFactor * rb.mass * -_velocities[i];

                float forceMagnitude = waterDensity * Mathf.Abs(Physics.gravity.y) * k * buoyancyForce * 1.5f; // Increased multiplier
                Vector3 force = dampingForce + Vector3.up * forceMagnitude;


                rb.AddForceAtPosition(force, point.position, ForceMode.Force);
            }

            // Update drag based on submersion
            UpdateDrag(submergedAmount);
        }

        private void UpdateDrag(float submergedAmount)
        {
            _percentSubmerged = Mathf.Lerp(_percentSubmerged, submergedAmount, 0.25f);

            if (_percentSubmerged > 0)
            {
                rb.linearDamping = _baseDrag + waterDrag * _percentSubmerged * 10f;
                rb.angularDamping = _baseAngularDrag + waterAngularDrag * _percentSubmerged;
            }
            else
            {
                rb.linearDamping = _baseDrag;
                rb.angularDamping = _baseAngularDrag;
            }
        }

        private void CheckPaddlesInWater()
        {
            if (leftPaddle)
                isLeftPaddleInWater = leftPaddle.position.y < waterHeight + 0.1f;

            if (rightPaddle)
                isRightPaddleInWater = rightPaddle.position.y < waterHeight + 0.1f;
        }

        private void HandlePaddling()
        {
            if (!leftPaddle || !rightPaddle) return;

            Vector3 leftPaddleDelta = leftPaddle.position - previousLeftPaddlePos;
            Vector3 rightPaddleDelta = rightPaddle.position - previousRightPaddlePos;

            // Calculate paddle forces
            float leftForce = 0f;
            float rightForce = 0f;

            if (isLeftPaddleInWater)
            {
                leftForce = CalculatePaddleForce(leftPaddle, leftPaddleDelta);
            }

            if (isRightPaddleInWater)
            {
                rightForce = CalculatePaddleForce(rightPaddle, rightPaddleDelta);
            }

            // Apply forward movement and calculate total forward force
            float totalForwardForce = ApplyPaddlingForces(leftForce, rightForce);
            forwardSpeed = rb.linearVelocity.magnitude;

            // Apply steering based on paddle differences
            ApplySteering(leftForce, rightForce, totalForwardForce);

            // Limit max speed
            if (rb.linearVelocity.magnitude > maxSpeed)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
            }
        }

        private float CalculatePaddleForce(Transform paddle, Vector3 movementDelta)
        {
            // Calculate backward movement of paddle (which pushes boat forward)
            float backwardForce = Vector3.Dot(movementDelta, -transform.forward) * paddleMultiplier;

            // Only return positive forces (backward paddle motion)
            return Mathf.Max(0, backwardForce);
        }

        private float ApplyPaddlingForces(float leftForce, float rightForce)
        {
            float totalForce = leftForce + rightForce;

            if (totalForce > 0.001f)
            {
                // Apply force in the forward direction
                Vector3 forwardForce = transform.forward * paddleForce * totalForce;

                // Reduce forward force when turning sharply
                if (Mathf.Abs(turnDirection) > 0.1f)
                {
                    forwardForce *= turnDragFactor;
                }

                rb.AddForce(forwardForce, ForceMode.Force);
            }

            return totalForce;
        }

        private void ApplySteering(float leftForce, float rightForce, float totalForwardForce)
        {
            // Calculate steering based on the difference between left and right paddle forces
            float forceDifference = rightForce - leftForce;

            // Apply asymmetric steering when paddling only on one side
            if (leftForce > 0 && rightForce <= 0)
            {
                // Only left paddle - turn right
                turnDirection = -asymmetricSteeringFactor;
            }
            else if (rightForce > 0 && leftForce <= 0)
            {
                // Only right paddle - turn left
                turnDirection = asymmetricSteeringFactor;
            }
            else if (leftForce > 0 && rightForce > 0)
            {
                // Both paddles - differential steering
                turnDirection = forceDifference * steeringFactor;
            }

            // Scale turning effect based on forward speed for more realistic behavior
            float speedFactor = Mathf.Clamp01(forwardSpeed / 2.0f);
            float targetRotation = turnDirection * rotationSpeed * speedFactor;

            // Smooth the rotation for more natural movement
            float smoothedRotation = Mathf.SmoothDamp(0, targetRotation, ref currentTurnVelocity, steeringResponseTime);

            // Apply the rotation
            transform.Rotate(0, smoothedRotation * Time.fixedDeltaTime, 0);
        }
    }
}
