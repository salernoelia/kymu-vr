using System;
using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Body.Input;
using Oculus.Interaction.Body.PoseDetection;
using UnityEngine;

namespace BodyPoseF
{
    public class LockedBodyPose : MonoBehaviour, IBodyPose
    {
        private static readonly Pose HIP_OFFSET = new Pose()
        {
            position = new Vector3(0f, 0.923987f, 0f),
            rotation = Quaternion.Euler(0, 270, 270),
        };

        public event Action WhenBodyPoseUpdated = delegate { };

        [Tooltip("The body pose to be locked")]
        [SerializeField, Interface(typeof(IBodyPose))]
        private UnityEngine.Object _pose;
        private IBodyPose Pose;

        [Tooltip("The body pose will be locked relative to this " +
            "joint at the specified offset.")]
        [SerializeField]
        private BodyJointId _referenceJoint = BodyJointId.Body_Hips;

        [Tooltip("The reference joint will be placed at " +
            "this offset from the root.")]
        [SerializeField]
        private Pose _referenceOffset = HIP_OFFSET;

        protected bool _started = false;

        private Dictionary<BodyJointId, Pose> _lockedPoses;

        public ISkeletonMapping SkeletonMapping => Pose != null ? Pose.SkeletonMapping : null;

        public bool GetJointPoseLocal(BodyJointId bodyJointId, out Pose pose)
        {
            if (Pose == null)
            {
                pose = default;
                return false;
            }
            return Pose.GetJointPoseLocal(bodyJointId, out pose);
        }

        public bool GetJointPoseFromRoot(BodyJointId bodyJointId, out Pose pose)
        {
            if (_lockedPoses == null)
            {
                pose = default;
                return false;
            }
            return _lockedPoses.TryGetValue(bodyJointId, out pose);
        }

        private void UpdateLockedBodyPose()
        {
            if (Pose == null)
            {
                return;
            }
            
            _lockedPoses.Clear();
            for (int i = 0; i < Constants.NUM_BODY_JOINTS; ++i)
            {
                BodyJointId jointId = (BodyJointId)i;
                if (Pose.GetJointPoseFromRoot(_referenceJoint, out Pose referencePose) &&
                    Pose.GetJointPoseFromRoot(jointId, out Pose jointPose))
                {
                    ref Pose offset = ref referencePose;
                    PoseUtils.Invert(ref offset);
                    PoseUtils.Multiply(offset, jointPose, ref jointPose);
                    PoseUtils.Multiply(_referenceOffset, jointPose, ref jointPose);
                    _lockedPoses[jointId] = jointPose;
                }
            }
            WhenBodyPoseUpdated.Invoke();
        }

        protected virtual void Awake()
        {
            _lockedPoses = new Dictionary<BodyJointId, Pose>();
            Pose = _pose as IBodyPose;
        }

        protected virtual void Start()
        {
            this.BeginStart(ref _started);
            this.AssertField(Pose, nameof(Pose));
            UpdateLockedBodyPose();
            this.EndStart(ref _started);
        }

        protected virtual void OnEnable()
        {
            if (_started && Pose != null)
            {
                Pose.WhenBodyPoseUpdated += UpdateLockedBodyPose;
            }
        }

        protected virtual void OnDisable()
        {
            if (_started && Pose != null)
            {
                Pose.WhenBodyPoseUpdated -= UpdateLockedBodyPose;
            }
        }
    }
}