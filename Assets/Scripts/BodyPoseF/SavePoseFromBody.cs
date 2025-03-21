using System;
using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Body.Input;
using Oculus.Interaction.Body.PoseDetection;
using UnityEngine;

namespace BodyPoseF
{
    /// <summary>
    /// Extends PoseFromBody to add functionality for saving body poses to ScriptableObjects
    /// </summary>
    [RequireComponent(typeof(PoseFromBody))]
    public class SavePoseFromBody : MonoBehaviour
    {
        private PoseFromBody _poseFromBody;

        protected virtual void Awake()
        {
            _poseFromBody = GetComponent<PoseFromBody>();
        }

        /// <summary>
        /// Saves the current body pose to a ScriptableObject asset
        /// </summary>
        public void SavePoseToFile()
        {
#if UNITY_EDITOR
            // Make sure pose is up-to-date
            _poseFromBody.UpdatePose();

            // Generate filename with timestamp
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string assetPath = $"Assets/BodyPoses/BodyPose-{timestamp}.asset";

            // Ensure directory exists
            string directory = System.IO.Path.GetDirectoryName(assetPath);
            if (!UnityEditor.AssetDatabase.IsValidFolder(directory))
            {
                string parentFolder = "Assets";
                string folderName = "BodyPoses";
                UnityEditor.AssetDatabase.CreateFolder(parentFolder, folderName);
            }

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
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();

            Debug.Log($"Body pose saved successfully to {assetPath}");
#else
            Debug.LogWarning("SavePoseToFile is only available in the Unity Editor.");
#endif
        }

        /// <summary>
        /// Ensures pose is up-to-date and saves it to a file
        /// </summary>
        public void CaptureAndSavePose()
        {
#if UNITY_EDITOR
            SavePoseToFile();
#else
            Debug.LogWarning("CaptureAndSavePose is only available in the Unity Editor.");
#endif
        }
    }
}