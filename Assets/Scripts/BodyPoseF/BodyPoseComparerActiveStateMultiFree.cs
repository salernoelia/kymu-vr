using System;
using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Body.Input;
using Oculus.Interaction.Body.PoseDetection;
using UnityEngine;

namespace BodyPoseF
{
    public sealed class BodyPoseComparerActiveStateMultiFree :
        MonoBehaviour, IActiveState, ITimeConsumer
    {
        public struct BodyPoseComparerFeatureState
        {
            public readonly float Delta;
            public readonly float MaxDelta;

            public BodyPoseComparerFeatureState(float delta, float maxDelta)
            {
                Delta = delta;
                MaxDelta = maxDelta;
            }
        }

        [Serializable]
        public class JointComparerConfig
        {
            public BodyJointId Joint;
            [Min(0)] public float MaxDelta;
            [Min(0)] public float Width;

            public JointComparerConfig(BodyJointId joint, float maxDelta, float width)
            {
                Joint = joint;
                MaxDelta = maxDelta;
                Width = width;
            }
        }

        [Tooltip("The source body pose to compare against reference poses.")]
        [SerializeField, Interface(typeof(IBodyPose))]
        private UnityEngine.Object _sourcePose;
        private IBodyPose SourcePose;

        [Tooltip("The reference poses to compare against the source pose.")]
        [SerializeField, Interface(typeof(IBodyPose))]
        private List<UnityEngine.Object> _referencePoses = new List<UnityEngine.Object>();
        private List<IBodyPose> ReferencePoses = new List<IBodyPose>();

        [SerializeField]
        private List<JointComparerConfig> _configs = new List<JointComparerConfig>
        {
            new JointComparerConfig(BodyJointId.Body_Head, 30f, 4f),
            new JointComparerConfig(BodyJointId.Body_LeftArmUpper, 30f, 4f),
            new JointComparerConfig(BodyJointId.Body_LeftArmLower, 30f, 4f),
            new JointComparerConfig(BodyJointId.Body_LeftHandWrist, 36f, 4f),
            new JointComparerConfig(BodyJointId.Body_RightArmUpper, 30f, 4f),
            new JointComparerConfig(BodyJointId.Body_RightArmLower, 30f, 4f),
            new JointComparerConfig(BodyJointId.Body_RightHandWrist, 36f, 4f)
        };

        [Tooltip("A new state must be maintaned for at least this many seconds before the Active property changes.")]
        [SerializeField]
        private float _minTimeInState = 0.05f;

        public float MinTimeInState
        {
            get => _minTimeInState;
            set => _minTimeInState = value;
        }

        private Func<float> _timeProvider = () => Time.time;
        public void SetTimeProvider(Func<float> timeProvider)
        {
            _timeProvider = timeProvider;
        }

        public IReadOnlyDictionary<JointComparerConfig, BodyPoseComparerFeatureState> FeatureStates =>
            _featureStates;

        private Dictionary<JointComparerConfig, BodyPoseComparerFeatureState> _featureStates =
            new Dictionary<JointComparerConfig, BodyPoseComparerFeatureState>();

        private bool _isActive;
        private bool _internalActive;
        private float _lastStateChangeTime;

        // New variable to track the detected pose index
        private int _detectedPoseIndex;

        // Public property to get the detected pose index
        public int DetectedPoseIndex => _detectedPoseIndex;

        private void Awake()
        {
            SourcePose = _sourcePose as IBodyPose;
            ReferencePoses.Clear();
            foreach (var pose in _referencePoses)
            {
                ReferencePoses.Add(pose as IBodyPose);
            }
        }

        private void Start()
        {
            this.AssertField(SourcePose, nameof(SourcePose));
            this.AssertCollectionField(ReferencePoses, nameof(ReferencePoses),
        "At least one reference pose must be provided");

        }

        public bool Active
        {
            get
            {
                if (!isActiveAndEnabled)
                {
                    return false;
                }

                bool wasActive = _internalActive;
                _internalActive = false;
                _detectedPoseIndex = 0; // Reset to 0 when no pose is detected

                for (int i = 0; i < ReferencePoses.Count; i++)
                {
                    var referencePose = ReferencePoses[i];
                    bool poseMatches = true;

                    foreach (var config in _configs)
                    {
                        float maxDelta = wasActive ?
                                        config.MaxDelta + config.Width / 2f :
                                        config.MaxDelta - config.Width / 2f;

                        bool withinDelta = GetJointDelta(SourcePose, referencePose, config.Joint, out float delta) &&
                                          Mathf.Abs(delta) <= maxDelta;

                        _featureStates[config] = new BodyPoseComparerFeatureState(delta, maxDelta);
                        poseMatches &= withinDelta;
                    }

                    if (poseMatches)
                    {
                        _internalActive = true;
                        _detectedPoseIndex = i + 1; // Set to the index of the detected pose + 1
                        break;
                    }
                }

                float time = _timeProvider();
                if (wasActive != _internalActive)
                {
                    _lastStateChangeTime = time;
                }
                if (time - _lastStateChangeTime >= _minTimeInState)
                {
                    _isActive = _internalActive;
                }
                return _isActive;
            }
        }

        private bool GetJointDelta(IBodyPose sourcepose, IBodyPose referencePose, BodyJointId joint, out float delta)
        {
            if (!sourcepose.GetJointPoseLocal(joint, out Pose localSource) ||
                !referencePose.GetJointPoseLocal(joint, out Pose localRef))
            {
                delta = 0;
                return false;
            }

            delta = Quaternion.Angle(localSource.rotation, localRef.rotation);

            return true;
        }

        #region Inject
        public void InjectAllBodyPoseComparerActiveStateMulti(
            IBodyPose sourcePose, IEnumerable<IBodyPose> referencePoses,
            IEnumerable<JointComparerConfig> configs)
        {
            InjectSourcePose(sourcePose);
            InjectReferencePoses(referencePoses);
            InjectJoints(configs);
        }

        public void InjectSourcePose(IBodyPose sourcePose)
        {
            _sourcePose = sourcePose as UnityEngine.Object;
            SourcePose = sourcePose;
        }

        public void InjectReferencePoses(IEnumerable<IBodyPose> referencePoses)
        {
            _referencePoses = new List<UnityEngine.Object>();
            ReferencePoses = new List<IBodyPose>();

            foreach (var pose in referencePoses)
            {
                _referencePoses.Add(pose as UnityEngine.Object);
                ReferencePoses.Add(pose);
            }
        }

        public void InjectJoints(IEnumerable<JointComparerConfig> configs)
        {
            _configs = new List<JointComparerConfig>(configs);
        }

        #endregion
    }
}
