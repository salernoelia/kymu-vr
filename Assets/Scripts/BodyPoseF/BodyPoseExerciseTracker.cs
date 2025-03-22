using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Oculus.Interaction;
using Oculus.Interaction.Body.Input;
using Oculus.Interaction.Body.PoseDetection;
using UnityEngine;
using UnityEngine.Events;

namespace BodyPoseF
{
    /// <summary>
    /// Tracks which poses a user hits during an exercise session without requiring them to be performed in order.
    /// Allows the user to freely move through the poses within a time limit and tracks their accuracy.
    /// </summary>
    [RequireComponent(typeof(BodyPoseComparerActiveStateMultiFree))]
    public class BodyPoseExerciseTracker : MonoBehaviour
    {
        [Serializable]
        public class PoseDetectionEvent : UnityEvent<int, float> { } // PoseIndex, AccuracyScore

        [Serializable]
        public class ExerciseCompletionEvent : UnityEvent<Dictionary<int, float>, float> { } // PoseIndex->Accuracy, OverallAccuracy

        [Header("Exercise Configuration")]
        [Tooltip("Maximum time allowed for the exercise in seconds")]
        [SerializeField] private float _exerciseDuration = 60f;

        [Tooltip("Time a pose must be maintained to be considered successfully detected")]
        [SerializeField] private float _poseHoldTime = 0.5f;

        [Tooltip("Whether a pose can be detected multiple times during the exercise")]
        [SerializeField] private bool _allowRepeatedDetections = false;

        [Header("Pose References")]
        [Tooltip("The source body pose to compare against reference poses")]
        [SerializeField, Interface(typeof(IBodyPose))]
        private UnityEngine.Object _sourcePose;

        [Tooltip("The sequence of poses to detect during the exercise")]
        [SerializeField, Interface(typeof(IBodyPose))]
        private List<UnityEngine.Object> _exercisePoses = new List<UnityEngine.Object>();

        [Header("Status Display")]
        [Tooltip("Display a timer during the exercise")]
        [SerializeField] private bool _showTimer = true;

        [Header("Events")]
        [SerializeField] private PoseDetectionEvent _onPoseDetected = new PoseDetectionEvent();
        [SerializeField] private UnityEvent _onExerciseStarted = new UnityEvent();
        [SerializeField] private UnityEvent _onExercisePaused = new UnityEvent();
        [SerializeField] private UnityEvent _onExerciseResumed = new UnityEvent();
        [SerializeField] private ExerciseCompletionEvent _onExerciseCompleted = new ExerciseCompletionEvent();

        private BodyPoseComparerActiveStateMultiFree _poseComparer;
        private bool _isExerciseActive = false;
        private float _exerciseStartTime = 0f;
        private float _exerciseElapsedTime = 0f;
        private GameObject _timerTextObj;
        private int _currentPoseIndex = -1;
        private float _currentPoseHoldStartTime = 0f;

        // Record of which poses were successfully detected and their accuracy
        private Dictionary<int, float> _detectedPoses = new Dictionary<int, float>();
        private Dictionary<int, float> _poseAccuracy = new Dictionary<int, float>();
        private Dictionary<int, float> _bestPoseAccuracy = new Dictionary<int, float>();

        // Property to get the remaining time in the exercise
        public float RemainingTime => Mathf.Max(0, _exerciseDuration - _exerciseElapsedTime);

        // Property to get the percentage of poses detected
        public float CompletionPercentage => _exercisePoses.Count > 0 ?
            (float)_detectedPoses.Count / _exercisePoses.Count * 100f : 0f;

        // Property to get overall accuracy
        public float OverallAccuracy
        {
            get
            {
                if (_bestPoseAccuracy.Count == 0) return 0f;
                return _bestPoseAccuracy.Values.Sum() / _exercisePoses.Count;
            }
        }

        protected virtual void Awake()
        {
            _poseComparer = GetComponent<BodyPoseComparerActiveStateMultiFree>();

            // Initialize detection data structures
            for (int i = 0; i < _exercisePoses.Count; i++)
            {
                _poseAccuracy[i] = 0f;
                _bestPoseAccuracy[i] = 0f;
            }
        }

        protected virtual void Start()
        {
            // Configure pose comparer with the source pose
            if (_sourcePose != null)
            {
                _poseComparer.InjectSourcePose(_sourcePose as IBodyPose);
            }
        }

        protected virtual void Update()
        {
            if (_isExerciseActive)
            {
                _exerciseElapsedTime = Time.time - _exerciseStartTime;

                // Update timer display if enabled
                if (_showTimer)
                {
                    UpdateTimerDisplay(RemainingTime);
                }

                // Check if time is up
                if (_exerciseElapsedTime >= _exerciseDuration)
                {
                    StopExercise();
                    return;
                }

                // Get the current pose from the comparer
                CheckCurrentPose();
            }
        }

        /// <summary>
        /// Starts the exercise tracking
        /// </summary>
        public void StartExercise()
        {
            if (_isExerciseActive) return;

            // Reset exercise data
            _detectedPoses.Clear();
            _poseAccuracy.Clear();
            _bestPoseAccuracy.Clear();

            // Initialize tracking data
            for (int i = 0; i < _exercisePoses.Count; i++)
            {
                _poseAccuracy[i] = 0f;
                _bestPoseAccuracy[i] = 0f;
            }

            _exerciseStartTime = Time.time;
            _exerciseElapsedTime = 0f;
            _currentPoseIndex = -1;

            // Configure pose comparer with all exercise poses
            List<IBodyPose> poses = new List<IBodyPose>();
            foreach (var pose in _exercisePoses)
            {
                poses.Add(pose as IBodyPose);
            }
            _poseComparer.InjectReferencePoses(poses);

            // Create timer display if needed
            if (_showTimer)
            {
                CreateTimerDisplay();
            }

            _isExerciseActive = true;
            _onExerciseStarted?.Invoke();

            Debug.Log($"Exercise started. {_exercisePoses.Count} poses to detect in {_exerciseDuration} seconds.");
        }

        /// <summary>
        /// Pauses the current exercise
        /// </summary>
        public void PauseExercise()
        {
            if (!_isExerciseActive) return;

            _isExerciseActive = false;
            _onExercisePaused?.Invoke();

            Debug.Log("Exercise paused.");
        }

        /// <summary>
        /// Resumes the exercise from a paused state
        /// </summary>
        public void ResumeExercise()
        {
            if (_isExerciseActive) return;

            // Adjust the start time to account for the pause duration
            _exerciseStartTime = Time.time - _exerciseElapsedTime;
            _isExerciseActive = true;
            _onExerciseResumed?.Invoke();

            Debug.Log("Exercise resumed.");
        }

        /// <summary>
        /// Stops the exercise and calculates final results
        /// </summary>
        public void StopExercise()
        {
            if (!_isExerciseActive) return;

            _isExerciseActive = false;

            // Clean up timer display
            if (_timerTextObj != null)
            {
                DestroyTimerDisplay();
            }

            // Calculate overall stats
            float overallAccuracy = OverallAccuracy;

            // Make a copy of the final results to pass in the event
            Dictionary<int, float> finalResults = new Dictionary<int, float>(_bestPoseAccuracy);

            // Invoke completion event
            _onExerciseCompleted?.Invoke(finalResults, overallAccuracy);

            Debug.Log($"Exercise completed. Overall accuracy: {overallAccuracy:F1}%");
            Debug.Log($"Detected {_detectedPoses.Count} out of {_exercisePoses.Count} poses.");

            // Log individual pose results
            foreach (var pair in _bestPoseAccuracy)
            {
                Debug.Log($"Pose {pair.Key}: {pair.Value:F1}% accuracy");
            }
        }

        /// <summary>
        /// Sets the exercise duration
        /// </summary>
        public void SetExerciseDuration(float seconds)
        {
            _exerciseDuration = Mathf.Max(1f, seconds);
        }

        /// <summary>
        /// Checks which pose the user is currently matching
        /// </summary>
        private void CheckCurrentPose()
        {
            // The pose comparer's Active property performs the comparison
            if (_poseComparer.Active)
            {
                // A pose is being detected
                int detectedPoseIndex = GetDetectedPoseIndex();

                if (detectedPoseIndex >= 0 && detectedPoseIndex < _exercisePoses.Count)
                {
                    // If it's a new pose or we allow repeated poses
                    if (detectedPoseIndex != _currentPoseIndex)
                    {
                        _currentPoseIndex = detectedPoseIndex;
                        _currentPoseHoldStartTime = Time.time;

                        Debug.Log($"Started detecting pose {_currentPoseIndex}");
                    }
                    else
                    {
                        // Calculate hold time
                        float holdTime = Time.time - _currentPoseHoldStartTime;

                        // If the pose has been held long enough
                        if (holdTime >= _poseHoldTime)
                        {
                            // Already counted this pose and not allowing repeats?
                            if (_detectedPoses.ContainsKey(_currentPoseIndex) && !_allowRepeatedDetections)
                            {
                                // Nothing to do, we already counted this pose
                                return;
                            }

                            // Calculate accuracy for this pose
                            float accuracy = CalculatePoseAccuracy(_currentPoseIndex);
                            _poseAccuracy[_currentPoseIndex] = accuracy;

                            // Track the best accuracy for this pose
                            if (!_bestPoseAccuracy.ContainsKey(_currentPoseIndex) ||
                                accuracy > _bestPoseAccuracy[_currentPoseIndex])
                            {
                                _bestPoseAccuracy[_currentPoseIndex] = accuracy;
                            }

                            // Mark the pose as detected
                            _detectedPoses[_currentPoseIndex] = Time.time;

                            // Raise the event
                            _onPoseDetected?.Invoke(_currentPoseIndex, accuracy);

                            Debug.Log($"Detected pose {_currentPoseIndex} with {accuracy:F1}% accuracy");

                            // Reset the current pose to allow detecting a different pose
                            _currentPoseIndex = -1;
                        }
                    }
                }
            }
            else
            {
                // If no pose is detected, reset the current pose
                _currentPoseIndex = -1;
            }
        }

        /// <summary>
        /// Gets the index of the currently detected pose
        /// </summary>
        private int GetDetectedPoseIndex()
        {
            // Check the pose comparer's feature states to determine which pose is being matched
            var featureStates = _poseComparer.FeatureStates;

            // Get the index from the pose comparer
            // This relies on the internal implementation of BodyPoseComparerActiveStateMultiFree
            // which stores the matched pose index
            if (featureStates.Count > 0)
            {
                foreach (var state in featureStates)
                {
                    // We need to access the matching pose index from the comparer
                    var config = state.Key;
                    var poseState = state.Value;

                    // For now, use a simple approach: the detected pose is the one that has feature states
                    // In a more complete implementation, this would get the actual pose index
                    return 0; // Placeholder - would need to access the actual matched pose index
                }
            }

            return -1;
        }

        /// <summary>
        /// Calculates the accuracy percentage for a specific pose
        /// </summary>
        private float CalculatePoseAccuracy(int poseIndex)
        {
            // Get the feature states from the pose comparer
            var featureStates = _poseComparer.FeatureStates;

            if (featureStates.Count == 0)
            {
                return 0f;
            }

            // Sum up how close we are to the perfect pose
            float totalDelta = 0f;
            float totalMaxDelta = 0f;

            foreach (var state in featureStates)
            {
                var config = state.Key;
                var poseState = state.Value;

                totalDelta += poseState.Delta;
                totalMaxDelta += poseState.MaxDelta;
            }

            // Calculate accuracy as percentage
            if (totalMaxDelta <= 0f)
            {
                return 100f;
            }

            // Higher accuracy when delta is smaller
            float accuracy = (1f - (totalDelta / totalMaxDelta)) * 100f;
            return Mathf.Clamp(accuracy, 0f, 100f);
        }

        /// <summary>
        /// Creates a 3D text object to display the timer countdown
        /// </summary>
        private void CreateTimerDisplay()
        {
            // Create a new game object for the timer text
            _timerTextObj = new GameObject("ExerciseTimerDisplay");

            // Add TextMesh component
            TextMesh textMesh = _timerTextObj.AddComponent<TextMesh>();
            textMesh.fontSize = 48;
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.color = Color.white;
            textMesh.text = _exerciseDuration.ToString("F0");

            // Add a MeshRenderer and configure it
            MeshRenderer meshRenderer = _timerTextObj.GetComponent<MeshRenderer>();
            meshRenderer.material = new Material(Shader.Find("GUI/Text Shader"));
            meshRenderer.material.color = Color.white;

            // Position in front of the main camera
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                _timerTextObj.transform.position = mainCamera.transform.position + mainCamera.transform.forward * 2f;
                _timerTextObj.transform.rotation = mainCamera.transform.rotation;
                _timerTextObj.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
            }
            else
            {
                // Fallback if no main camera
                _timerTextObj.transform.position = new Vector3(0, 1.6f, 2f); // Approximate head height
                _timerTextObj.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
            }
        }

        /// <summary>
        /// Updates the timer display with the current remaining time
        /// </summary>
        private void UpdateTimerDisplay(float remainingTime)
        {
            if (_timerTextObj != null)
            {
                TextMesh textMesh = _timerTextObj.GetComponent<TextMesh>();
                if (textMesh != null)
                {
                    // Display with one decimal place
                    textMesh.text = remainingTime.ToString("F0");

                    // Change color as time runs out
                    if (remainingTime < 10.0f)
                    {
                        textMesh.color = Color.red;
                    }
                    else if (remainingTime < 30.0f)
                    {
                        textMesh.color = Color.yellow;
                    }

                    // Update position to stay in front of camera
                    Camera mainCamera = Camera.main;
                    if (mainCamera != null)
                    {
                        _timerTextObj.transform.position = mainCamera.transform.position + mainCamera.transform.forward * 2f;
                        _timerTextObj.transform.rotation = mainCamera.transform.rotation;
                    }
                }
            }
        }

        /// <summary>
        /// Destroys the timer display object
        /// </summary>
        private void DestroyTimerDisplay()
        {
            if (_timerTextObj != null)
            {
                Destroy(_timerTextObj);
                _timerTextObj = null;
            }
        }
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(BodyPoseExerciseTracker))]
    public class BodyPoseExerciseTrackerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            BodyPoseExerciseTracker tracker = (BodyPoseExerciseTracker)target;

            UnityEditor.EditorGUILayout.Space();

            using (new UnityEditor.EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Start Exercise"))
                {
                    tracker.StartExercise();
                }

                if (GUILayout.Button("Stop Exercise"))
                {
                    tracker.StopExercise();
                }
            }

            using (new UnityEditor.EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Pause"))
                {
                    tracker.PauseExercise();
                }

                if (GUILayout.Button("Resume"))
                {
                    tracker.ResumeExercise();
                }
            }

            UnityEditor.EditorGUILayout.Space();
            UnityEditor.EditorGUILayout.LabelField("Status", UnityEditor.EditorStyles.boldLabel);

            if (Application.isPlaying)
            {
                UnityEditor.EditorGUILayout.LabelField($"Remaining Time: {tracker.RemainingTime:F1} seconds");
                UnityEditor.EditorGUILayout.LabelField($"Completion: {tracker.CompletionPercentage:F1}%");
                UnityEditor.EditorGUILayout.LabelField($"Overall Accuracy: {tracker.OverallAccuracy:F1}%");
            }
        }
    }
#endif
}
