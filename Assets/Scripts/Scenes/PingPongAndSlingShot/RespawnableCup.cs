using UnityEngine;
using UnityEngine.InputSystem;
using Oculus.Interaction.Samples;

public class CupRespawner : MonoBehaviour
{
    [Tooltip("Optional key to trigger respawn manually")]
    [SerializeField] private Key _respawnKey = Key.R;

    [Tooltip("Set to true to automatically respawn all cups on Start")]
    [SerializeField] private bool _respawnOnStart = false;

    private void Start()
    {
        if (_respawnOnStart)
        {
            RespawnAllCups();
        }
    }

    private void Update()
    {
        // Optional keyboard trigger for testing
        var keyboard = Keyboard.current;
        if (keyboard != null && IsValidKey(_respawnKey) && keyboard[_respawnKey].wasPressedThisFrame)
        {
            Debug.Log("Respawning all cups");
            RespawnAllCups();
        }
    }

    private bool IsValidKey(Key key)
    {
        // Check if the key is valid (within enum range)
        return System.Enum.IsDefined(typeof(Key), key);
    }

    /// <summary>
    /// Respawns all GameObjects with the tag "RespawnableCup"
    /// </summary>
    [SerializeField]
    public void RespawnAllCups()
    {
        GameObject[] cups = GameObject.FindGameObjectsWithTag("RespawnableCup");

        foreach (GameObject cup in cups)
        {
            RespawnOnDrop respawner = cup.GetComponent<RespawnOnDrop>();
            if (respawner != null)
            {
                respawner.Respawn();
            }
        }

        Debug.Log($"Respawned {cups.Length} cups");
    }
}
