using UnityEngine;
using BodyPoseF;

/// <summary>
/// Plays audio feedback when poses are detected in the BodyPoseExerciseTracker
/// </summary>
public class PoseDetectionAudioFeedback : MonoBehaviour
{
    [Tooltip("Reference to the BodyPoseExerciseTracker component")]
    [SerializeField] private BodyPoseExerciseTracker exerciseTracker;

    [Tooltip("Audio source to play sounds")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Sound to play when a pose is detected")]
    [SerializeField] private AudioClip poseDetectedSound;

    [Tooltip("Different sounds for different poses (optional)")]
    [SerializeField] private AudioClip[] poseSpecificSounds;

    [Tooltip("Whether to use pose-specific sounds if available")]
    [SerializeField] private bool usePoseSpecificSounds = false;

    private void Start()
    {
        // Find components if not assigned
        if (exerciseTracker == null)
        {
            exerciseTracker = FindObjectOfType<BodyPoseExerciseTracker>();
            if (exerciseTracker == null)
            {
                Debug.LogError("No BodyPoseExerciseTracker found in the scene. Please assign it in the inspector.");
                return;
            }
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // Subscribe to the pose detected event
        exerciseTracker.GetType()
            .GetField("_onPoseDetected", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(exerciseTracker)
            ?.GetType()
            .GetMethod("AddListener")
            ?.Invoke(exerciseTracker.GetType().GetField("_onPoseDetected", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(exerciseTracker),
                new object[] { new UnityEngine.Events.UnityAction<int, float>(OnPoseDetected) });
    }

    private void OnDestroy()
    {
        // Unsubscribe from the event when this component is destroyed
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
    /// Called when a pose is detected by the exercise tracker
    /// </summary>
    /// <param name="poseIndex">Index of the detected pose</param>
    /// <param name="accuracy">Accuracy of the pose detection</param>
    private void OnPoseDetected(int poseIndex, float accuracy)
    {
        if (!audioSource.isActiveAndEnabled) return;

        // Check if we should use pose-specific sounds
        if (usePoseSpecificSounds && poseSpecificSounds != null && poseSpecificSounds.Length > 0)
        {
            // If we have a specific sound for this pose index
            if (poseIndex < poseSpecificSounds.Length && poseSpecificSounds[poseIndex] != null)
            {
                audioSource.PlayOneShot(poseSpecificSounds[poseIndex]);
                return;
            }
        }

        // Fall back to the general pose detected sound
        if (poseDetectedSound != null)
        {
            audioSource.PlayOneShot(poseDetectedSound);
        }
    }

    /// <summary>
    /// Manually play the pose detected sound
    /// </summary>
    public void PlayPoseDetectedSound()
    {
        if (audioSource != null && poseDetectedSound != null)
        {
            audioSource.PlayOneShot(poseDetectedSound);
        }
    }
}
