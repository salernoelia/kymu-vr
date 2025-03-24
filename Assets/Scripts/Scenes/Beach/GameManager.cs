using UnityEngine;
using UnityEngine.Events;

public class GameManager : MonoBehaviour
{
	public static GameManager Instance { get; private set; }
	
	[Header("Game Settings")]
	public int butterfliesRequired = 20;
	public float roundTime = 60f;
	
	[Header("Events")]
	public UnityEvent onRoundStart;
	public UnityEvent onRoundEnd;
	public UnityEvent<int> onButterflyCollected;
	
	private int butterfliesCollected = 0;
	private float currentRoundTime;
	private bool isRoundActive = false;
	
	public int ButterfliesCollected => butterfliesCollected;
	public float CurrentRoundTime => currentRoundTime;
	public bool IsRoundActive => isRoundActive;

	void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
			DontDestroyOnLoad(gameObject);
		}
		else
		{
			Destroy(gameObject);
		}
	}

	void Start()
	{
		StartNewRound();
		Debug.Log("ButterflyGameManager started");
	}

	void Update()
	{
		if (isRoundActive)
		{
			currentRoundTime -= Time.deltaTime;
			
			if (currentRoundTime <= 0 || butterfliesCollected >= butterfliesRequired)
			{
				EndRound();
			}
		}
	}

	public void ButterflyCollected()
	{
		butterfliesCollected++;
		onButterflyCollected?.Invoke(butterfliesCollected);
		Debug.Log("Butterfly collected! Total: " + butterfliesCollected);
	}

	public void StartNewRound()
	{
		butterfliesCollected = 0;
		currentRoundTime = roundTime;
		isRoundActive = true;
		onRoundStart?.Invoke();
		Debug.Log("New round started");
	}

	private void EndRound()
	{
		isRoundActive = false;
		onRoundEnd?.Invoke();
		// Add delay before starting new round
		Invoke(nameof(StartNewRound), 3f);
	}
}