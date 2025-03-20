using System.Collections;
using Unity.RenderStreaming;
using UnityEngine;

namespace RenderStreaming
{
    /// <summary>
    /// Streams XR camera view using a secondary camera that doesn't conflict with the Meta XR main camera
    /// </summary>
    public class XRCameraStreamer : MonoBehaviour
    {
        [Tooltip("The OVRCameraRig's CenterEyeAnchor transform")]
        [SerializeField] private Transform centerEyeAnchor;
    
        [Tooltip("Optional: The OVRCamera component (will be found automatically if not set)")]
        [SerializeField] private Camera mainXRCamera;
    
        [Tooltip("VideoStreamSender component to use for streaming")]
        [SerializeField] private VideoStreamSender streamSender;
    
        [Tooltip("The camera used for streaming (will be created if not set)")]
        [SerializeField] private Camera streamingCamera;
    
        [Tooltip("Resolution width for streaming")]
        [SerializeField] private int resolutionWidth = 1280;
    
        [Tooltip("Resolution height for streaming")]
        [SerializeField] private int resolutionHeight = 720;
    
        [Tooltip("Anti-aliasing level for render texture")]
        [SerializeField] private int antiAliasing = 1;
    
        [Tooltip("Depth buffer bit depth")]
        [SerializeField] private int depthBuffer = 24;
    
        private RenderTexture streamingRenderTexture;
        private bool isStreamingCameraCreated = false;
    
        void Start()
        {
            StartCoroutine(SetupAfterDelay());
        }
    
        private IEnumerator SetupAfterDelay()
        {
            // Wait for OVR to initialize its camera
            yield return new WaitForSeconds(0.5f);
        
            // Find the CenterEyeAnchor if not assigned
            if (centerEyeAnchor == null)
            {
                var ovrCameraRig = FindObjectOfType<OVRCameraRig>();
                if (ovrCameraRig != null)
                {
                    centerEyeAnchor = ovrCameraRig.centerEyeAnchor;
                    Debug.Log("Found CenterEyeAnchor automatically: " + centerEyeAnchor.name);
                }
                else
                {
                    Debug.LogError("No OVRCameraRig found in the scene. Please assign the CenterEyeAnchor manually.");
                    enabled = false;
                    yield break;
                }
            }
        
            // Find the main XR camera if not assigned
            if (mainXRCamera == null && centerEyeAnchor != null)
            {
                mainXRCamera = centerEyeAnchor.GetComponent<Camera>();
                if (mainXRCamera == null)
                {
                    Debug.LogWarning("No Camera component found on CenterEyeAnchor. Using default camera settings.");
                    // We'll still continue, but use default camera settings
                }
            }
        
            // Find the VideoStreamSender if not assigned
            if (streamSender == null)
            {
                streamSender = GetComponent<VideoStreamSender>();
                if (streamSender == null)
                {
                    Debug.LogError("No VideoStreamSender component found. Please add one to this GameObject.");
                    enabled = false;
                    yield break;
                }
            }
        
            // Setup streaming camera
            if (streamingCamera == null)
            {
                var cameraGO = new GameObject("XR Streaming Camera");
                cameraGO.transform.parent = transform;
                streamingCamera = cameraGO.AddComponent<Camera>();
                isStreamingCameraCreated = true;
            }
        
            // Configure the streaming camera
            streamingCamera.tag = "Untagged"; // Ensure it's not tagged as "MainCamera"
            streamingCamera.stereoTargetEye = StereoTargetEyeMask.None; // Don't render to VR display
            streamingCamera.enabled = false; // Important: Keep camera disabled to avoid rendering twice
        
            // Copy important camera settings from main camera if available
            if (mainXRCamera != null)
            {
                streamingCamera.fieldOfView = mainXRCamera.fieldOfView;
                streamingCamera.nearClipPlane = mainXRCamera.nearClipPlane;
                streamingCamera.farClipPlane = mainXRCamera.farClipPlane;
                streamingCamera.cullingMask = mainXRCamera.cullingMask;
                streamingCamera.clearFlags = mainXRCamera.clearFlags;
                streamingCamera.backgroundColor = mainXRCamera.backgroundColor;
            }
            else
            {
                // Default values if main camera not available
                streamingCamera.fieldOfView = 90f;
                streamingCamera.nearClipPlane = 0.01f;
                streamingCamera.farClipPlane = 1000f;
                streamingCamera.clearFlags = CameraClearFlags.SolidColor;
                streamingCamera.backgroundColor = Color.black;
            }
        
            // Remove audio listener if present to avoid conflicts
            AudioListener audioListener = streamingCamera.GetComponent<AudioListener>();
            if (audioListener != null)
            {
                Destroy(audioListener);
            }
        
            // Create render texture for the streaming camera
            streamingRenderTexture = new RenderTexture(resolutionWidth, resolutionHeight, depthBuffer);
            streamingRenderTexture.antiAliasing = antiAliasing;
            streamingRenderTexture.Create();
        
            // Assign the render texture to the streaming camera
            streamingCamera.targetTexture = streamingRenderTexture;
        
            // Configure the VideoStreamSender
            streamSender.source = VideoStreamSource.Camera;
            streamSender.sourceCamera = streamingCamera;
        
            Debug.Log("XR Camera Streaming setup complete. Streaming from " + (centerEyeAnchor ? centerEyeAnchor.name : "unknown") + " camera view.");
        }
    
        void LateUpdate()
        {
            if (centerEyeAnchor != null && streamingCamera != null)
            {
                // Sync the streaming camera with the CenterEyeAnchor every frame
                streamingCamera.transform.position = centerEyeAnchor.position;
                streamingCamera.transform.rotation = centerEyeAnchor.rotation;
            
                // Force the camera to render to its render texture each frame
                // This is needed because the camera is disabled to avoid double rendering
                if (!streamingCamera.enabled)
                {
                    streamingCamera.Render();
                }
            }
        }
    
        void OnDestroy()
        {
            if (streamingRenderTexture != null)
            {
                streamingRenderTexture.Release();
                Destroy(streamingRenderTexture);
            }
        
            if (streamingCamera != null && isStreamingCameraCreated)
            {
                Destroy(streamingCamera.gameObject);
            }
        }
    }
}