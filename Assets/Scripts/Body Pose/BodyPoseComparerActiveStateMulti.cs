using System;
using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;
using Oculus.Interaction.Body.Input;
using Oculus.Interaction.Body.PoseDetection;

namespace Body_Pose
{
    /// <summary>
    /// Compares a user-provided set of joints between a source Body Pose and multiple reference poses.
    /// You can select which joints to monitor and what the maximum angle delta between each joint should be.
    /// If all joints are within this maximum range for ANY of the reference poses, the IActiveState becomes Active.
    /// </summary>
    public sealed class BodyPoseComparerActiveStateMulti :
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
            [Tooltip("The joint to compare from each Body Pose")]
            public BodyJointId Joint = BodyJointId.Body_Head;

            [Min(0)]
            [Tooltip("The maximum angle that two joint rotations " +
                "can be from each other to be considered equal.")]
            public float MaxDelta = 30f;

            [Tooltip("The width of the threshold when transitioning " +
                "states. " + nameof(Width) + " / 2 is added to " +
                nameof(MaxDelta) + " when leaving Active state, and " +
                "subtracted when entering.")]
            [Min(0)]
            public float Width = 4f;
        }

        /// <summary>
        /// The source body pose to compare against reference poses.
        /// </summary>
        [Tooltip("The source body pose to compare against reference poses.")]
        [SerializeField, Interface(typeof(IBodyPose))]
        private UnityEngine.Object _sourcePose;
        private IBodyPose SourcePose;

        /// <summary>
        /// The reference poses to compare against the source pose.
        /// </summary>
        [Tooltip("The reference poses to compare against the source pose.")]
        [SerializeField, Interface(typeof(IBodyPose))]
        private List<UnityEngine.Object> _referencePoses = new List<UnityEngine.Object>();
        private List<IBodyPose> ReferencePoses = new List<IBodyPose>();

        /// <summary>
        /// A list of JointComparerConfigs which contains the parameters to test.
        /// </summary>
        [SerializeField]
        private List<JointComparerConfig> _configs =
            new List<JointComparerConfig>()
            {
                new JointComparerConfig()
            };

        /// <summary>
        /// A new state must be maintaned for at least this many seconds before the Active property changes.
        /// Prevents unwanted momentary activations/deactivations of the IActiveState.
        /// </summary>
        [Tooltip("A new state must be maintaned for at least this " +
            "many seconds before the Active property changes.")]
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

                foreach (var referencePose in ReferencePoses)
                {
                    bool poseMatches = true;
                        Debug.Log("Pose Found");
                    
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