using UnityEngine;

public class VisualController : MonoBehaviour
{
    [SerializeField]
    private GameObject handPress;
    [SerializeField]
    private GameObject handTwist;

    public enum Hand
    {
        Press = 0,
        Twist = 1,
        Error = 2
    };

    private GameObject[] hands = new GameObject[2];
    private Transform mainCamera;


    // Start is called before the first frame update
    void Start()
    {
        hands[0] = handPress;
        hands[1] = handTwist;
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
        GameObject handVisual = hands[(int)type];

        /* Place hand at location along normal to surface. */
        Vector3 offset = normal * 0.1f;
        handVisual.transform.position = location + offset;
        /* Rotate hand towards surface. */
        handVisual.transform.rotation = Quaternion.LookRotation(-normal);
        handVisual.SetActive(true);
    }

    public void TestPlaceHand()
    {
        Vector3 location = new Vector3(0, 0, 0.3f);
        Vector3 camForward = mainCamera.TransformDirection(Vector3.forward);
        location = mainCamera.TransformPoint(location);
        PlaceHandVisual(location, -camForward, Hand.Twist);
    }
}
