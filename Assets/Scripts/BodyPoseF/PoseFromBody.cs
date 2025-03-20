/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * Licensed under the Oculus SDK License Agreement (the "License");
 * you may not use the Oculus SDK except in compliance with the License,
 * which is provided at the time of installation or download, or which
 * otherwise accompanies this software in either electronic or hard copy form.
 *
 * You may obtain a copy of the License at
 *
 * https://developer.oculus.com/licenses/oculussdk/
 *
 * Unless required by applicable law or agreed to in writing, the Oculus SDK
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Body.Input;
using Oculus.Interaction.Body.PoseDetection;
using UnityEngine;

namespace BodyPoseF
{
    /// <summary>
    /// Exposes an <see cref="IBodyPose"/> from an <see cref="IBody"/>
    /// </summary>
    public class PoseFromBody : MonoBehaviour, IBodyPose
    {
        public event Action WhenBodyPoseUpdated = delegate { };

        [Tooltip("The IBodyPose will be derived from this IBody.")]
        [SerializeField, Interface(typeof(IBody))]
        private UnityEngine.Object _body;
        private IBody Body;

        [Tooltip("If true, this component will track the provided IBody as " +
            "its data is updated. If false, you must call " +
            nameof(UpdatePose) + " to update joint data.")]
        [SerializeField]
        private bool _autoUpdate = true;

        /// <summary>
        /// If true, this component will track the provided IBody as
        /// its data is updated. If false, you must call
        /// <see cref="UpdatePose"/> to update joint data.
        /// </summary>
        public bool AutoUpdate
        {
            get => _autoUpdate;
            set => _autoUpdate = value;
        }

        protected bool _started = false;

        private Dictionary<BodyJointId, Pose> _jointPosesLocal;
        private Dictionary<BodyJointId, Pose> _jointPosesFromRoot;

        public ISkeletonMapping SkeletonMapping => Body.SkeletonMapping;

        public bool GetJointPoseLocal(BodyJointId bodyJointId, out Pose pose) =>
            _jointPosesLocal.TryGetValue(bodyJointId, out pose);

        public bool GetJointPoseFromRoot(BodyJointId bodyJointId, out Pose pose)
        {
            pose = new Pose();
            return _jointPosesFromRoot != null && _jointPosesFromRoot.TryGetValue(bodyJointId, out pose);
        }

        protected virtual void Awake()
        {
            _jointPosesLocal = new Dictionary<BodyJointId, Pose>();
            _jointPosesFromRoot = new Dictionary<BodyJointId, Pose>();
            Body = _body as IBody;
        }

        protected virtual void Start()
        {
            this.BeginStart(ref _started);
            this.AssertField(Body, nameof(Body));
            this.EndStart(ref _started);
        }

        protected virtual void OnEnable()
        {
            if (_started)
            {
                Body.WhenBodyUpdated += Body_WhenBodyUpdated;
            }
        }

        protected virtual void OnDisable()
        {
            if (_started)
            {
                Body.WhenBodyUpdated -= Body_WhenBodyUpdated;
            }
        }

        private void Body_WhenBodyUpdated()
        {
            if (_autoUpdate)
            {
                UpdatePose();
            }
        }

     public void UpdatePose()
        {
            _jointPosesLocal.Clear();
            _jointPosesFromRoot.Clear();

            foreach (var joint in Body.SkeletonMapping.Joints)
            {
                if (Body.GetJointPoseLocal(joint,
                    out Pose localPose))
                {
                    _jointPosesLocal[joint] = localPose;
                }
                if (Body.GetJointPoseFromRoot(joint,
                    out Pose poseFromRoot))
                {
                    _jointPosesFromRoot[joint] = poseFromRoot;
                }
            }

            WhenBodyPoseUpdated.Invoke();
        }

    
[SerializeField]
public void SavePoseToFile()
{
#if UNITY_EDITOR

    UpdatePose();
    

    string timestamp = System.DateTime.Now.ToString("yyyyMMdd-HHmmss");
    string assetPath = $"Assets/BodyPoses/BodyPose-{timestamp}.asset";
    
  
    string directory = System.IO.Path.GetDirectoryName(assetPath);
    if (!UnityEditor.AssetDatabase.IsValidFolder(directory))
    {
        string parentFolder = "Assets";
        string folderName = "BodyPoses";
        UnityEditor.AssetDatabase.CreateFolder(parentFolder, folderName);
    }
    
 
    BodyPose bodyPose = UnityEngine.ScriptableObject.CreateInstance<BodyPose>();
    

    bodyPose.SetPose(_jointPosesLocal, _jointPosesFromRoot);
    

    UnityEditor.AssetDatabase.CreateAsset(bodyPose, assetPath);
    UnityEditor.AssetDatabase.SaveAssets();
    UnityEditor.AssetDatabase.Refresh();
    
    Debug.Log($"Body pose saved successfully to {assetPath}");
#else
    Debug.LogWarning("SavePoseToFile is only available in the Unity Editor.");
#endif
}

[SerializeField]
public void CaptureAndSavePose()
{
#if UNITY_EDITOR

    UpdatePose();
    
    SavePoseToFile();
#else
    Debug.LogWarning("CaptureAndSavePose is only available in the Unity Editor.");
#endif
}
        
        [Serializable]
        private class PoseData
        {
            public Dictionary<string, SerializablePose> LocalPoses;
            public Dictionary<string, SerializablePose> RootPoses;
            public string TimeStamp;
        }

        [Serializable]
        private class SerializablePose
        {
            public SerializableVector3 Position;
            public SerializableQuaternion Rotation;

            public SerializablePose(Pose pose)
            {
                Position = new SerializableVector3(pose.position);
                Rotation = new SerializableQuaternion(pose.rotation);
            }
        }

        [Serializable]
        private class SerializableVector3
        {
            public float x;
            public float y;
            public float z;

            public SerializableVector3(Vector3 vector)
            {
                x = vector.x;
                y = vector.y;
                z = vector.z;
            }
        }

        [Serializable]
        private class SerializableQuaternion
        {
            public float x;
            public float y;
            public float z;
            public float w;

            public SerializableQuaternion(Quaternion quaternion)
            {
                x = quaternion.x;
                y = quaternion.y;
                z = quaternion.z;
                w = quaternion.w;
            }
        }

        #region Inject

        public void InjectAllPoseFromBody(IBody body)
        {
            InjectBody(body);
        }

        public void InjectBody(IBody body)
        {
            _body = body as UnityEngine.Object;
            Body = body;
        }

        #endregion
    }
}
