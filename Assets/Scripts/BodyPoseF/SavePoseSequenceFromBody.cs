using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Oculus.Interaction;
using Oculus.Interaction.Body.Input;
using Oculus.Interaction.Body.PoseDetection;
using UnityEngine;

namespace BodyPoseF
{
    /// <summary>
    /// Extends PoseFromBody to add functionality for saving sequences of body poses to ScriptableObjects
    /// </summary>
    [RequireComponent(typeof(PoseFromBody))]
    public class SavePoseSequenceFromBody : MonoBehaviour
    {
        private PoseFromBody _poseFromBody;
        private bool _isCapturingSequence = false;
        private GameObject _timerTextObj;

        // Sequence capture parameters
        [SerializeField]
        public float SEQUENCE_DURATION = 3.0f; 

        [SerializeField]
        public float CAPTURE_INTERVAL = 0.2f;  

        [SerializeField]
        public int POSES_PER_SEQUENCE = 16;    

        protected virtual void Awake()
        {
            _poseFromBody = GetComponent<PoseFromBody>();
        }

        /// <summary>
        /// Starts capturing a sequence of poses over 3 seconds
        /// </summary>
        public void CaptureAndSavePoseSequence()
        {
#if UNITY_EDITOR
            if (!_isCapturingSequence)
            {
                StartCoroutine(CaptureSequence());
            }
            else
            {
                Debug.LogWarning("Already capturing a pose sequence.");
            }
#else
            Debug.LogWarning("CaptureAndSavePoseSequence is only available in the Unity Editor.");
#endif
        }

        private IEnumerator CaptureSequence()
        {
#if UNITY_EDITOR
            _isCapturingSequence = true;

            // Create timer text object
            CreateTimerDisplay();

            // Generate timestamp for folder name
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string sequenceFolderName = $"PoseSequence-{timestamp}";
            string sequenceFolderPath = $"Assets/BodyPoses/{sequenceFolderName}";

            // Create main directory if it doesn't exist
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/BodyPoses"))
            {
                UnityEditor.AssetDatabase.CreateFolder("Assets", "BodyPoses");
            }

            // Create sequence directory
            UnityEditor.AssetDatabase.CreateFolder("Assets/BodyPoses", sequenceFolderName);

            Debug.Log($"Starting pose sequence capture. Will capture {POSES_PER_SEQUENCE} poses over {SEQUENCE_DURATION} seconds.");

            int poseIndex = 0;
            float elapsedTime = 0f;
            float remainingTime = SEQUENCE_DURATION;

            // Capture the first pose immediately
            SavePoseToFile(sequenceFolderPath, poseIndex);
            poseIndex++;

            // Update timer display with initial value
            UpdateTimerDisplay(remainingTime);

            // Continue capturing at intervals
            while (elapsedTime < SEQUENCE_DURATION) // Changed <= to < to ensure we don't exceed poses
            {
                // Wait for next capture interval
                float startTime = Time.time;
                float intervalTimer = 0f;

                while (intervalTimer < CAPTURE_INTERVAL)
                {
                    intervalTimer = Time.time - startTime;
                    remainingTime = SEQUENCE_DURATION - elapsedTime - intervalTimer;

                    // Update timer display
                    UpdateTimerDisplay(remainingTime);

                    yield return null;
                }

                elapsedTime += CAPTURE_INTERVAL;

                // Capture and save pose
                SavePoseToFile(sequenceFolderPath, poseIndex);
                poseIndex++;
            }

            // Clean up timer display
            DestroyTimerDisplay();

            Debug.Log($"Pose sequence capture completed. {poseIndex} poses saved to {sequenceFolderPath}");
            _isCapturingSequence = false;
#endif
            yield break;
        }

        /// <summary>
        /// Creates a 3D text object to display the timer countdown
        /// </summary>
        private void CreateTimerDisplay()
        {
            // Create a new game object for the timer text
            _timerTextObj = new GameObject("TimerDisplay");

            // Add TextMesh component
            TextMesh textMesh = _timerTextObj.AddComponent<TextMesh>();
            textMesh.fontSize = 48;
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.color = Color.white;
            textMesh.text = "3.0";

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
                    textMesh.text = remainingTime.ToString("F1");

                    // Change color as time runs out
                    if (remainingTime < 1.0f)
                    {
                        textMesh.color = Color.red;
                    }
                    else if (remainingTime < 2.0f)
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
        /// Saves the current body pose to a ScriptableObject asset
        /// </summary>
        private void SavePoseToFile(string folderPath, int poseIndex)
        {
#if UNITY_EDITOR
            // Make sure pose is up-to-date
            _poseFromBody.UpdatePose();

            // Generate filename
            string assetPath = $"{folderPath}/BodyPose-{poseIndex:D2}.asset";

            // Create the ScriptableObject
            BodyPose bodyPose = UnityEngine.ScriptableObject.CreateInstance<BodyPose>();

            // Extract poses from PoseFromBody
            Dictionary<BodyJointId, Pose> localPoses = new Dictionary<BodyJointId, Pose>();
            Dictionary<BodyJointId, Pose> rootPoses = new Dictionary<BodyJointId, Pose>();

            foreach (var joint in _poseFromBody.SkeletonMapping.Joints)
            {
                if (_poseFromBody.GetJointPoseLocal(joint, out Pose localPose))
                {
                    localPoses[joint] = localPose;
                }

                if (_poseFromBody.GetJointPoseFromRoot(joint, out Pose rootPose))
                {
                    rootPoses[joint] = rootPose;
                }
            }

            // Store poses in the ScriptableObject
            bodyPose.SetPose(localPoses, rootPoses);

            // Save the asset
            UnityEditor.AssetDatabase.CreateAsset(bodyPose, assetPath);

            // Only save assets and refresh after the last pose to avoid performance issues
            if (poseIndex == POSES_PER_SEQUENCE - 1)
            {
                UnityEditor.AssetDatabase.SaveAssets();
                UnityEditor.AssetDatabase.Refresh();
            }

            Debug.Log($"Body pose {poseIndex} saved to {assetPath}");
#endif
        }
    }
}
