using UnityEngine;

public class SailingBehaviour : MonoBehaviour
{
    [Header("Ship Movement")]
    [SerializeField] private float maxForwardSpeed = 10f;
    [SerializeField] private float acceleration = 1f;
    [SerializeField] private float deceleration = 0.5f;

    [Header("Rudder Settings")]
    [SerializeField] private Transform rudder;
    [SerializeField] private float maxRudderEffect = 20f; // Maximum turning force
    [SerializeField] private float rudderAcceleration = 2f; // How quickly rudder effect builds up
    [SerializeField] private float rudderDeceleration = 1f; // How quickly rudder effect fades
    [SerializeField] private float speedReductionWhileTurning = 0.7f; // Slows ship when turning

    [Header("Ship Pivot")]
    [SerializeField] private Transform shipPivot; // Reference to the ship pivot object

    [Header("References")]
    [SerializeField] private Rigidbody shipRigidbody;

    [Header("Model Rotation")]
    [SerializeField] private float modelRotationSpeed = 5f; // How quickly the ship model rotates to match pivot

    // Runtime variables
    private float currentSpeed = 0f;
    private float targetSpeed;
    private float currentRudderEffect = 0f;
    private float rudderAngle = 0f;

    // Normalization factor for rudder angle (converts from -360/360 to -1/1)
    private const float RUDDER_ANGLE_NORMALIZATION = 360f;

    void Start()
    {
        if (shipRigidbody == null)
        {
            shipRigidbody = GetComponent<Rigidbody>();
        }

        // Ensure rigidbody stays upright
        if (shipRigidbody != null)
        {
            shipRigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }
    }

    void Update()
    {
        // Read rudder input - replace this with your actual rudder control method
        // For example, if you're getting rudder angle from another component
        if (rudder != null)
        {
            rudderAngle = rudder.localRotation.eulerAngles.x;
            // Normalize to -180 to 180 range
            if (rudderAngle > 180f) rudderAngle -= 360f;
        }
    }

    void FixedUpdate()
    {
        // Set target speed based on whatever control scheme you're using
        // For testing, we'll assume full speed forward
        targetSpeed = maxForwardSpeed;

        // Apply acceleration/deceleration
        ApplySpeed();

        // Apply rudder effect to ship pivot
        ApplyRudderToShipPivot();

        // Apply forward movement
        ApplyMovement();

        // Align the ship model to the pivot's rotation
        AlignShipModel();
    }

    void ApplySpeed()
    {
        // Calculate normalized rudder angle (-1 to 1)
        float normalizedRudderAngle = Mathf.Clamp(rudderAngle / RUDDER_ANGLE_NORMALIZATION, -1f, 1f);

        // Reduce speed when turning based on rudder angle
        float turnSpeedFactor = 1f - (Mathf.Abs(normalizedRudderAngle) * speedReductionWhileTurning);
        float adjustedTargetSpeed = targetSpeed * turnSpeedFactor;

        // Apply acceleration or deceleration
        if (currentSpeed < adjustedTargetSpeed)
        {
            currentSpeed += acceleration * Time.fixedDeltaTime;
            if (currentSpeed > adjustedTargetSpeed)
                currentSpeed = adjustedTargetSpeed;
        }
        else if (currentSpeed > adjustedTargetSpeed)
        {
            currentSpeed -= deceleration * Time.fixedDeltaTime;
            if (currentSpeed < adjustedTargetSpeed)
                currentSpeed = adjustedTargetSpeed;
        }
    }

    void ApplyRudderToShipPivot()
    {
        if (shipPivot == null)
            return;

        // Calculate normalized rudder angle (-1 to 1)
        float normalizedRudderAngle = Mathf.Clamp(rudderAngle / RUDDER_ANGLE_NORMALIZATION, -1f, 1f);

        // Calculate target rudder effect
        float targetRudderEffect = normalizedRudderAngle * maxRudderEffect;

        // Gradually adjust current rudder effect (simulates slow response of large ship)
        if (Mathf.Abs(currentRudderEffect) < Mathf.Abs(targetRudderEffect))
        {
            // Rudder effect builds up slowly
            float delta = rudderAcceleration * Time.fixedDeltaTime;
            currentRudderEffect = Mathf.MoveTowards(currentRudderEffect, targetRudderEffect, delta);
        }
        else if (Mathf.Abs(currentRudderEffect) > Mathf.Abs(targetRudderEffect))
        {
            // Rudder effect decreases slowly
            float delta = rudderDeceleration * Time.fixedDeltaTime;
            currentRudderEffect = Mathf.MoveTowards(currentRudderEffect, targetRudderEffect, delta);
        }

        // Scale turning by current speed - more speed = more turning effect
        float speedFactor = currentSpeed / maxForwardSpeed;
        float turnAmount = currentRudderEffect * speedFactor * Time.fixedDeltaTime;

        // Apply rotation to the ship pivot instead of the ship itself
        shipPivot.Rotate(0f, turnAmount, 0f);
    }

    void ApplyMovement()
    {
        if (shipPivot == null)
            return;

        // Get the forward direction from the pivot object
        Vector3 forwardDirection = -shipPivot.right;

        if (shipRigidbody != null)
        {
            // Apply velocity in the pivot's forward direction
            shipRigidbody.linearVelocity = forwardDirection * currentSpeed;
        }
        else
        {
            // Fallback if no rigidbody, move in the pivot's forward direction
            transform.position += forwardDirection * currentSpeed * Time.fixedDeltaTime;
        }
    }

    // Aligns the ship model with the pivot's rotation smoothly over time.
    void AlignShipModel()
    {
        if (shipPivot == null)
            return;

        Quaternion targetRotation = Quaternion.Euler(0f, shipPivot.rotation.eulerAngles.y, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, modelRotationSpeed * Time.fixedDeltaTime);
    }

    // Helper method to set ship throttle (0 to 1)
    public void SetThrottle(float throttle)
    {
        targetSpeed = Mathf.Clamp01(throttle) * maxForwardSpeed;
    }

    // Helper method to manually set rudder angle if needed
    public void SetRudderAngle(float angle)
    {
        rudderAngle = Mathf.Clamp(angle, -360f, 360f);
    }
}
