using UnityEngine;

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
    public float steeringFactor = 0.4f;
    public float maxSpeed = 50f;
    public float paddleMultiplier = 25f;
    public float rotationSpeed = 30f;
    
    [Header("Stabilization")]
    public float waterDrag = 0.8f;
    public float waterAngularDrag = 5.0f;
    
    private Vector3 previousLeftPaddlePos;
    private Vector3 previousRightPaddlePos;
    private bool isLeftPaddleInWater = false;
    private bool isRightPaddleInWater = false;
    private float turnDirection = 0f;
    private float currentTurnVelocity = 0f;
    
    private void Start()
    {
        if (rb == null) {
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
    
    private void HandleBuoyancy()
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
        
        float leftForce = 0f;
        float rightForce = 0f;
        
        if (isLeftPaddleInWater)
        {
            leftForce = ApplyPaddleMovement(leftPaddle, leftPaddleDelta);
        }
        
        if (isRightPaddleInWater)
        {
            rightForce = ApplyPaddleMovement(rightPaddle, rightPaddleDelta);
        }
        
        turnDirection = rightForce - leftForce;
        float targetRotation = turnDirection * rotationSpeed;
        
        float smoothedRotation = Mathf.SmoothDamp(0, targetRotation, ref currentTurnVelocity, 0.1f);
        transform.Rotate(0, smoothedRotation * Time.fixedDeltaTime, 0);
        
        if (rb.linearVelocity.magnitude > maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }
    }
    
    private float ApplyPaddleMovement(Transform paddle, Vector3 movementDelta)
    {
        float backwardForce = Vector3.Dot(movementDelta, -transform.forward) * paddleMultiplier;
        
        if (backwardForce > 0.001f)
        {
            Vector3 forwardForce = transform.forward * paddleForce * backwardForce;
            rb.AddForce(forwardForce, ForceMode.Force);
            return backwardForce;
        }
        
        return 0f;
    }
}