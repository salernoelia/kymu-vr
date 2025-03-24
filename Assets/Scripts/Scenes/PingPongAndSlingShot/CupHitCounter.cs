using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace PingPong
{
    public class CupHitCounter : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI counterText;

        [Header("Settings")]
        [SerializeField] private string ballTag = "SlingShotAndPingPongBall";
        [SerializeField] private string cupTag = "RespawnableCup";
        [SerializeField] private float minImpactForce = 1.5f;
        [SerializeField] private float minCupToCupForce = 0.8f;
        [SerializeField] private float floorY = 0.05f;
        [SerializeField] private float cooldownTime = 1.0f;
        [SerializeField] private float angleTolerance = 30f;

        [Header("Display")]
        [SerializeField] private bool singleCounterOnly = true;
        [SerializeField] private string singleCounterFormat = "Cups Hit: {0}";
        [SerializeField] private string detailedFormat = "Hits: {0}\nFloor: {1}\nRespawns: {2}";

        private int successfulHits;
        private Dictionary<int, CupState> cupStates = new Dictionary<int, CupState>();

        private void Awake()
        {
            ResetCounters();
        }

        private void Start()
        {
            UpdateCounterDisplay();

            // Find all existing cups and register them
            RegisterExistingCups();

            // Subscribe to listen for new balls and cups
            StartCoroutine(MonitorForNewObjects());
        }

        private void RegisterExistingCups()
        {
            GameObject[] cups = GameObject.FindGameObjectsWithTag(cupTag);
            foreach (GameObject cup in cups)
            {
                RegisterCup(cup);
            }
        }

        private IEnumerator MonitorForNewObjects()
        {
            while (true)
            {
                // Check for new balls
                GameObject[] balls = GameObject.FindGameObjectsWithTag(ballTag);
                foreach (GameObject ball in balls)
                {
                    if (!ball.GetComponent<BallCollisionTracker>())
                    {
                        var tracker = ball.AddComponent<BallCollisionTracker>();
                        tracker.Initialize(this, cupTag, minImpactForce);
                    }
                }

                // Check for new cups
                GameObject[] cups = GameObject.FindGameObjectsWithTag(cupTag);
                foreach (GameObject cup in cups)
                {
                    RegisterCup(cup);
                }

                yield return new WaitForSeconds(2.0f);
            }
        }

        private void RegisterCup(GameObject cup)
        {
            int cupID = cup.GetInstanceID();

            if (!cupStates.ContainsKey(cupID))
            {
                // Add to tracking dictionary
                cupStates[cupID] = new CupState
                {
                    originalPosition = cup.transform.position,
                    originalRotation = cup.transform.rotation,
                    lastStableTime = Time.time
                };

                // Add tracker component
                CupTracker tracker = cup.GetComponent<CupTracker>();
                if (!tracker)
                {
                    tracker = cup.AddComponent<CupTracker>();
                    tracker.Initialize(this, floorY);
                }

                // Add cup-to-cup collision tracker
                CupToCupCollisionTracker cupCollisionTracker = cup.GetComponent<CupToCupCollisionTracker>();
                if (!cupCollisionTracker)
                {
                    cupCollisionTracker = cup.AddComponent<CupToCupCollisionTracker>();
                    cupCollisionTracker.Initialize(this, cupTag, minCupToCupForce);
                }
            }
        }

        public void RecordSuccessfulHit(GameObject cup, bool fromBall = true)
        {
            int cupID = cup.GetInstanceID();

            if (cupStates.TryGetValue(cupID, out CupState state))
            {
                if (!state.hasBeenHit && !state.isOnCooldown)
                {
                    successfulHits++;
                    state.hasBeenHit = true;
                    cupStates[cupID] = state;
                    UpdateCounterDisplay();

                    // Start cooldown
                    StartCoroutine(ResetCupCooldown(cupID));
                }
            }
        }

        private IEnumerator ResetCupCooldown(int cupID)
        {
            if (cupStates.TryGetValue(cupID, out CupState state))
            {
                state.isOnCooldown = true;
                cupStates[cupID] = state;

                yield return new WaitForSeconds(cooldownTime);

                if (cupStates.TryGetValue(cupID, out state))
                {
                    state.isOnCooldown = false;
                    cupStates[cupID] = state;
                }
            }
        }

        public void RecordRespawn(GameObject cup)
        {
            int cupID = cup.GetInstanceID();

            if (cupStates.TryGetValue(cupID, out CupState state))
            {
                // Reset cup state when respawned
                state.hasBeenHit = false;
                state.isOnFloor = false;
                state.originalPosition = cup.transform.position;
                state.originalRotation = cup.transform.rotation;
                state.lastStableTime = Time.time;
                cupStates[cupID] = state;
            }
        }

        public void RecordFloorHit(GameObject cup)
        {
            int cupID = cup.GetInstanceID();

            if (cupStates.TryGetValue(cupID, out CupState state))
            {
                if (!state.isOnFloor)
                {
                    state.isOnFloor = true;
                    cupStates[cupID] = state;
                }
            }
        }

        public void CheckCupTipped(GameObject cup)
        {
            int cupID = cup.GetInstanceID();

            if (cupStates.TryGetValue(cupID, out CupState state))
            {
                if (!state.hasBeenHit && !state.isOnCooldown && Time.time - state.lastStableTime > 0.5f)
                {
                    // Calculate the angle between the cup's up vector and world up
                    float angle = Vector3.Angle(cup.transform.up, state.originalRotation * Vector3.up);

                    // If cup has tipped over beyond our tolerance
                    if (angle > angleTolerance)
                    {
                        successfulHits++;
                        state.hasBeenHit = true;
                        cupStates[cupID] = state;
                        UpdateCounterDisplay();

                        // Start cooldown
                        StartCoroutine(ResetCupCooldown(cupID));
                    }
                }
            }
        }

        private void UpdateCounterDisplay()
        {
            if (counterText == null) return;

            if (singleCounterOnly)
            {
                counterText.text = string.Format(singleCounterFormat, successfulHits);
            }
            else
            {
                int floorCount = 0;
                int respawnCount = 0;

                foreach (var state in cupStates.Values)
                {
                    if (state.isOnFloor) floorCount++;
                    if (state.hasRespawned) respawnCount++;
                }

                counterText.text = string.Format(detailedFormat,
                    successfulHits, floorCount, respawnCount);
            }
        }

        public void ResetCounters()
        {
            successfulHits = 0;
            cupStates.Clear();
            UpdateCounterDisplay();
        }

        [System.Serializable]
        private struct CupState
        {
            public Vector3 originalPosition;
            public Quaternion originalRotation;
            public bool hasBeenHit;
            public bool isOnFloor;
            public bool hasRespawned;
            public bool isOnCooldown;
            public float lastStableTime;
        }

        public class BallCollisionTracker : MonoBehaviour
        {
            private CupHitCounter counter;
            private string cupTagToTrack;
            private float minForce;
            private Dictionary<int, float> lastHitTime = new Dictionary<int, float>();

            public void Initialize(CupHitCounter hitCounter, string cupTag, float minimumForce)
            {
                counter = hitCounter;
                cupTagToTrack = cupTag;
                minForce = minimumForce;
            }

            private void OnCollisionEnter(Collision collision)
            {
                if (counter == null || string.IsNullOrEmpty(cupTagToTrack)) return;

                GameObject collidedObject = collision.gameObject;

                if (collidedObject.CompareTag(cupTagToTrack) &&
                    collision.relativeVelocity.magnitude > minForce)
                {
                    int cupID = collidedObject.GetInstanceID();

                    // Throttle collision detection
                    float currentTime = Time.time;
                    if (!lastHitTime.ContainsKey(cupID) || currentTime - lastHitTime[cupID] > 0.5f)
                    {
                        counter.RecordSuccessfulHit(collidedObject, true);
                        lastHitTime[cupID] = currentTime;
                    }
                }
            }
        }

        public class CupToCupCollisionTracker : MonoBehaviour
        {
            private CupHitCounter counter;
            private string cupTagToTrack;
            private float minForce;
            private Dictionary<int, float> lastHitTime = new Dictionary<int, float>();
            private Rigidbody rb;

            public void Initialize(CupHitCounter hitCounter, string cupTag, float minimumForce)
            {
                counter = hitCounter;
                cupTagToTrack = cupTag;
                minForce = minimumForce;
                rb = GetComponent<Rigidbody>();
            }

            private void OnCollisionEnter(Collision collision)
            {
                if (counter == null || string.IsNullOrEmpty(cupTagToTrack) || rb == null) return;

                // Only consider collisions where this cup is moving significantly
                if (rb.linearVelocity.magnitude < 0.1f) return;

                GameObject collidedObject = collision.gameObject;

                if (collidedObject.CompareTag(cupTagToTrack) &&
                    collision.relativeVelocity.magnitude > minForce)
                {
                    int cupID = collidedObject.GetInstanceID();

                    // Throttle collision detection
                    float currentTime = Time.time;
                    if (!lastHitTime.ContainsKey(cupID) || currentTime - lastHitTime[cupID] > 0.5f)
                    {
                        // Record the other cup as hit (the one being knocked over)
                        counter.RecordSuccessfulHit(collidedObject, false);
                        lastHitTime[cupID] = currentTime;
                    }
                }
            }
        }

        public class CupTracker : MonoBehaviour
        {
            private CupHitCounter counter;
            private float floorY;
            private Vector3 lastPosition;
            private Quaternion lastRotation;
            private float lastMovedTime;
            private bool hasDetectedRespawn = false;
            private Rigidbody rb;
            private float stableCheckInterval = 0.2f;
            private float lastStableCheck = 0f;

            public void Initialize(CupHitCounter hitCounter, float floor)
            {
                counter = hitCounter;
                floorY = floor;
                lastPosition = transform.position;
                lastRotation = transform.rotation;
                lastMovedTime = Time.time;
                rb = GetComponent<Rigidbody>();
            }

            private void Update()
            {
                if (counter == null) return;

                // Check if cup has hit the floor
                if (transform.position.y <= floorY)
                {
                    counter.RecordFloorHit(gameObject);
                }

                // Check for respawn (sudden large movement)
                Vector3 currentPos = transform.position;
                float distance = Vector3.Distance(currentPos, lastPosition);

                // Detect sudden large position changes (likely respawns)
                if (distance > 0.5f && Time.time - lastMovedTime > 0.2f)
                {
                    counter.RecordRespawn(gameObject);
                    lastMovedTime = Time.time;
                }

                // Periodically check if cup has tipped over
                if (Time.time - lastStableCheck > stableCheckInterval)
                {
                    lastStableCheck = Time.time;

                    if (rb != null && rb.linearVelocity.magnitude < 0.05f &&
                        Quaternion.Angle(transform.rotation, lastRotation) < 1f)
                    {
                        counter.CheckCupTipped(gameObject);
                    }

                    lastRotation = transform.rotation;
                }

                lastPosition = currentPos;
            }
        }
    }
}
