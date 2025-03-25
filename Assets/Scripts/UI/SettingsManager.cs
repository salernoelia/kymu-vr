using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    public GameObject settingsMenu;

    public float Volume { get; private set; } = 1f;
    public int GraphicsQuality { get; private set; } = 1;

    [Tooltip("Reference to the OVRCameraRig's CenterEyeAnchor Transform")]
    public Transform cameraTransform;

    public float menuDistance = 1.5f;
    public Vector3 menuOffset = new Vector3(0, -0.3f, 0);
    public float maxDistanceBeforeClose = 3.0f;

    [Tooltip("Which controller button toggles the settings menu")]
    public OVRInput.Button toggleButton = OVRInput.Button.One; // A button by default

    [Tooltip("Which controller to use (or both)")]
    public OVRInput.Controller controllerMask = OVRInput.Controller.All;

    [Header("Haptic Feedback")]
    public bool enableHapticFeedback = true;
    [Range(0, 1)]
    public float hapticAmplitude = 0.5f;
    public float hapticDuration = 0.1f;
    public float hapticFrequency = 0.0f;

    private bool wasButtonPressed = false;
    private Vector3 menuSpawnPosition;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            settingsMenu.SetActive(false); // Ensure menu starts hidden

            // Register for scene change events
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }
        else
        {
            Destroy(gameObject);
        }

        // Find camera if not assigned
        if (cameraTransform == null)
        {
            OVRCameraRig cameraRig = FindObjectOfType<OVRCameraRig>();
            if (cameraRig != null)
            {
                cameraTransform = cameraRig.centerEyeAnchor;
            }
            else
            {
                Debug.LogError("No OVRCameraRig found in the scene! Please assign the camera transform manually.");
            }
        }
    }

    private void OnEnable()
    {
        // Additional subscription to scene events
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnDisable()
    {
        // Unsubscribe from scene events
        SceneManager.sceneUnloaded -= OnSceneUnloaded;

        // Ensure time is not frozen when component is disabled
        ResetTimeScale();
    }

    // Called when scene is unloaded
    private void OnSceneUnloaded(Scene scene)
    {
        // Reset time scale when the scene is unloaded
        ResetTimeScale();
    }

    // Reset time scale to normal
    private void ResetTimeScale()
    {
        Time.timeScale = 1f;
    }

    private void OnApplicationQuit()
    {
        // Ensure time is not frozen when application quits
        ResetTimeScale();
    }

    private void Update()
    {
        // Check for button press
        bool isButtonPressed = OVRInput.Get(toggleButton, controllerMask);

        // Button just pressed (rising edge detection)
        if (isButtonPressed && !wasButtonPressed)
        {
            ToggleSettingsMenu(!settingsMenu.activeSelf);

            // Play haptic feedback when settings menu is toggled
            if (enableHapticFeedback)
            {
                PlayHapticFeedback();
            }
        }

        wasButtonPressed = isButtonPressed;

        // Check if menu is active and user is too far away
        if (settingsMenu.activeSelf)
        {
            // Only check distance, don't update position
            CheckDistance();
        }
    }

    private void PositionMenuInFrontOfUser()
    {
        if (cameraTransform == null)
        {
            Debug.LogError("Camera reference is missing!");
            return;
        }

        // Position menu in front of user once
        menuSpawnPosition = cameraTransform.position + (cameraTransform.forward * menuDistance);
        settingsMenu.transform.position = menuSpawnPosition + menuOffset;

        // Apply the rotation
        Quaternion baseRotation = Quaternion.LookRotation(-cameraTransform.forward, Vector3.up);

        // Rotate 180 degrees around the Y axis
        settingsMenu.transform.rotation = baseRotation * Quaternion.Euler(0, 180, 0);
    }

    private void CheckDistance()
    {
        if (cameraTransform == null) return;

        // Calculate distance between user and menu spawn position
        float distance = Vector3.Distance(cameraTransform.position, menuSpawnPosition);

        // If user is too far away, close the menu
        if (distance > maxDistanceBeforeClose)
        {
            ToggleSettingsMenu(false);
        }
    }

    public void ToggleSettingsMenu(bool isVisible)
    {
        if (settingsMenu == null) return;

        // Set menu visibility
        settingsMenu.SetActive(isVisible);

        if (isVisible)
        {
            // Position menu in front of user only when toggling on
            PositionMenuInFrontOfUser();
        }

        // Optionally freeze/unfreeze time
        Time.timeScale = settingsMenu.activeSelf ? 0 : 1;
    }

    private void PlayHapticFeedback()
    {
        // Left controller
        OVRInput.SetControllerVibration(hapticFrequency, hapticAmplitude, OVRInput.Controller.LTouch);

        // Right controller
        OVRInput.SetControllerVibration(hapticFrequency, hapticAmplitude, OVRInput.Controller.RTouch);

        // Stop haptic feedback after duration
        StartCoroutine(StopHapticFeedback(hapticDuration));
    }

    private IEnumerator StopHapticFeedback(float duration)
    {
        yield return new WaitForSeconds(duration);
        OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch);
        OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
    }

    public void SetVolume(float volume)
    {
        Volume = volume;
        AudioListener.volume = volume;
    }

    public void SetGraphicsQuality(int qualityIndex)
    {
        GraphicsQuality = qualityIndex;
        QualitySettings.SetQualityLevel(qualityIndex);
    }

    private void OnDestroy()
    {
        // Unsubscribe to prevent any memory leaks
        SceneManager.sceneUnloaded -= OnSceneUnloaded;

        // Reset time scale
        ResetTimeScale();

        if (Instance == this)
        {
            Instance = null;
        }
    }
}
