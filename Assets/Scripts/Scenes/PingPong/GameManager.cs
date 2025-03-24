using UnityEngine;
using TMPro;
using System.Collections;
using Oculus.Interaction.Samples;

namespace PingPong
{
    public class GameManager : MonoBehaviour
    {
        [Header("Game Settings")]
        [SerializeField] private int pointsToWin = 11;
        [SerializeField] private float respawnDelay = 1.5f;
        [SerializeField] private Vector3 ballSpawnPosition;
        [SerializeField] private float initialForce = 3f;
        
        [Header("References")]
        [SerializeField] private GameObject ballPrefab;
        [SerializeField] private Transform playerSide;
        [SerializeField] private Transform enemySide;
        [SerializeField] private EnemyAI enemyAI;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI gameStateText;
        [SerializeField] private GameObject paddle;
        
        [Header("Audio")]
        [SerializeField] private AudioClip pointSound;
        [SerializeField] private AudioClip hitSound;
        [SerializeField] private AudioClip gameOverSound;
        
        private int playerScore;
        private int enemyScore;
        private GameObject currentBall;
        private bool gameInProgress = false;
        private Vector3 lastBallPosition;
        private float floorY;
        private int consecutiveHitsPlayer;
        private int consecutiveHitsEnemy;

        private void Start()
        {
            floorY = transform.position.y - 1f;
            InitGame();
        }

        private void Update()
        {
            if (!gameInProgress) return;
            
            if (currentBall == null)
            {
                SpawnBall();
                return;
            }
            
            CheckBallPosition();
        }

        private void CheckBallPosition()
        {
            if (currentBall == null) return;
            
            Vector3 ballPos = currentBall.transform.position;
            
            if (ballPos.y < floorY)
            {
                StartCoroutine(RespawnBall());
                return;
            }
            
            if (HasBallMovedSides(lastBallPosition, ballPos))
            {
                HandlePotentialPoint(ballPos);
            }
            
            lastBallPosition = ballPos;
        }

        private bool HasBallMovedSides(Vector3 prevPos, Vector3 currentPos)
        {
            float midZ = (playerSide.position.z + enemySide.position.z) / 2f;
            return (prevPos.z < midZ && currentPos.z > midZ) || 
                   (prevPos.z > midZ && currentPos.z < midZ);
        }

        private void HandlePotentialPoint(Vector3 ballPos)
        {
            bool ballOnPlayerSide = IsBallOnPlayerSide(ballPos);
            
            if (ballOnPlayerSide)
            {
                consecutiveHitsPlayer++;
                consecutiveHitsEnemy = 0;
                
                if (consecutiveHitsPlayer > 1)
                {
                    AwardPoint(false);
                }
            }
            else
            {
                consecutiveHitsEnemy++;
                consecutiveHitsPlayer = 0;
                
                if (consecutiveHitsEnemy > 1)
                {
                    AwardPoint(true);
                }
            }
        }

        private bool IsBallOnPlayerSide(Vector3 position)
        {
            float midZ = (playerSide.position.z + enemySide.position.z) / 2f;
            return position.z < midZ;
        }

        private void AwardPoint(bool toPlayer)
        {
            if (toPlayer)
                playerScore++;
            else
                enemyScore++;
                
            UpdateScoreText();
            PlaySound(pointSound);
            
            if (playerScore >= pointsToWin || enemyScore >= pointsToWin)
            {
                EndGame();
            }
            else
            {
                StartCoroutine(RespawnBall());
            }
        }
        
        private void UpdateScoreText()
        {
            if (scoreText != null)
                scoreText.text = $"{playerScore} - {enemyScore}";
        }
        
        private void PlaySound(AudioClip clip)
        {
            if (clip != null)
                AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position);
        }

        private void InitGame()
        {
            playerScore = 0;
            enemyScore = 0;
            consecutiveHitsPlayer = 0;
            consecutiveHitsEnemy = 0;
            UpdateScoreText();
            
            if (gameStateText != null)
                gameStateText.text = "Game Started";
                
            gameInProgress = true;
            SpawnBall();
        }

        private void SpawnBall()
        {
            if (currentBall != null)
                Destroy(currentBall);
                
            currentBall = Instantiate(ballPrefab, ballSpawnPosition, Quaternion.identity);
            lastBallPosition = currentBall.transform.position;
            
            Rigidbody rb = currentBall.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 initialDirection = Vector3.forward * (Random.value > 0.5f ? 1 : -1);
                initialDirection.x = Random.Range(-0.3f, 0.3f);
                rb.AddForce(initialDirection.normalized * initialForce, ForceMode.Impulse);
            }
        }

        private IEnumerator RespawnBall()
        {
            if (currentBall != null)
            {
                RespawnOnDrop respawner = currentBall.GetComponent<RespawnOnDrop>();
                if (respawner != null)
                {
                    respawner.Respawn();
                }
                else
                {
                    Destroy(currentBall);
                    currentBall = null;
                }
            }
            
            consecutiveHitsPlayer = 0;
            consecutiveHitsEnemy = 0;
            
            yield return new WaitForSeconds(respawnDelay);
            
            if (currentBall == null)
                SpawnBall();
        }
        
        private void EndGame()
        {
            gameInProgress = false;
            
            if (gameStateText != null)
            {
                string winner = playerScore > enemyScore ? "You Win!" : "AI Wins!";
                gameStateText.text = winner;
            }
            
            PlaySound(gameOverSound);
            
            if (enemyAI != null)
                enemyAI.ResetAI();
        }
        
        public void RestartGame()
        {
            StopAllCoroutines();
            
            if (currentBall != null)
            {
                Destroy(currentBall);
                currentBall = null;
            }
            
            if (paddle != null)
            {
                RespawnOnDrop paddleRespawner = paddle.GetComponent<RespawnOnDrop>();
                if (paddleRespawner != null)
                    paddleRespawner.Respawn();
            }
            
            if (enemyAI != null)
                enemyAI.ResetAI();
                
            InitGame();
        }
    }
}
