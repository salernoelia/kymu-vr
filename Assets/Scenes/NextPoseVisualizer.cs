using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Oculus.Interaction;
using Oculus.Interaction.Body.Input;
using Oculus.Interaction.Body.PoseDetection;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BodyPoseF
{
    /// <summary>
    /// Shows the next pose that should be performed in the exercise sequence.
    /// Adapts to the user's current position and direction in the sequence.
    /// </summary>
    [RequireComponent(typeof(OVRBodyPoseSkeletonProvider))]
    public class NextPoseVisualizer : MonoBehaviour
    {
        [Tooltip("Reference to the BodyPoseExerciseTracker component")]
        [SerializeField] private BodyPoseExerciseTracker exerciseTracker;

        [Tooltip("The skeleton on which to display the next pose")]
        [SerializeField] private OVRSkeleton displaySkeleton;

        [Tooltip("Whether to display the suggested pose automatically")]
        [SerializeField] private bool autoShowNextPose = true;

        [Tooltip("The opacity level for the skeleton displaying the next pose")]
        [Range(0.1f, 1.0f)]
        [SerializeField] private float skeletonOpacity = 0.6f;

        [Tooltip("Distance from the user skeleton where the next pose will be displayed")]
        [SerializeField] private float displayDistance = 1.0f;

        [Tooltip("Whether to show pose numbers in the scene")]
        [SerializeField] private bool showPoseLabels = true;

        [Tooltip("Whether to automatically guess the next pose based on the user's motion direction")]
        [SerializeField] private bool adaptiveDirection = true;

        private OVRBodyPoseSkeletonProvider _skeletonProvider;
        private List<IBodyPose> _loadedPoses = new List<IBodyPose>();
        private int _lastDetectedPoseIndex = -1;
        private int _nextSuggestedPoseIndex = -1;
        private bool _isAscendingSequence = true;
        private GameObject _poseNumberLabel;
        private TextMesh _poseNumberText;

        // Exercise tracking
        private int _highestPoseReached = -1;
        private float _lastPoseDetectionTime = 0f;

        protected virtual void Awake()
        {
            _skeletonProvider = GetComponent<OVRBodyPoseSkeletonProvider>();

            // Find the exercise tracker if not assigned
            if (exerciseTracker == null)
            {
                exerciseTracker = FindObjectOfType<BodyPoseExerciseTracker>();
                if (exerciseTracker == null)
                {
                    Debug.LogError("No BodyPoseExerciseTracker found in the scene. Please assign it in the inspector.");
                }
            }

            // Try to find the skeleton if not assigned
            if (displaySkeleton == null)
            {
                // Try to find skeleton in children or parent
                displaySkeleton = GetComponentInChildren<OVRSkeleton>(true);
                if (displaySkeleton == null)
                {
                    displaySkeleton = GetComponentInParent<OVRSkeleton>(true);
                }

                // If still not found, find any in scene
                if (displaySkeleton == null)
                {
                    displaySkeleton = FindObjectOfType<OVRSkeleton>();
                }

                if (displaySkeleton != null)
                {
                    Debug.Log($"Found OVRSkeleton on {displaySkeleton.gameObject.name}");
                }
                else
                {
                    Debug.LogWarning("No OVRSkeleton found. Next pose will not be visible.");
                }
            }

            // Create the pose number label if needed
            if (showPoseLabels)
            {
                CreatePoseLabel();
            }
        }

        protected virtual void Start()
        {
            // Subscribe to the pose detected event
            if (exerciseTracker != null)
            {
                exerciseTracker.GetType()
                    .GetField("_onPoseDetected", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(exerciseTracker)
                    ?.GetType()
                    .GetMethod("AddListener")
                    ?.Invoke(exerciseTracker.GetType().GetField("_onPoseDetected", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(exerciseTracker),
                        new object[] { new UnityEngine.Events.UnityAction<int, float>(OnPoseDetected) });

                // Also subscribe to exercise started/completed events
                exerciseTracker.GetType()
                    .GetField("_onExerciseStarted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(exerciseTracker)
                    ?.GetType()
                    .GetMethod("AddListener")
                    ?.Invoke(exerciseTracker.GetType().GetField("_onExerciseStarted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(exerciseTracker),
                        new object[] { new UnityEngine.Events.UnityAction(OnExerciseStarted) });

                exerciseTracker.GetType()
                    .GetField("_onExerciseCompleted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(exerciseTracker)
                    ?.GetType()
                    .GetMethod("AddListener")
                    ?.Invoke(exerciseTracker.GetType().GetField("_onExerciseCompleted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(exerciseTracker),
                        new object[] { new UnityEngine.Events.UnityAction<System.Collections.Generic.Dictionary<int, float>, float>(OnExerciseCompleted) });
            }

            // Load the poses from the exercise tracker
            LoadPoses();

            // Set the skeleton to semi-transparent
            SetSkeletonVisibility(skeletonOpacity);
        }

        protected virtual void OnDestroy()
        {
            // Unsubscribe from events
            if (exerciseTracker != null)
            {
                exerciseTracker.GetType()
                    .GetField("_onPoseDetected", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(exerciseTracker)
                    ?.GetType()
                    .GetMethod("RemoveListener")
                    ?.Invoke(exerciseTracker.GetType().GetField("_onPoseDetected", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(exerciseTracker),
                        new object[] { new UnityEngine.Events.UnityAction<int, float>(OnPoseDetected) });

                exerciseTracker.GetType()
                    .GetField("_onExerciseStarted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(exerciseTracker)
                    ?.GetType()
                    .GetMethod("RemoveListener")
                    ?.Invoke(exerciseTracker.GetType().GetField("_onExerciseStarted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(exerciseTracker),
                        new object[] { new UnityEngine.Events.UnityAction(OnExerciseStarted) });

                exerciseTracker.GetType()
                    .GetField("_onExerciseCompleted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(exerciseTracker)
                    ?.GetType()
                    .GetMethod("RemoveListener")
                    ?.Invoke(exerciseTracker.GetType().GetField("_onExerciseCompleted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(exerciseTracker),
                        new object[] { new UnityEngine.Events.UnityAction<System.Collections.Generic.Dictionary<int, float>, float>(OnExerciseCompleted) });
            }

            // Destroy the label if it exists
            if (_poseNumberLabel != null)
            {
                Destroy(_poseNumberLabel);
            }
        }

        private void OnExerciseStarted()
        {
            // Reset tracking variables
            _lastDetectedPoseIndex = -1;
            _nextSuggestedPoseIndex = -1;
            _highestPoseReached = -1;
            _isAscendingSequence = true;

            // Show initial pose suggestion (usually pose 0 or 1)
            if (autoShowNextPose && _loadedPoses.Count > 0)
            {
                _nextSuggestedPoseIndex = 0;
                DisplayNextPose();
            }
        }

        private void OnExerciseCompleted(System.Collections.Generic.Dictionary<int, float> poseResults, float overallAccuracy)
        {
            // Hide the skeleton and label when the exercise is complete
            SetSkeletonVisibility(0);
            if (_poseNumberLabel != null)
            {
                _poseNumberLabel.SetActive(false);
            }
        }

        protected virtual void Update()
        {
            // Keep the next pose visualization positioned correctly
            if (_nextSuggestedPoseIndex >= 0 && displaySkeleton != null && displaySkeleton.gameObject.activeSelf)
            {
                PositionSkeleton();
            }
        }

        /// <summary>
        /// Position the visualization skeleton relative to the user
        /// </summary>
        private void PositionSkeleton()
        {
            // Find the user's head/camera position
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                // Position the skeleton in front of the user
                Vector3 positionInFront = mainCamera.transform.position + mainCamera.transform.forward * displayDistance;
                
                // Keep the skeleton at the user's height but in front of them
                positionInFront.y = mainCamera.transform.position.y - 1.5f; // Adjust for height difference
                
                // Smoothly move the skeleton to the new position
                displaySkeleton.transform.position = Vector3.Lerp(
                    displaySkeleton.transform.position, 
                    positionInFront, 
                    Time.deltaTime * 5f);
                
                // Make the skeleton face the user
                displaySkeleton.transform.rotation = Quaternion.Lerp(
                    displaySkeleton.transform.rotation,
                    Quaternion.LookRotation(mainCamera.transform.position - displaySkeleton.transform.position),
                    Time.deltaTime * 5f);

                // Update the label position if it exists
                if (_poseNumberLabel != null && _poseNumberLabel.activeSelf)
                {
                    _poseNumberLabel.transform.position = displaySkeleton.transform.position + Vector3.up * 2f;
                    _poseNumberLabel.transform.rotation = Quaternion.LookRotation(
                        _poseNumberLabel.transform.position - mainCamera.transform.position);
                }
            }
        }

        /// <summary>
        /// Loads the poses from the exercise tracker
        /// </summary>
        private void LoadPoses()
        {
            if (exerciseTracker == null) return;

            // Use reflection to get the poses from the exercise tracker
            System.Reflection.FieldInfo loadedPosesField = exerciseTracker.GetType()
                .GetField("_loadedPoses", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (loadedPosesField != null)
            {
                var poses = loadedPosesField.GetValue(exerciseTracker) as List<IBodyPose>;
                if (poses != null)
                {
                    _loadedPoses = new List<IBodyPose>(poses);
                    Debug.Log($"Loaded {_loadedPoses.Count} poses from exercise tracker for next pose visualization");
                }
            }
        }

        /// <summary>
        /// Called when a pose is detected by the exercise tracker
        /// </summary>
        private void OnPoseDetected(int poseIndex, float accuracy)
        {
            if (_loadedPoses.Count == 0)
            {
                LoadPoses();
                if (_loadedPoses.Count == 0)
                {
                    Debug.LogWarning("No poses loaded from exercise tracker");
                    return;
                }
            }

            // Track the highest pose number reached
            if (poseIndex > _highestPoseReached)
            {
                _highestPoseReached = poseIndex;
            }

            // Determine if we're ascending or descending in the sequence
            if (_lastDetectedPoseIndex >= 0)
            {
                if (poseIndex > _lastDetectedPoseIndex)
                {
                    _isAscendingSequence = true;
                }
                else if (poseIndex < _lastDetectedPoseIndex)
                {
                    _isAscendingSequence = false;
                }
                // If poseIndex == _lastDetectedPoseIndex, keep current direction
            }

            _lastDetectedPoseIndex = poseIndex;
            _lastPoseDetectionTime = Time.time;

            // Determine the next suggested pose
            DetermineNextSuggestedPose();

            // Display the next suggested pose if auto-show is enabled
            if (autoShowNextPose)
            {
                DisplayNextPose();
            }
        }

        /// <summary>
        /// Determines which pose should be suggested next based on the current sequence direction
        /// </summary>
        private void DetermineNextSuggestedPose()
        {
            if (_loadedPoses.Count == 0) return;

            // Basic increment/decrement logic based on the detected direction
            if (_isAscendingSequence)
            {
                // Going up in the sequence
                if (_lastDetectedPoseIndex < _loadedPoses.Count - 1)
                {
                    _nextSuggestedPoseIndex = _lastDetectedPoseIndex + 1;
                }
                else
                {
                    // Reached the end, suggest going back down
                    _nextSuggestedPoseIndex = _loadedPoses.Count - 2;
                    _isAscendingSequence = false;
                }
            }
            else
            {
                // Going down in the sequence
                if (_lastDetectedPoseIndex > 0)
                {
                    _nextSuggestedPoseIndex = _lastDetectedPoseIndex - 1;
                }
                else
                {
                    // Reached the beginning, suggest going back up
                    _nextSuggestedPoseIndex = 1;
                    _isAscendingSequence = true;
                }
            }

            // Make sure we're within bounds
            _nextSuggestedPoseIndex = Mathf.Clamp(_nextSuggestedPoseIndex, 0, _loadedPoses.Count - 1);

            // Log the suggestion
            Debug.Log($"Next suggested pose: {_nextSuggestedPoseIndex} (Direction: {(_isAscendingSequence ? "Up" : "Down")})");
        }

        /// <summary>
        /// Displays the next suggested pose on the visualization skeleton
        /// </summary>
        private void DisplayNextPose()
        {
            if (_nextSuggestedPoseIndex < 0 || _nextSuggestedPoseIndex >= _loadedPoses.Count)
            {
                Debug.LogWarning($"Invalid next pose index: {_nextSuggestedPoseIndex}");
                return;
            }

            // Get the pose
            IBodyPose pose = _loadedPoses[_nextSuggestedPoseIndex];

            // Display the pose
            if (pose != null && displaySkeleton != null && _skeletonProvider != null)
            {
                // Check if pose is a BodyPose (Unity ScriptableObject)
                BodyPose bodyPose = pose as BodyPose;
                if (bodyPose == null)
                {
                    Debug.LogError("The next suggested pose is not a BodyPose ScriptableObject");
                    return;
                }

                // Apply the pose to the skeleton
                ApplyPose(bodyPose);

                // Ensure the skeleton is visible
                displaySkeleton.gameObject.SetActive(true);
                SetSkeletonVisibility(skeletonOpacity);

                // Update the pose number label
                UpdatePoseLabel(_nextSuggestedPoseIndex);
            }
        }

        /// <summary>
        /// Creates a text label to display the pose number
        /// </summary>
        private void CreatePoseLabel()
        {
            _poseNumberLabel = new GameObject("NextPoseLabel");
            _poseNumberText = _poseNumberLabel.AddComponent<TextMesh>();
            _poseNumberText.fontSize = 60;
            _poseNumberText.alignment = TextAlignment.Center;
            _poseNumberText.anchor = TextAnchor.MiddleCenter;
            _poseNumberText.color = Color.yellow;
            _poseNumberText.text = "?";

            // Add a MeshRenderer and configure it
            MeshRenderer meshRenderer = _poseNumberLabel.GetComponent<MeshRenderer>();
            meshRenderer.material = new Material(Shader.Find("GUI/Text Shader"));
            meshRenderer.material.color = Color.yellow;

            // Set initial scale
            _poseNumberLabel.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
        }

        /// <summary>
        /// Updates the pose number label text
        /// </summary>
        private void UpdatePoseLabel(int poseIndex)
        {
            if (_poseNumberLabel != null && _poseNumberText != null)
            {
                _poseNumberText.text = $"Next: Pose {poseIndex:D2}";
                _poseNumberLabel.SetActive(showPoseLabels);
            }
        }

        /// <summary>
        /// Sets the visibility of the skeleton by adjusting material transparency
        /// </summary>
        private void SetSkeletonVisibility(float alpha)
        {
            if (displaySkeleton == null) return;

            // Get all renderers in the skeleton
            Renderer[] renderers = displaySkeleton.GetComponentsInChildren<Renderer>();

            foreach (Renderer renderer in renderers)
            {
                // Update each material's alpha
                foreach (Material material in renderer.materials)
                {
                    // Check if the material supports transparency
                    if (material.HasProperty("_Color"))
                    {
                        Color color = material.color;
                        color.a = alpha;
                        material.color = color;
                    }

                    // Ensure proper rendering mode for transparency
                    if (alpha < 1.0f)
                    {
                        material.SetFloat("_Mode", 3); // Transparent mode
                        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        material.SetInt("_ZWrite", 0);
                        material.DisableKeyword("_ALPHATEST_ON");
                        material.EnableKeyword("_ALPHABLEND_ON");
                        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        material.renderQueue = 3000;
                    }
                    else
                    {
                        material.SetFloat("_Mode", 0); // Opaque mode
                        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                        material.SetInt("_ZWrite", 1);
                        material.DisableKeyword("_ALPHATEST_ON");
                        material.DisableKeyword("_ALPHABLEND_ON");
                        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        material.renderQueue = -1;
                    }
                }
            }
        }

        /// <summary>
        /// Applies a pose to the OVRBodyPoseSkeletonProvider and forces OVRSkeleton to update
        /// </summary>
        private void ApplyPose(BodyPose pose)
        {
            if (pose == null || _skeletonProvider == null)
            {
                return;
            }

            try
            {
                // Get the UnityEngine.Object field for setting in the skeleton provider
                var bodyPoseField = _skeletonProvider.GetType()
                    .GetField("_bodyPose", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                var bodyPoseProperty = _skeletonProvider.GetType()
                    .GetField("BodyPose", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (bodyPoseField != null)
                {
                    // Set the pose in the skeleton provider
                    bodyPoseField.SetValue(_skeletonProvider, pose);

                    // Also update the property that caches the interface reference
                    if (bodyPoseProperty != null)
                    {
                        bodyPoseProperty.SetValue(_skeletonProvider, pose);
                    }

                    // Force the skeleton to update
                    if (displaySkeleton != null)
                    {
                        ForceSkeletonUpdate(displaySkeleton);
                    }
                }
                else
                {
                    Debug.LogError("Failed to find _bodyPose field in OVRBodyPoseSkeletonProvider");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error applying pose: {ex.Message}");
            }
        }

        /// <summary>
        /// Forces an OVRSkeleton to update by accessing its internal update methods
        /// </summary>
        private void ForceSkeletonUpdate(OVRSkeleton skeleton)
        {
            if (skeleton == null)
                return;

            // Try several approaches to force the skeleton to update

            // Approach 1: Try to invoke the Update method directly
            var updateMethod = skeleton.GetType().GetMethod("Update",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (updateMethod != null)
            {
                updateMethod.Invoke(skeleton, null);
            }

            // Approach 2: Set the skeleton's bones dirty to force recalculation
            var bonesChangedMethod = skeleton.GetType().GetMethod("MarkBonesDirty",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (bonesChangedMethod != null)
            {
                bonesChangedMethod.Invoke(skeleton, null);
            }

            // Approach 3: Get the ShouldUpdateBones property and set it to true
            var shouldUpdateBonesField = skeleton.GetType().GetField("_shouldUpdateBones",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (shouldUpdateBonesField != null)
            {
                shouldUpdateBonesField.SetValue(skeleton, true);
            }

            // Final approach: Toggle skeleton enabled state
            bool wasEnabled = skeleton.enabled;
            skeleton.enabled = false;
            skeleton.enabled = true;
        }

        /// <summary>
        /// Manually shows the next suggested pose
        /// </summary>
        public void ShowNextPose()
        {
            if (_nextSuggestedPoseIndex < 0)
            {
                // If no suggestion yet, start with pose 0
                _nextSuggestedPoseIndex = 0;
            }
            
            DisplayNextPose();
        }

        /// <summary>
        /// Manually hides the next pose visualization
        /// </summary>
        public void HideNextPose()
        {
            if (displaySkeleton != null)
            {
                displaySkeleton.gameObject.SetActive(false);
            }

            if (_poseNumberLabel != null)
            {
                _poseNumberLabel.SetActive(false);
            }
        }

        /// <summary>
        /// Toggles the direction of the sequence (ascending/descending)
        /// </summary>
        public void ToggleDirection()
        {
            _isAscendingSequence = !_isAscendingSequence;
            DetermineNextSuggestedPose();
            
            if (autoShowNextPose)
            {
                DisplayNextPose();
            }
            
            Debug.Log($"Exercise direction toggled to: {(_isAscendingSequence ? "Ascending" : "Descending")}");
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(NextPoseVisualizer))]
    public class NextPoseVisualizerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            NextPoseVisualizer visualizer = (NextPoseVisualizer)target;

            EditorGUILayout.Space();

            if (GUILayout.Button("Show Next Pose"))
            {
                if (Application.isPlaying)
                {
                    visualizer.ShowNextPose();
                }
                else
                {
                    Debug.LogWarning("Cannot display poses in edit mode");
                }
            }

            if (GUILayout.Button("Hide Next Pose"))
            {
                if (Application.isPlaying)
                {
                    visualizer.HideNextPose();
                }
                else
                {
                    Debug.LogWarning("Cannot hide poses in edit mode");
                }
            }

            if (GUILayout.Button("Toggle Direction"))
            {
                if (Application.isPlaying)
                {
                    visualizer.ToggleDirection();
                }
                else
                {
                    Debug.LogWarning("Cannot toggle direction in edit mode");
                }
            }
        }
    }
#endif
}
