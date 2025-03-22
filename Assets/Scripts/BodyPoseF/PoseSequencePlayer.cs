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
    /// Component for playing back a sequence of body poses stored in a folder
    /// </summary>
    [RequireComponent(typeof(OVRBodyPoseSkeletonProvider))]
    public class PoseSequencePlayer : MonoBehaviour
    {
        [SerializeField]
        private string sequenceFolderPath = "Assets/BodyPoses";

        [SerializeField]
        private bool playOnStart = false;

        [SerializeField]
        private bool loopPlayback = false;

        [SerializeField]
        private float playbackSpeed = 1.0f;

        [Tooltip("If empty, will use the last recorded sequence")]
        [SerializeField]
        private string specificSequenceFolderName = "";

        [SerializeField]
        private OVRSkeleton targetSkeleton;

        private OVRBodyPoseSkeletonProvider _skeletonProvider;
        private List<BodyPose> _poseSequence = new List<BodyPose>();
        private bool _isPlaying = false;
        private int _currentPoseIndex = 0;
        private float _defaultInterval = 0.2f; // Same as capture interval
        private Coroutine _playbackCoroutine;

        protected virtual void Awake()
        {
            _skeletonProvider = GetComponent<OVRBodyPoseSkeletonProvider>();

            // Try to find the skeleton if not assigned
            if (targetSkeleton == null)
            {
                // Try to find skeleton in children or parent
                targetSkeleton = GetComponentInChildren<OVRSkeleton>(true);
                if (targetSkeleton == null)
                {
                    targetSkeleton = GetComponentInParent<OVRSkeleton>(true);
                }

                // If still not found, find any in scene
                if (targetSkeleton == null)
                {
                    targetSkeleton = FindObjectOfType<OVRSkeleton>();
                }

                if (targetSkeleton != null)
                {
                    Debug.Log($"Found OVRSkeleton on {targetSkeleton.gameObject.name}");
                }
                else
                {
                    Debug.LogWarning("No OVRSkeleton found. Playback may not be visible.");
                }
            }
        }

        protected virtual void Start()
        {
            if (playOnStart)
            {
                PlayPoseSequence();
            }
        }

        /// <summary>
        /// Loads and plays the pose sequence
        /// </summary>
        public void PlayPoseSequence()
        {
            if (_isPlaying)
            {
                StopPlayback();
            }

            // Load the sequence if not already loaded or if specific sequence is requested
            if (_poseSequence.Count == 0 || !string.IsNullOrEmpty(specificSequenceFolderName))
            {
                LoadPoseSequence();
            }

            if (_poseSequence.Count > 0)
            {
                _playbackCoroutine = StartCoroutine(PlaySequenceCoroutine());
            }
            else
            {
                Debug.LogWarning("No pose sequence found to play.");
            }
        }

        /// <summary>
        /// Stops the current playback
        /// </summary>
        public void StopPlayback()
        {
            if (_playbackCoroutine != null)
            {
                StopCoroutine(_playbackCoroutine);
                _playbackCoroutine = null;
            }
            _isPlaying = false;
        }

        /// <summary>
        /// Toggles looping mode on/off
        /// </summary>
        public void ToggleLooping()
        {
            loopPlayback = !loopPlayback;
            Debug.Log($"Looping {(loopPlayback ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Sets the playback speed
        /// </summary>
        public void SetPlaybackSpeed(float speed)
        {
            playbackSpeed = Mathf.Max(0.1f, speed);
            Debug.Log($"Playback speed set to {playbackSpeed}x");
        }

#if UNITY_EDITOR
        /// <summary>
        /// Loads a sequence of poses from the specified folder
        /// </summary>
        private void LoadPoseSequence()
        {
            _poseSequence.Clear();
            _currentPoseIndex = 0;
            string sequenceFolder;

            // Determine which folder to use
            if (!string.IsNullOrEmpty(specificSequenceFolderName))
            {
                sequenceFolder = Path.Combine(sequenceFolderPath, specificSequenceFolderName);
            }
            else
            {
                // Find the most recent sequence folder
                var directories = AssetDatabase.GetSubFolders(sequenceFolderPath);
                if (directories == null || directories.Length == 0)
                {
                    Debug.LogError($"No pose sequence folders found in {sequenceFolderPath}");
                    return;
                }

                // Sort by directory name (which contains timestamp) to get the most recent
                sequenceFolder = directories
                    .Where(d => Path.GetFileName(d).StartsWith("PoseSequence-"))
                    .OrderByDescending(d => d)
                    .FirstOrDefault();

                if (string.IsNullOrEmpty(sequenceFolder))
                {
                    Debug.LogError("No valid pose sequence folders found");
                    return;
                }
            }

            // Load all pose assets in the folder
            string[] assetGuids = AssetDatabase.FindAssets("t:BodyPose", new[] { sequenceFolder });

            // Process found assets
            foreach (var guid in assetGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                BodyPose pose = AssetDatabase.LoadAssetAtPath<BodyPose>(assetPath);

                if (pose != null)
                {
                    _poseSequence.Add(pose);
                }
            }

            // Sort poses by filename (which should have numeric indices)
            _poseSequence = _poseSequence
                .OrderBy(p => AssetDatabase.GetAssetPath(p))
                .ToList();

            Debug.Log($"Loaded {_poseSequence.Count} poses from {Path.GetFileName(sequenceFolder)}");
        }
#endif

        /// <summary>
        /// Coroutine for playing back the sequence of poses
        /// </summary>
        private IEnumerator PlaySequenceCoroutine()
        {
            if (_poseSequence.Count == 0)
            {
                Debug.LogWarning("Attempting to play empty pose sequence");
                yield break;
            }

            _isPlaying = true;
            _currentPoseIndex = 0;
            float interval = _defaultInterval / playbackSpeed;

            Debug.Log($"Starting playback of {_poseSequence.Count} poses");

            do
            {
                // Reset index for looping
                if (_currentPoseIndex >= _poseSequence.Count)
                {
                    _currentPoseIndex = 0;
                    if (!loopPlayback)
                    {
                        _isPlaying = false;
                        break;
                    }
                }

                // Apply the current pose
                ApplyPose(_poseSequence[_currentPoseIndex]);

                // Wait for the interval
                yield return new WaitForSeconds(interval);

                // Advance to next pose
                _currentPoseIndex++;

            } while (_isPlaying);

            Debug.Log("Pose sequence playback complete");
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
                    if (targetSkeleton != null)
                    {
                        // Use reflection to force the skeleton to update
                        ForceSkeletonUpdate(targetSkeleton);
                    }

                    Debug.Log($"Applied pose {_currentPoseIndex} to skeleton provider");
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
        /// Editor method to list available pose sequences
        /// </summary>
        public void ListAvailableSequences()
        {
#if UNITY_EDITOR
            var directories = AssetDatabase.GetSubFolders(sequenceFolderPath)
                .Where(d => Path.GetFileName(d).StartsWith("PoseSequence-"))
                .OrderByDescending(d => d)
                .ToList();

            Debug.Log($"Available pose sequences ({directories.Count}):");
            foreach (var dir in directories)
            {
                string folderName = Path.GetFileName(dir);
                int poseCount = AssetDatabase.FindAssets("t:BodyPose", new[] { dir }).Length;
                Debug.Log($"- {folderName} ({poseCount} poses)");
            }
#endif
        }

        /// <summary>
        /// Set a specific sequence to play by folder name
        /// </summary>
        public void SetSequenceFolder(string folderName)
        {
            specificSequenceFolderName = folderName;
            _poseSequence.Clear(); // Clear cached sequence to force reload
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(PoseSequencePlayer))]
    public class PoseSequencePlayerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PoseSequencePlayer player = (PoseSequencePlayer)target;

            EditorGUILayout.Space();

            if (GUILayout.Button("Play Sequence"))
            {
                player.PlayPoseSequence();
            }

            if (GUILayout.Button("Stop Playback"))
            {
                player.StopPlayback();
            }

            if (GUILayout.Button("Toggle Looping"))
            {
                player.ToggleLooping();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("List Available Sequences"))
            {
                player.ListAvailableSequences();
            }
        }
    }
#endif
}

