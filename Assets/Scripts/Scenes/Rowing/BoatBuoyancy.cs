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

            previousLeftPaddlePos = leftPaddle.position;
            previousRightPaddlePos = rightPaddle.position;

            rb.linearDamping = 0.1f;
            rb.angularDamping = 2.0f;

            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        private void FixedUpdate()
        {
            HandleBuoyancy();
            CheckPaddlesInWater();
            HandlePaddling();

            turnDirection = 0f;

            previousLeftPaddlePos = leftPaddle.position;
            previousRightPaddlePos = rightPaddle.position;
        }

        public void HandleBuoyancy()
        {
            int submergedPoints = 0;

            foreach (Transform point in floatPoints)
            {
                if (point.position.y < waterHeight)
                {
                    float submersion = Mathf.Clamp01((waterHeight - point.position.y) / 0.2f);
                    Vector3 force = Vector3.up * submersion * buoyancyForce;
                    rb.AddForceAtPosition(force, point.position, ForceMode.Force);
                    submergedPoints++;
                }
            }

            if (submergedPoints > 0)
            {
                rb.linearDamping = waterDrag;
                rb.angularDamping = waterAngularDrag;
            }
            else
            {
                rb.linearDamping = 0.1f;
                rb.angularDamping = 2.0f;
            }
        }

        private void CheckPaddlesInWater()
        {
            isLeftPaddleInWater = leftPaddle.position.y < waterHeight + 0.1f;
            isRightPaddleInWater = rightPaddle.position.y < waterHeight + 0.1f;
        }

        private void HandlePaddling()
        {
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
