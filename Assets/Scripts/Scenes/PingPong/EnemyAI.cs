using UnityEngine;

namespace PingPong
{
    public class EnemyAI : MonoBehaviour
    {
        [Header("AI Settings")]
        [SerializeField] private Transform paddleTransform;
        [SerializeField] private float moveSpeed = 10f;
        [SerializeField] private float reactionTime = 0.2f;
        [SerializeField] private float maxPredictionError = 0.5f;

        [Header("AI Behavior")]
        [SerializeField] private float paddleHeight = 1f;
        [SerializeField] private float horizontalRange = 2f;
        [SerializeField] private Vector3 restingPosition;
        [SerializeField] private bool debugVisuals = false;

        [Header("References")]
        [SerializeField] private Transform targetBall;
        [SerializeField] private Transform aiSide;

        private Vector3 targetPosition;
        private Vector3 initialPosition;
        private float lastDecisionTime;
        private bool ballIncoming = false;
        private Vector3 lastBallPosition;
        private Vector3 ballVelocity;

        private void Start()
        {
            initialPosition = paddleTransform.position;
            restingPosition = initialPosition;
            targetPosition = initialPosition;
            lastBallPosition = targetBall ? targetBall.position : Vector3.zero;
        }

        private void Update()
        {
            if (targetBall == null)
            {
                GameObject ball = GameObject.FindWithTag("Ball");
                if (ball) targetBall = ball.transform;
                return;
            }

            UpdateBallVelocity();
            DecideMovement();
            MovePaddle();
        }

        private void UpdateBallVelocity()
        {
            ballVelocity = (targetBall.position - lastBallPosition) / Time.deltaTime;
            lastBallPosition = targetBall.position;
        }

        private void DecideMovement()
        {
            bool isBallMovingTowardsAI = IsBallMovingTowardsAI();

            if (Time.time - lastDecisionTime > reactionTime)
            {
                lastDecisionTime = Time.time;

                if (isBallMovingTowardsAI && IsBallInRange())
                {
                    ballIncoming = true;
                    Vector3 predictedBallPosition = PredictBallPosition();

                    targetPosition = new Vector3(
                        Mathf.Clamp(predictedBallPosition.x, -horizontalRange, horizontalRange),
                        paddleTransform.position.y,
                        aiSide.position.z
                    );

                    AddRandomError();
                }
                else if (!isBallMovingTowardsAI || !IsBallInRange())
                {
                    ballIncoming = false;
                    targetPosition = restingPosition;
                }
            }
        }

        private void MovePaddle()
        {
            paddleTransform.position = Vector3.MoveTowards(
                paddleTransform.position,
                targetPosition,
                moveSpeed * Time.deltaTime
            );
        }

        private bool IsBallMovingTowardsAI()
        {
            if (targetBall == null) return false;

            Vector3 directionToAI = (aiSide.position - targetBall.position).normalized;
            return Vector3.Dot(ballVelocity.normalized, directionToAI) > 0.3f;
        }

        private bool IsBallInRange()
        {
            if (targetBall == null) return false;

            return Mathf.Abs(targetBall.position.x) < horizontalRange * 1.5f;
        }

        private Vector3 PredictBallPosition()
        {
            if (targetBall == null) return restingPosition;

            float distanceZ = Mathf.Abs(targetBall.position.z - aiSide.position.z);
            float timeToReach = distanceZ / Mathf.Abs(ballVelocity.z);

            Vector3 futurePosition = targetBall.position + ballVelocity * timeToReach;
            return futurePosition;
        }

        private void AddRandomError()
        {
            float errorAmount = Random.Range(-maxPredictionError, maxPredictionError);
            targetPosition.x += errorAmount;
        }

        public void ResetAI()
        {
            paddleTransform.position = initialPosition;
            targetPosition = initialPosition;
            ballIncoming = false;
        }

        private void OnDrawGizmos()
        {
            if (!debugVisuals) return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(targetPosition, 0.2f);

            if (targetBall != null)
            {
                Gizmos.color = ballIncoming ? Color.green : Color.red;
                Gizmos.DrawLine(paddleTransform.position, targetBall.position);
            }
        }
    }
}
