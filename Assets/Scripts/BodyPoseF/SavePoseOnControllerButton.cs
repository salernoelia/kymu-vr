using Oculus.Interaction;
using UnityEngine;

namespace BodyPoseF
{
    public class SavePoseOnControllerButton : MonoBehaviour
    {
        [Tooltip("Reference to the PoseFromBody script that handles pose saving")]
        [SerializeField] private BodyPoseF.PoseFromBody poseFromBody;
    
        [Tooltip("Reference to the AudioTrigger script to play confirmation sound")]
        [SerializeField] private AudioTrigger audioTrigger;
    
        [Tooltip("Audio clip to play when pose is saved")]
        [SerializeField] private AudioClip saveConfirmationAudio;
    
        [Tooltip("Which controller button triggers the pose save")]
        [SerializeField] private OVRInput.Button triggerButton = OVRInput.Button.One; // A button by default
    
        [Tooltip("Which controller to use (left or right)")]
        [SerializeField] private OVRInput.Controller controllerType = OVRInput.Controller.RTouch;
    
        [Tooltip("Cooldown period between saves (seconds)")]
        [SerializeField] private float saveCooldown = 0.5f;
    
        private bool canSave = true;
        private float lastSaveTime = 0f;

        void Start()
        {
            // Validate references
            if (poseFromBody == null)
            {
                Debug.LogError("PoseFromBody reference is missing! Please assign it in the inspector.");
            }
        
            if (audioTrigger == null)
            {
                Debug.LogError("AudioTrigger reference is missing! Please assign it in the inspector.");
            }
        
            if (saveConfirmationAudio == null)
            {
                Debug.LogWarning("Save confirmation audio is missing! No sound will play on save.");
            }
        }

        void Update()
        {
            // Handle cooldown
            if (!canSave)
            {
                if (Time.time - lastSaveTime >= saveCooldown)
                {
                    canSave = true;
                }
                else
                {
                    return;
                }
            }
        
            // Check if the button is pressed using Meta SDK
            if (OVRInput.GetDown(triggerButton, controllerType))
            {
                SavePoseAndPlayAudio();
            
                // Start cooldown
                canSave = false;
                lastSaveTime = Time.time;
            }
        }
    
        private void SavePoseAndPlayAudio()
        {
            // Save the pose
            if (poseFromBody != null)
            {
                // Fix: Use the instance variable instead of the class name
                poseFromBody.SavePoseToFile();
                Debug.Log("Pose saved!");

                // Play confirmation audio
                if (audioTrigger != null && saveConfirmationAudio != null)
                {
                    audioTrigger.PlayAudio();
                }
            }
        }
    }
}