using System.Collections.Generic;
using UnityEngine;
using WaterSystem;

namespace Rowing
{
	[RequireComponent(typeof(Rigidbody))]
	public class BoatController : MonoBehaviour
	{
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

		[Header("Terrain Collision Settings")]
		[Tooltip("Force applied to push boat away from terrain")]
		public float terrainBounceForce = 20f;
		[Tooltip("Layer mask for terrain collision detection")]
		public LayerMask terrainLayer;
		[Tooltip("How much velocity is preserved after terrain collision (0-1)")]
		public float terrainBounceFactor = 0.3f;
		[Tooltip("Distance to check for terrain")]
		public float checkDistance = 0.5f;
		[Tooltip("Minimum impact velocity to trigger collision effects")]
		public float minImpactVelocity = 2.0f;
		[Tooltip("Points to check for terrain collisions")]
		public Transform[] collisionPoints;

		// References
		private Rigidbody _rb;
		private BuoyantObject _buoyantObject;
		private float _waterHeight = 0f;

		// Paddling state
		private Vector3 _previousLeftPaddlePos;
		private Vector3 _previousRightPaddlePos;
		private bool _isLeftPaddleInWater = false;
		private bool _isRightPaddleInWater = false;
		private float _turnDirection = 0f;
		private float _currentTurnVelocity = 0f;
		private float _forwardSpeed = 0f;

		private void Start()
		{
			_rb = GetComponent<Rigidbody>();
			_buoyantObject = GetComponent<BuoyantObject>();

			if (_rb == null)
			{
				Debug.LogError("Missing Rigidbody component on boat!");
				enabled = false;
				return;
			}

			if (_buoyantObject == null)
			{
				Debug.LogError("Missing BuoyantObject component on boat!");
				enabled = false;
				return;
			}

			// Configure rigidbody for paddling and collisions
			_rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
			_rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

			// Initialize paddle positions
			_previousLeftPaddlePos = leftPaddle ? leftPaddle.position : Vector3.zero;
			_previousRightPaddlePos = rightPaddle ? rightPaddle.position : Vector3.zero;

			// Initialize collision detection points if not set
			if (collisionPoints == null || collisionPoints.Length == 0)
			{
				// Create default collision points at corners of the boat
				CreateDefaultCollisionPoints();
			}

			// Initialize terrain layer if not set
			if (terrainLayer.value == 0)
			{
				terrainLayer = LayerMask.GetMask("Terrain");
				Debug.Log("Terrain layer automatically set to 'Terrain'");
			}

			// Get water height from the water system
			var waterSystem = FindObjectOfType<WaterSystem.Water>();
			if (waterSystem != null)
			{
				_waterHeight = waterSystem.transform.position.y;
			}
			else
			{
				// If no water system is found, try to estimate water height from buoyant object
				if (_buoyantObject.Heights != null && _buoyantObject.Heights.Length > 0)
				{
					_waterHeight = _buoyantObject.Heights[0].y;
				}
				Debug.LogWarning("No water system found - water height estimation may be inaccurate");
			}
		}

		private void CreateDefaultCollisionPoints()
		{
			Debug.LogWarning("No collision points defined - adding default points at corners");

			// Get collider bounds
			Bounds bounds = new Bounds();
			foreach (Collider collider in GetComponentsInChildren<Collider>())
			{
				bounds.Encapsulate(collider.bounds);
			}

			// Create points at corners and center
			GameObject pointsContainer = new GameObject("CollisionPoints");
			pointsContainer.transform.parent = transform;
			pointsContainer.transform.localPosition = Vector3.zero;

			List<Transform> points = new List<Transform>();

			// Front points
			GameObject frontLeft = CreateCollisionPoint("FrontLeft", pointsContainer.transform);
			frontLeft.transform.localPosition = new Vector3(-bounds.extents.x, -bounds.extents.y, bounds.extents.z);
			points.Add(frontLeft.transform);

			GameObject frontRight = CreateCollisionPoint("FrontRight", pointsContainer.transform);
			frontRight.transform.localPosition = new Vector3(bounds.extents.x, -bounds.extents.y, bounds.extents.z);
			points.Add(frontRight.transform);

			// Back points
			GameObject backLeft = CreateCollisionPoint("BackLeft", pointsContainer.transform);
			backLeft.transform.localPosition = new Vector3(-bounds.extents.x, -bounds.extents.y, -bounds.extents.z);
			points.Add(backLeft.transform);

			GameObject backRight = CreateCollisionPoint("BackRight", pointsContainer.transform);
			backRight.transform.localPosition = new Vector3(bounds.extents.x, -bounds.extents.y, -bounds.extents.z);
			points.Add(backRight.transform);

			// Center point
			GameObject center = CreateCollisionPoint("Center", pointsContainer.transform);
			center.transform.localPosition = new Vector3(0, -bounds.extents.y, 0);
			points.Add(center.transform);

			collisionPoints = points.ToArray();
		}

		private GameObject CreateCollisionPoint(string name, Transform parent)
		{
			GameObject point = new GameObject(name);
			point.transform.parent = parent;
			return point;
		}

		private void Update()
		{
			// Update water height from BuoyantObject if possible
			if (_buoyantObject.Heights != null && _buoyantObject.Heights.Length > 0)
			{
				_waterHeight = _buoyantObject.Heights[0].y + _buoyantObject.waterLevelOffset;
			}

			// Check if paddles are in water
			CheckPaddlesInWater();
		}

		private void FixedUpdate()
		{
			HandleTerrainCollision();
			HandlePaddling();

			// Reset turn direction for next frame
			_turnDirection = 0f;

			// Update previous paddle positions for next frame
			if (leftPaddle) _previousLeftPaddlePos = leftPaddle.position;
			if (rightPaddle) _previousRightPaddlePos = rightPaddle.position;
		}

		private void CheckPaddlesInWater()
		{
			if (leftPaddle)
				_isLeftPaddleInWater = leftPaddle.position.y < _waterHeight + 0.1f;

			if (rightPaddle)
				_isRightPaddleInWater = rightPaddle.position.y < _waterHeight + 0.1f;
		}

		private void HandlePaddling()
		{
			if (!leftPaddle || !rightPaddle) return;

			Vector3 leftPaddleDelta = leftPaddle.position - _previousLeftPaddlePos;
			Vector3 rightPaddleDelta = rightPaddle.position - _previousRightPaddlePos;

			// Calculate paddle forces
			float leftForce = 0f;
			float rightForce = 0f;

			if (_isLeftPaddleInWater)
			{
				leftForce = CalculatePaddleForce(leftPaddle, leftPaddleDelta);
			}

			if (_isRightPaddleInWater)
			{
				rightForce = CalculatePaddleForce(rightPaddle, rightPaddleDelta);
			}

			// Apply forward movement and calculate total forward force
			float totalForwardForce = ApplyPaddlingForces(leftForce, rightForce);
			_forwardSpeed = _rb.linearVelocity.magnitude;

			// Apply steering based on paddle differences
			ApplySteering(leftForce, rightForce, totalForwardForce);

			// Limit max speed
			if (_rb.linearVelocity.magnitude > maxSpeed)
			{
				_rb.linearVelocity = _rb.linearVelocity.normalized * maxSpeed;
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
				if (Mathf.Abs(_turnDirection) > 0.1f)
				{
					forwardForce *= turnDragFactor;
				}

				_rb.AddForce(forwardForce, ForceMode.Force);
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
				_turnDirection = -asymmetricSteeringFactor;
			}
			else if (rightForce > 0 && leftForce <= 0)
			{
				// Only right paddle - turn left
				_turnDirection = asymmetricSteeringFactor;
			}
			else if (leftForce > 0 && rightForce > 0)
			{
				// Both paddles - differential steering
				_turnDirection = forceDifference * steeringFactor;
			}

			// Scale turning effect based on forward speed for more realistic behavior
			float speedFactor = Mathf.Clamp01(_forwardSpeed / 2.0f);
			float targetRotation = _turnDirection * rotationSpeed * speedFactor;

			// Smooth the rotation for more natural movement
			float smoothedRotation = Mathf.SmoothDamp(0, targetRotation, ref _currentTurnVelocity, steeringResponseTime);

			// Apply the rotation
			transform.Rotate(0, smoothedRotation * Time.fixedDeltaTime, 0);
		}

		private void HandleTerrainCollision()
		{
			if (collisionPoints == null || collisionPoints.Length == 0)
				return;

			// Cast rays from each collision point to detect terrain
			foreach (Transform point in collisionPoints)
			{
				if (point == null) continue;

				// Cast ray downward to detect terrain below
				if (Physics.Raycast(point.position, Vector3.down, out RaycastHit hitDown, checkDistance, terrainLayer))
				{
					ApplyTerrainCollisionResponse(hitDown, point);
				}

				// Cast ray in the movement direction to detect terrain ahead
				Vector3 forwardDir = _rb.linearVelocity.normalized;
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
			Vector3 reflectionDir = Vector3.Reflect(_rb.linearVelocity, hit.normal);

			// Apply bounce force away from terrain
			Vector3 bounceForce = hit.normal * terrainBounceForce;
			_rb.AddForceAtPosition(bounceForce, collisionPoint.position, ForceMode.Impulse);

			// Reduce velocity based on bounce factor
			_rb.linearVelocity *= terrainBounceFactor;

			// Play collision sound if impact is significant
			if (_rb.linearVelocity.magnitude > minImpactVelocity)
			{
				// If you have an audio component, you could play a collision sound here
				Debug.Log("Terrain collision detected at velocity: " + _rb.linearVelocity.magnitude);
			}
		}

		private void OnDrawGizmos()
		{
			// Draw water level
			Gizmos.color = new Color(0, 0.5f, 1f, 0.3f);
			Vector3 center = transform.position;
			center.y = _waterHeight;
			Gizmos.DrawCube(center, new Vector3(3f, 0.01f, 3f));

			// Draw collision detection points
			if (collisionPoints == null) return;

			Gizmos.color = Color.red;
			foreach (Transform point in collisionPoints)
			{
				if (point != null)
				{
					// Draw collision point
					Gizmos.DrawSphere(point.position, 0.1f);

					// Draw terrain check ray
					Gizmos.DrawLine(point.position, point.position + Vector3.down * checkDistance);

					// Draw forward check ray if playing
					if (Application.isPlaying && _rb != null && _rb.linearVelocity.magnitude > 0.01f)
					{
						Vector3 forwardDir = _rb.linearVelocity.normalized;
						Gizmos.DrawLine(point.position, point.position + forwardDir * checkDistance);
					}
				}
			}

			// Draw paddle state indicators
			if (Application.isPlaying)
			{
				if (leftPaddle != null)
				{
					Gizmos.color = _isLeftPaddleInWater ? Color.blue : Color.cyan;
					Gizmos.DrawSphere(leftPaddle.position, 0.05f);
				}

				if (rightPaddle != null)
				{
					Gizmos.color = _isRightPaddleInWater ? Color.blue : Color.cyan;
					Gizmos.DrawSphere(rightPaddle.position, 0.05f);
				}
			}
		}
	}
}
