using UnityEngine;

public class ActivatePasstroughOnStart : MonoBehaviour
{
    [SerializeField]
    private Oculus.Interaction.Samples.MRPassthrough MRPassthrough;

    [SerializeField]
    public bool shouldPasstroughBeActivated;
    void Start()
    {
        if (MRPassthrough == null)
        {
            Debug.LogError("MRPassthrough is not set in the inspector");
            return;
        }

        if (shouldPasstroughBeActivated)
        {
            if (Oculus.Interaction.Samples.MRPassthrough.PassThrough.IsPassThroughOn)
            {
                return;
            }

            MRPassthrough.TogglePassThrough();
        } else if (!shouldPasstroughBeActivated)
        {
            if (!Oculus.Interaction.Samples.MRPassthrough.PassThrough.IsPassThroughOn)
            {
                return;
            }
            MRPassthrough.TogglePassThrough();
        }

    }

 
    void Update()
    {

    }
}
