using UnityEngine;

public class Collector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoxCollider catchingCollider;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip catchSound;
    
    [Header("Settings")]
    [SerializeField] private string butterflyTag = "Butterfly";

    private void Start()
    {
        if (catchingCollider == null)
        {
            Debug.LogError("Catching collider not assigned!");
            return;
        }
        
        catchingCollider.isTrigger = true;
        Debug.Log("ButterflyCollector initialized with collider: " + catchingCollider.name);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"Trigger entered by: {other.gameObject.name} with tag: {other.tag}");
        
        if (other.CompareTag(butterflyTag) && GameManager.Instance.IsRoundActive)
        {
            Debug.Log("Butterfly hit detected!");
            CatchButterfly(other.gameObject);
        }
    }
    
  private void CatchButterfly(GameObject butterfly)
{
    if (audioSource != null && catchSound != null)
    {
        audioSource.PlayOneShot(catchSound);
    }
    
    SwarmSpawner spawner = FindAnyObjectByType<SwarmSpawner>();
    if (spawner != null)
    {
        spawner.RemoveButterfly(butterfly.transform);
    }
    
    GameManager.Instance.ButterflyCollected();
    Destroy(butterfly);
}
}