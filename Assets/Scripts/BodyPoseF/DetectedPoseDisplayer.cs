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
    /// Displays the pose that the user just correctly performed by applying it to a skeleton
    /// Can be used alongside or instead of PoseSequencePlayer
    /// </summary>
    [RequireComponent(typeof(OVRBodyPoseSkeletonProvider))]
    public class DetectedPoseDisplayer : MonoBehaviour
    {
        [Tooltip("Reference to the BodyPoseExerciseTracker component")]
        [SerializeField] private BodyPoseExerciseTracker exerciseTracker;

        [Tooltip("The skeleton on which to display the detected pose")]
        [SerializeField] private OVRSkeleton displaySkeleton;

        [Tooltip("How long to display the pose after detection (in seconds)")]
        [SerializeField] private float displayDuration = 3.0f;

        [Tooltip("Whether to fade out the display after the duration")]
        [SerializeField] private bool fadeOut = true;

        [Tooltip("Duration of the fade out animation")]
        [SerializeField] private float fadeDuration = 1.0f;

        private OVRBodyPoseSkeletonProvider _skeletonProvider;
        private bool _isDisplayingPose = false;
        private Coroutine _displayCoroutine;
        private List<IBodyPose> _loadedPoses = new List<IBodyPose>();
        private int _lastDisplayedPoseIndex = -1;

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
                    Debug.LogWarning("No OVRSkeleton found. Pose display will not be visible.");
                }
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
            }

            // Load the poses from the exercise tracker
            LoadPoses();
        }

        private void OnDestroy()
        {
            // Unsubscribe from the event
            if (exerciseTracker != null)
            {
                exerciseTracker.GetType()
                    .GetField("_onPoseDetected", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(exerciseTracker)
                    ?.GetType()
                    .GetMethod("RemoveListener")
                    ?.Invoke(exerciseTracker.GetType().GetField("_onPoseDetected", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(exerciseTracker),
                        new object[] { new UnityEngine.Events.UnityAction<int, float>(OnPoseDetected) });
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
                    Debug.Log($"Loaded {_loadedPoses.Count} poses from exercise tracker");
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

            // If the pose index is valid, display it
            if (poseIndex >= 0 && poseIndex < _loadedPoses.Count)
            {
                DisplayPose(poseIndex);
            }
        }

        /// <summary>
        /// Displays the specified pose on the skeleton
        /// </summary>
        public void DisplayPose(int poseIndex)
        {
            if (poseIndex < 0 || poseIndex >= _loadedPoses.Count)
            {
                Debug.LogError($"Invalid pose index: {poseIndex}. Valid range: 0-{_loadedPoses.Count - 1}");
                return;
            }

            // Stop any existing display coroutine
            if (_displayCoroutine != null)
            {
                StopCoroutine(_displayCoroutine);
                _displayCoroutine = null;
            }

            // Get the pose
            IBodyPose pose = _loadedPoses[poseIndex];

            // Display the pose
            if (pose != null && displaySkeleton != null && _skeletonProvider != null)
            {
                // Check if pose is a BodyPose (Unity ScriptableObject)
                BodyPose bodyPose = pose as BodyPose;
                if (bodyPose == null)
                {
                    Debug.LogError("The pose is not a BodyPose ScriptableObject");
                    return;
                }

                // Apply the pose
                ApplyPose(bodyPose);

                // Store the last displayed pose index
                _lastDisplayedPoseIndex = poseIndex;

                // Start the display coroutine
                _displayCoroutine = StartCoroutine(DisplayPoseCoroutine());

                Debug.Log($"Displaying pose {poseIndex}");
            }
        }

        /// <summary>
        /// Coroutine to manage the display duration and fade out
        /// </summary>
        private IEnumerator DisplayPoseCoroutine()
        {
            _isDisplayingPose = true;

            // Make sure the skeleton is fully visible
            SetSkeletonVisibility(1.0f);

            // Wait for the display duration
            yield return new WaitForSeconds(displayDuration);

            // Fade out if needed
            if (fadeOut)
            {
                float elapsedTime = 0;
                while (elapsedTime < fadeDuration)
                {
                    float alpha = 1.0f - (elapsedTime / fadeDuration);
                    SetSkeletonVisibility(alpha);

                    elapsedTime += Time.deltaTime;
                    yield return null;
                }

                // Ensure it's fully invisible at the end
                SetSkeletonVisibility(0);
            }

            _isDisplayingPose = false;
            _displayCoroutine = null;
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
        /// Manually display the last detected pose again
        /// </summary>
        public void RedisplayLastPose()
        {
            if (_lastDisplayedPoseIndex >= 0)
            {
                DisplayPose(_lastDisplayedPoseIndex);
            }
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(DetectedPoseDisplayer))]
    public class DetectedPoseDisplayerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            DetectedPoseDisplayer displayer = (DetectedPoseDisplayer)target;

            EditorGUILayout.Space();

            if (GUILayout.Button("Redisplay Last Pose"))
            {
                if (Application.isPlaying)
                {
                    displayer.RedisplayLastPose();
                }
                else
                {
                    Debug.LogWarning("Cannot display poses in edit mode");
                }
            }
        }
    }
#endif
}
