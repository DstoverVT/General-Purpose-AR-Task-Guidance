using UnityEngine;

public class VisualController : MonoBehaviour
{
    [SerializeField]
    private GameObject handPress;
    [SerializeField]
    private GameObject handTwist;
    [SerializeField]
    private GameObject handPickup;

    public enum Hand
    {
        Press,
        Twist,
        Pickup,
        Pull,
        NumHands,
        Error
    };

    private GameObject[] hands = new GameObject[(int)Hand.NumHands];
    private Transform mainCamera;


    // Start is called before the first frame update
    void Start()
    {
        hands[(int)Hand.Press] = handPress;
        hands[(int)Hand.Twist] = handTwist;
        hands[(int)Hand.Pickup] = handPickup;
        hands[(int)Hand.Pull] = handPickup;
        mainCamera = GameObject.Find("Main Camera").transform;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    /**
     * location: In world coordinates already. 
     */
    public void PlaceHandVisual(Vector3 location, Vector3 normal, Hand type)
    {
        float[] distanceFromCenter = new float[(int)Hand.NumHands];
        distanceFromCenter[(int)Hand.Press] = 0.25f;
        distanceFromCenter[(int)Hand.Twist] = 0.15f;
        distanceFromCenter[(int)Hand.Pickup] = 0.15f;
        distanceFromCenter[(int)Hand.Pull] = 0.15f;

        GameObject handVisual = hands[(int)type];
        float dist = distanceFromCenter[(int)type];

        /* Pickup motion should always be upward instead of facing normal */
        if(type == Hand.Pickup)
        {
            Vector3 offset = Vector3.up * dist;
            handVisual.transform.position = location + offset;
            /* Rotate hand towards ground. */
            handVisual.transform.rotation = Quaternion.LookRotation(-Vector3.up, Vector3.right);
        }
        /* Place hand at location along normal to surface. */
        else 
        {
            Vector3 offset = normal * dist;
            handVisual.transform.position = location + offset;
            /* Rotate hand towards surface. */
            handVisual.transform.rotation = Quaternion.LookRotation(-normal);
        }
        handVisual.SetActive(true);
    }

    public void TestPlaceHand(Hand handType)
    {
        Vector3 location = new Vector3(0, 0, 0.3f);
        Vector3 camForward = mainCamera.TransformDirection(Vector3.forward);
        location = mainCamera.TransformPoint(location);
        PlaceHandVisual(location, -camForward, handType);
    }

    public void DisableVisual(Hand handType)
    {
        GameObject handVisual = hands[(int)handType];
        handVisual.SetActive(false);
    }
}
