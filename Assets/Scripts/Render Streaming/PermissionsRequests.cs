using System.Collections;
using UnityEngine;

namespace Render_Streaming
{
    /// <summary>
    /// Handles permission requests for camera, microphone, and other device features.
    /// This is particularly useful for XR applications that need access to these devices.
    /// </summary>
    public class PermissionsRequests : MonoBehaviour
    {
        [Tooltip("Whether to request camera permissions on start")]
        [SerializeField] private bool requestCameraOnStart = true;
    
        [Tooltip("Whether to request microphone permissions on start")]
        [SerializeField] private bool requestMicrophoneOnStart = true;
    
        [Tooltip("Delay in seconds before requesting permissions")]
        [SerializeField] private float requestDelay = 0.5f;
    
        /// <summary>
        /// Indicates whether camera permission has been granted
        /// </summary>
    
        public bool IsCameraAuthorized { get; private set; }
    
        /// <summary>
        /// Indicates whether microphone permission has been granted
        /// </summary>
        public bool IsMicrophoneAuthorized { get; private set; }
    
        void Start()
        {
            if (requestCameraOnStart || requestMicrophoneOnStart)
            {
                StartCoroutine(RequestPermissionsWithDelay());
            }
        }
    
        private IEnumerator RequestPermissionsWithDelay()
        {
            // Wait a moment before requesting permissions
            yield return new WaitForSeconds(requestDelay);
        
            if (requestCameraOnStart)
            {
                yield return RequestCameraPermission();
            }
        
            if (requestMicrophoneOnStart)
            {
                yield return RequestMicrophonePermission();
            }
        }
    
        /// <summary>
        /// Requests camera permission from the user
        /// </summary>
        /// <returns>Coroutine that completes when the permission request is processed</returns>
        public IEnumerator RequestCameraPermission()
        {
            Debug.Log("Requesting camera permission...");
        
            // Request camera permission
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        
            // Check if permission was granted
            IsCameraAuthorized = Application.HasUserAuthorization(UserAuthorization.WebCam);
        
            Debug.Log($"Camera permission {(IsCameraAuthorized ? "granted" : "denied")}");
        }
    
        /// <summary>
        /// Requests microphone permission from the user
        /// </summary>
        /// <returns>Coroutine that completes when the permission request is processed</returns>
        public IEnumerator RequestMicrophonePermission()
        {
            Debug.Log("Requesting microphone permission...");
        
            // Request microphone permission
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
        
            // Check if permission was granted
            IsMicrophoneAuthorized = Application.HasUserAuthorization(UserAuthorization.Microphone);
        
            Debug.Log($"Microphone permission {(IsMicrophoneAuthorized ? "granted" : "denied")}");
        }
    
        /// <summary>
        /// Force request both camera and microphone permissions
        /// </summary>
        public void RequestAllPermissions()
        {
            StartCoroutine(RequestAllPermissionsCoroutine());
        }
    
        private IEnumerator RequestAllPermissionsCoroutine()
        {
            yield return RequestCameraPermission();
            yield return RequestMicrophonePermission();
        }
    
        /// <summary>
        /// Checks if the device has camera permission already granted
        /// </summary>
        /// <returns>True if camera permission is granted</returns>
        public bool CheckCameraPermission()
        {
            IsCameraAuthorized = Application.HasUserAuthorization(UserAuthorization.WebCam);
            return IsCameraAuthorized;
        }
    
        /// <summary>
        /// Checks if the device has microphone permission already granted
        /// </summary>
        /// <returns>True if microphone permission is granted</returns>
        public bool CheckMicrophonePermission()
        {
            IsMicrophoneAuthorized = Application.HasUserAuthorization(UserAuthorization.Microphone);
            return IsMicrophoneAuthorized;
        }
    }
}