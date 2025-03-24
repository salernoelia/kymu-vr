using UnityEngine;
using System.Collections.Generic;

public class SwarmSpawner : MonoBehaviour
{
	[SerializeField] private GameObject butterflyPrefab;
	[SerializeField] private int numberOfButterflies = 20;
	[SerializeField] private float spawnRadius = 2.5f;
	[SerializeField] private float minHeight = 0f;
	[SerializeField] private float maxHeight = 1.5f;
	[SerializeField] private float flySpeed = 2f;
	[SerializeField] private float rotationSpeed = 2f;
	
	private List<Transform> butterflies = new List<Transform>();
	private List<Vector3> targetPositions = new List<Vector3>();
	
	private void Start()
	{
		SpawnButterflies();
	}
	
		private void OnEnable()
	{
		if (GameManager.Instance != null)
		{
			GameManager.Instance.onRoundStart.AddListener(SpawnButterflies);
			GameManager.Instance.onRoundEnd.AddListener(ClearButterflies);
		}
	}
	
	 public void RemoveButterfly(Transform butterfly)
    {
        int index = butterflies.IndexOf(butterfly);
        if (index != -1)
        {
            butterflies.RemoveAt(index);
            targetPositions.RemoveAt(index);
        }
    }
	
	private void OnDisable()
	{
		if (GameManager.Instance != null)
		{
			GameManager.Instance.onRoundStart.RemoveListener(SpawnButterflies);
			GameManager.Instance.onRoundEnd.RemoveListener(ClearButterflies);
		}
	}
	private void ClearButterflies()
	{
		foreach (Transform butterfly in butterflies)
		{
			if (butterfly != null)
			{
				Destroy(butterfly.gameObject);
			}
		}
		butterflies.Clear();
		targetPositions.Clear();
	}
	
	private void SpawnButterflies()
	{
		ClearButterflies();
		int numberOfButterflies = GameManager.Instance.butterfliesRequired;
		
		for (int i = 0; i < numberOfButterflies; i++)
		{
			Vector3 randomPosition = GetRandomPosition();
			GameObject butterfly = Instantiate(butterflyPrefab, randomPosition, Quaternion.identity);
			butterfly.tag = "Butterfly";
			butterflies.Add(butterfly.transform);
			targetPositions.Add(GetRandomPosition());
		}
	}
	 private void Update()
    {
        for (int i = butterflies.Count - 1; i >= 0; i--)
        {
            if (butterflies[i] == null)
            {
                butterflies.RemoveAt(i);
                targetPositions.RemoveAt(i);
                continue;
            }

			Transform butterfly = butterflies[i];
			Vector3 targetPosition = targetPositions[i];
			
			if (Vector3.Distance(butterfly.position, targetPosition) < 0.1f)
			{
				targetPositions[i] = GetRandomPosition();
			}
			
			Vector3 direction = (targetPositions[i] - butterfly.position).normalized;
			butterfly.position = Vector3.MoveTowards(butterfly.position, targetPositions[i], flySpeed * Time.deltaTime);
			
			if (direction != Vector3.zero)
			{
	
				Quaternion targetRotation = Quaternion.LookRotation(-direction);
				butterfly.rotation = Quaternion.Slerp(butterfly.rotation, targetRotation, rotationSpeed * Time.deltaTime);
			}
        }
    }
	
	private Vector3 GetRandomPosition()
	{
		Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
		float randomHeight = Random.Range(minHeight, maxHeight);
		return transform.position + new Vector3(randomCircle.x, randomHeight, randomCircle.y);
	}
}