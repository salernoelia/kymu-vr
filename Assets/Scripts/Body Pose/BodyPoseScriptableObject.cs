using System;
using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction.Body.Input;
using Oculus.Interaction.Body.PoseDetection;
using Oculus.Interaction.Collections;
using UnityEngine;

namespace Body_Pose
{
    [CreateAssetMenu(fileName = "BodyPose", menuName = "MoveFast/Body Pose", order = 1)]
    public class BodyPose : ScriptableObject, IBodyPose
    {
        [SerializeField] private int _serializedVersion = 1;
        [SerializeField] private List<JointData> _jointData = new List<JointData>();
    
   
        [SerializeField, HideInInspector] 
        private SimpleSkeletonMapping _skeletonMapping = new SimpleSkeletonMapping();
    

        public event Action WhenBodyPoseUpdated = delegate { };
        public ISkeletonMapping SkeletonMapping => _skeletonMapping;

        [Serializable]
        public class JointData
        {
            public string JointId;
            public SerializablePose LocalPose;
            public SerializablePose RootPose;
        }

        [Serializable]
        public class SerializablePose
        {
            public SerializableVector3 Position;
            public SerializableQuaternion Rotation;

            public SerializablePose(Vector3 position, Quaternion rotation)
            {
                Position = new SerializableVector3(position);
                Rotation = new SerializableQuaternion(rotation);
            }

            public SerializablePose() 
            {
                Position = new SerializableVector3(Vector3.zero);
                Rotation = new SerializableQuaternion(Quaternion.identity);
            }

            public Pose ToPose()
            {
                return new Pose(
                    Position.ToVector3(), 
                    Rotation.ToQuaternion());
            }
        }

        [Serializable]
        public class SerializableVector3
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

            public Vector3 ToVector3()
            {
                return new Vector3(x, y, z);
            }
        }

        [Serializable]
        public class SerializableQuaternion
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

            public Quaternion ToQuaternion()
            {
                return new Quaternion(x, y, z, w);
            }
        }


        public void SetPose(Dictionary<BodyJointId, Pose> localPoses, Dictionary<BodyJointId, Pose> rootPoses)
        {
            _jointData.Clear();
        
      
            UpdateSkeletonMapping(localPoses.Keys);

            foreach (var kvp in localPoses)
            {
                JointData data = new JointData
                {
                    JointId = kvp.Key.ToString(),
                    LocalPose = new SerializablePose(kvp.Value.position, kvp.Value.rotation)
                };

                if (rootPoses.TryGetValue(kvp.Key, out Pose rootPose))
                {
                    data.RootPose = new SerializablePose(rootPose.position, rootPose.rotation);
                }
                else
                {
                    data.RootPose = new SerializablePose();
                }

                _jointData.Add(data);
            }
        }


        public bool GetJointPoseLocal(BodyJointId bodyJointId, out Pose pose)
        {
            string jointIdStr = bodyJointId.ToString();
            foreach (var data in _jointData)
            {
                if (data.JointId == jointIdStr && data.LocalPose != null)
                {
                    pose = data.LocalPose.ToPose();
                    return true;
                }
            }
        
            pose = new Pose();
            return false;
        }

        public bool GetJointPoseFromRoot(BodyJointId bodyJointId, out Pose pose)
        {
            string jointIdStr = bodyJointId.ToString();
            foreach (var data in _jointData)
            {
                if (data.JointId == jointIdStr && data.RootPose != null)
                {
                    pose = data.RootPose.ToPose();
                    return true;
                }
            }
        
            pose = new Pose();
            return false;
        }
    

        private void UpdateSkeletonMapping(IEnumerable<BodyJointId> joints)
        {
            _skeletonMapping.Initialize(joints);
        }


        [Serializable]
        private class SimpleSkeletonMapping : ISkeletonMapping
        {
            [SerializeField] private List<BodyJointId> _jointIds = new List<BodyJointId>();
            private EnumerableHashSet<BodyJointId> _jointsHashSet = new EnumerableHashSet<BodyJointId>();
        
            public IEnumerableHashSet<BodyJointId> Joints => _jointsHashSet;
        
            public void Initialize(IEnumerable<BodyJointId> joints)
            {
                _jointIds.Clear();
                _jointsHashSet.Clear();
            
                foreach (var joint in joints)
                {
                    _jointIds.Add(joint);
                    _jointsHashSet.Add(joint);
                }
            }
        
            public bool TryGetParentJointId(BodyJointId jointId, out BodyJointId parent)
            {

                parent = BodyJointId.Invalid;
                return false;
            }
        }
    

        [Serializable]
        private class EnumerableHashSet<T> : IEnumerableHashSet<T>
        {
            private HashSet<T> _hashSet = new HashSet<T>();
        
            public int Count => _hashSet.Count;
        
      
            HashSet<T>.Enumerator IEnumerableHashSet<T>.GetEnumerator() => _hashSet.GetEnumerator();
        
   
            IEnumerator<T> IEnumerable<T>.GetEnumerator() => _hashSet.GetEnumerator();
        
   
            IEnumerator IEnumerable.GetEnumerator() => _hashSet.GetEnumerator();
        
            public void Add(T item) => _hashSet.Add(item);
        
            public void Clear() => _hashSet.Clear();
        
            public bool Contains(T item) => _hashSet.Contains(item);
        
            public bool IsProperSubsetOf(IEnumerable<T> other) => _hashSet.IsProperSubsetOf(other);
        
            public bool IsProperSupersetOf(IEnumerable<T> other) => _hashSet.IsProperSupersetOf(other);
        
            public bool IsSubsetOf(IEnumerable<T> other) => _hashSet.IsSubsetOf(other);
        
            public bool IsSupersetOf(IEnumerable<T> other) => _hashSet.IsSupersetOf(other);
        
            public bool Overlaps(IEnumerable<T> other) => _hashSet.Overlaps(other);
        
            public bool SetEquals(IEnumerable<T> other) => _hashSet.SetEquals(other);
        }
    }
}