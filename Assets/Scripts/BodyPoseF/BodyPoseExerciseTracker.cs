using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Oculus.Interaction;
using Oculus.Interaction.Body.Input;
using Oculus.Interaction.Body.PoseDetection;
using UnityEditor;
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

        [Tooltip("Optional: Load poses from a folder (takes precedence over individual pose assignments)")]
        [SerializeField] private string _posesFolderPath = "Assets/Poses";

        [Tooltip("The sequence of poses to detect during the exercise (if not loading from folder)")]
        [SerializeField, Interface(typeof(IBodyPose))]
        private List<UnityEngine.Object> _exercisePoses = new List<UnityEngine.Object>();

        [Header("Joint Comparison Configuration")]
        [Tooltip("Joint configuration for pose comparison")]
        [SerializeField]
        private List<BodyPoseComparerActiveStateMultiFree.JointComparerConfig> _jointConfig = new List<BodyPoseComparerActiveStateMultiFree.JointComparerConfig>
        {
            new BodyPoseComparerActiveStateMultiFree.JointComparerConfig(BodyJointId.Body_Head, 30f, 4f),
            new BodyPoseComparerActiveStateMultiFree.JointComparerConfig(BodyJointId.Body_LeftArmUpper, 30f, 4f),
            new BodyPoseComparerActiveStateMultiFree.JointComparerConfig(BodyJointId.Body_LeftArmLower, 30f, 4f),
            new BodyPoseComparerActiveStateMultiFree.JointComparerConfig(BodyJointId.Body_LeftHandWrist, 36f, 4f),
            new BodyPoseComparerActiveStateMultiFree.JointComparerConfig(BodyJointId.Body_RightArmUpper, 30f, 4f),
            new BodyPoseComparerActiveStateMultiFree.JointComparerConfig(BodyJointId.Body_RightArmLower, 30f, 4f),
            new BodyPoseComparerActiveStateMultiFree.JointComparerConfig(BodyJointId.Body_RightHandWrist, 36f, 4f)
        };

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
        private int _matchedPoseIndex = -1;

        // Record of which poses were successfully detected and their accuracy
        private Dictionary<int, float> _detectedPoses = new Dictionary<int, float>();
        private Dictionary<int, float> _poseAccuracy = new Dictionary<int, float>();
        private Dictionary<int, float> _bestPoseAccuracy = new Dictionary<int, float>();
        private List<IBodyPose> _loadedPoses = new List<IBodyPose>();

        // Property to get the remaining time in the exercise
        public float RemainingTime => Mathf.Max(0, _exerciseDuration - _exerciseElapsedTime);

        // Property to get the percentage of poses detected
        public float CompletionPercentage
        {
            get
            {
                int totalPoses = _loadedPoses.Count > 0 ? _loadedPoses.Count : _exercisePoses.Count;
                return totalPoses > 0 ? (float)_detectedPoses.Count / totalPoses * 100f : 0f;
            }
        }

        // Property to get overall accuracy
        public float OverallAccuracy
        {
            get
            {
                if (_bestPoseAccuracy.Count == 0) return 0f;
                int totalPoses = _loadedPoses.Count > 0 ? _loadedPoses.Count : _exercisePoses.Count;
                return _bestPoseAccuracy.Values.Sum() / totalPoses;
            }
        }

        protected virtual void Awake()
        {
            _poseComparer = GetComponent<BodyPoseComparerActiveStateMultiFree>();
            LoadPoses();
        }

        protected virtual void Start()
        {
            // Configure pose comparer with the source pose
            if (_sourcePose != null)
            {
                _poseComparer.InjectSourcePose(_sourcePose as IBodyPose);
            }

            // Configure joint comparison settings
            _poseComparer.InjectJoints(_jointConfig);
        }

        /// <summary>
        /// Loads poses either from folder or from assigned list
        /// </summary>
        private void LoadPoses()
        {
            _loadedPoses.Clear();

            // First try to load from folder if specified
            if (!string.IsNullOrEmpty(_posesFolderPath))
            {
                if (Directory.Exists(_posesFolderPath))
                {
                    // Load all pose assets from the folder
                    string[] files = Directory.GetFiles(_posesFolderPath, "*.asset", SearchOption.TopDirectoryOnly);

                    List<IBodyPose> folderPoses = new List<IBodyPose>();
                    foreach (string file in files)
                    {
                        string relativePath = file.Replace(Application.dataPath, "Assets");
#if UNITY_EDITOR
                        UnityEngine.Object poseAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(relativePath);
                        if (poseAsset != null && poseAsset is IBodyPose bodyPose)
                        {
                            folderPoses.Add(bodyPose);
                        }
#endif
                    }

                    if (folderPoses.Count > 0)
                    {
                        _loadedPoses = folderPoses;
                        Debug.Log($"Loaded {_loadedPoses.Count} poses from folder: {_posesFolderPath}");
                    }
                    else
                    {
                        Debug.LogWarning($"No valid pose assets found in folder: {_posesFolderPath}");
                    }
                }
                else
                {
                    Debug.LogWarning($"Poses folder not found: {_posesFolderPath}");
                }
            }

            // If nothing loaded from folder, use individually assigned poses
            if (_loadedPoses.Count == 0)
            {
                foreach (var pose in _exercisePoses)
                {
                    if (pose is IBodyPose bodyPose)
                    {
                        _loadedPoses.Add(bodyPose);
                    }
                }
                Debug.Log($"Using {_loadedPoses.Count} individually assigned poses");
            }

            // Initialize detection data structures
            for (int i = 0; i < _loadedPoses.Count; i++)
            {
                _poseAccuracy[i] = 0f;
                _bestPoseAccuracy[i] = 0f;
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

            // Make sure poses are loaded
            if (_loadedPoses.Count == 0)
            {
                LoadPoses();
                if (_loadedPoses.Count == 0)
                {
                    Debug.LogError("No poses loaded for exercise. Cannot start.");
                    return;
                }
            }

            // Reset exercise data
            _detectedPoses.Clear();
            _poseAccuracy.Clear();
            _bestPoseAccuracy.Clear();

            // Initialize tracking data
            for (int i = 0; i < _loadedPoses.Count; i++)
            {
                _poseAccuracy[i] = 0f;
                _bestPoseAccuracy[i] = 0f;
            }

            _exerciseStartTime = Time.time;
            _exerciseElapsedTime = 0f;
            _currentPoseIndex = -1;
            _matchedPoseIndex = -1;

            // Configure pose comparer with exercise poses
            _poseComparer.InjectReferencePoses(_loadedPoses);

            // Create timer display if needed
            if (_showTimer)
            {
                CreateTimerDisplay();
            }

            _isExerciseActive = true;
            _onExerciseStarted?.Invoke();

            Debug.Log($"Exercise started. {_loadedPoses.Count} poses to detect in {_exerciseDuration} seconds.");
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
            Debug.Log($"Detected {_detectedPoses.Count} out of {_loadedPoses.Count} poses.");

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
                // Find which pose is being matched
                int detectedPoseIndex = GetDetectedPoseIndex();
                if (detectedPoseIndex >= 0)
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
        /// Gets the index of the currently detected pose by comparing against each pose in sequence
        /// </summary>
        private int GetDetectedPoseIndex()
        {
            // We need to determine which of the reference poses is currently being matched
            // Since BodyPoseComparerActiveStateMultiFree doesn't expose which pose is matched,
            // we need to manually perform the comparison again

            for (int i = 0; i < _loadedPoses.Count; i++)
            {
                IBodyPose referencePose = _loadedPoses[i];
                bool poseMatches = true;

                // Check each joint configuration
                foreach (var config in _jointConfig)
                {
                    float maxDelta = _poseComparer.Active ?
                                    config.MaxDelta + config.Width / 2f :
                                    config.MaxDelta - config.Width / 2f;

                    if (!GetJointDelta(_sourcePose as IBodyPose, referencePose, config.Joint, out float delta) ||
                        Mathf.Abs(delta) > maxDelta)
                    {
                        poseMatches = false;
                        break;
                    }
                }

                if (poseMatches)
                {
                    return i; // Found a matching pose
                }
            }

            return -1; // No match found
        }

        /// <summary>
        /// Calculates angle delta between two poses for a specific joint
        /// </summary>
        private bool GetJointDelta(IBodyPose sourcePose, IBodyPose referencePose, BodyJointId joint, out float delta)
        {
            if (!sourcePose.GetJointPoseLocal(joint, out Pose localSource) ||
                !referencePose.GetJointPoseLocal(joint, out Pose localRef))
            {
                delta = 0;
                return false;
            }

            delta = Quaternion.Angle(localSource.rotation, localRef.rotation);
            return true;
        }

        /// <summary>
        /// Calculates the accuracy percentage for a specific pose
        /// </summary>
        private float CalculatePoseAccuracy(int poseIndex)
        {
            if (poseIndex < 0 || poseIndex >= _loadedPoses.Count)
                return 0f;

            IBodyPose referencePose = _loadedPoses[poseIndex];
            IBodyPose sourcePose = _sourcePose as IBodyPose;

            if (sourcePose == null || referencePose == null)
                return 0f;

            // Calculate total accuracy across all joints
            float totalMaxAngle = 0f;
            float totalCurrentAngle = 0f;
            int validJoints = 0;

            foreach (var config in _jointConfig)
            {
                if (GetJointDelta(sourcePose, referencePose, config.Joint, out float delta))
                {
                    totalMaxAngle += config.MaxDelta;
                    totalCurrentAngle += Mathf.Min(delta, config.MaxDelta);
                    validJoints++;
                }
            }

            if (validJoints == 0)
                return 0f;

            // Convert to percentage (lower angle = higher accuracy)
            float accuracyPercent = (1f - (totalCurrentAngle / totalMaxAngle)) * 100f;
            return Mathf.Clamp(accuracyPercent, 0f, 100f);
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

        /// <summary>
        /// Load poses by specific path
        /// </summary>
        public void LoadPosesFromPath(string folderPath)
        {
            _posesFolderPath = folderPath;
            LoadPoses();
        }

        /// <summary>
        /// Set joint configuration for pose comparison
        /// </summary>
        public void SetJointConfig(List<BodyPoseComparerActiveStateMultiFree.JointComparerConfig> config)
        {
            _jointConfig = new List<BodyPoseComparerActiveStateMultiFree.JointComparerConfig>(config);
            if (_poseComparer != null)
            {
                _poseComparer.InjectJoints(_jointConfig);
            }
        }
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(BodyPoseExerciseTracker))]
    public class BodyPoseExerciseTrackerEditor : UnityEditor.Editor
    {
        private bool _showJointConfigs = true;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            BodyPoseExerciseTracker tracker = (BodyPoseExerciseTracker)target;

            UnityEditor.EditorGUILayout.Space();

            // Browse folder button
            using (new UnityEditor.EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Browse Poses Folder"))
                {
                    string path = UnityEditor.EditorUtility.OpenFolderPanel("Select Poses Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        // Convert absolute path to relative project path
                        if (path.StartsWith(Application.dataPath))
                        {
                            path = "Assets" + path.Substring(Application.dataPath.Length);
                        }

                        // Set the path via serialized property
                        SerializedProperty posesFolderPathProp = serializedObject.FindProperty("_posesFolderPath");
                        posesFolderPathProp.stringValue = path;
                        serializedObject.ApplyModifiedProperties();

                        // Reload poses
                        tracker.Invoke("LoadPoses", 0.1f);
                    }
                }

                if (GUILayout.Button("Reload Poses"))
                {
                    tracker.Invoke("LoadPoses", 0.1f);
                }
            }

            UnityEditor.EditorGUILayout.Space();

            // Exercise controls
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
