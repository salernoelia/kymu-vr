using UnityEngine;
using Oculus.Interaction.Samples;

public class CupRespawner : MonoBehaviour
{
    [Tooltip("Optional key to trigger respawn manually")]
    [SerializeField] private KeyCode _respawnKey = KeyCode.R;

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
        if (Input.GetKeyDown(_respawnKey))
        {
            RespawnAllCups();
        }
    }

    /// <summary>
    /// Respawns all GameObjects with the tag "RespawnableCup"
    /// </summary>
    /// 
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