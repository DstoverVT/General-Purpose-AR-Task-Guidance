using System;
using System.Collections;
using UnityEngine;

/** Manages hand 3D visuals from app. */
public class VisualController : MonoBehaviour
{
    [SerializeField]
    private GameObject handPress;
    [SerializeField]
    private GameObject handTwist;
    [SerializeField]
    private GameObject handPull;
    [SerializeField]
    private GameObject handPickup;
    [SerializeField]
    private GameObject testHand;
    [SerializeField]
    private GameObject arrow;
    [SerializeField]
    private Transform mainCamera;
    /* Testing pickup animation. */
    private bool testPickup = false;
    private bool testArrow = false;
    [SerializeField]
    public GameObject testDestObject;
    [SerializeField]
    private GameObject testSourceObject;
    [SerializeField]
    private float testVelocity;
    [SerializeField]
    private float testOffset;
    private Vector3 testDest;
    private Vector3 testSource;

    [SerializeField]
    private GameObject intersectionObject;
    

    public enum Hand
    {
        Press,
        Twist,
        Pickup,
        Pull,
        NumHands,
        PutDown,
        Error
    };
    /* Holds coordinates for pickup visual since it requires 2 detected objects. */
    private const int numPickupVisuals = 2;
    /* Use nullable type so by default the values are null. */
    [HideInInspector]
    public Vector3?[] pickupCoords = new Vector3?[numPickupVisuals];
    [HideInInspector]
    public Vector3?[] pickupNormals = new Vector3?[numPickupVisuals];

    private GameObject[] hands = new GameObject[(int)Hand.NumHands];
    /* Values for pickup visual curve, from testing. */
    private float pickupHeightOffset = -0.04f;
    //private float pickupVelocity = 0.0075f;
    private float pickupVelocity = 0.02f;
    private SpatialMapping spatialMapping;


    // Start is called before the first frame update
    void Start()
    {
        hands[(int)Hand.Press] = handPress;
        hands[(int)Hand.Twist] = handTwist;
        hands[(int)Hand.Pickup] = handPickup;
        hands[(int)Hand.Pull] = handPull;
        if (testPickup)
        {
            spatialMapping = GetComponent<AppController>().spatialMapper;
            testDest = testDestObject.transform.position;
            testSource = testSourceObject.transform.position;
            //StartCoroutine(MovePickupHand(testHand, testSource, testDest, testOffset, testVelocity));
            pickupCoords[0] = testSource;
            pickupCoords[1] = testDest;
            pickupNormals[0] = Vector3.up;
            pickupNormals[1] = Vector3.up;
            GameObject[] visuals = PlaceHandPickupVisuals();
            spatialMapping.AddToVisualsMap(0, visuals);
        }
        if (testArrow)
        {
            spatialMapping = GetComponent<AppController>().spatialMapper;
            GameObject[] visuals = { testDestObject, testSourceObject };
            spatialMapping.AddToVisualsMap(0, visuals);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (testPickup)
        {
            testDest = testDestObject.transform.position;
            testSource = testSourceObject.transform.position;
        }
    }


    public GameObject[] PlaceHandPickupVisuals()
    {
        Vector3 source = pickupCoords[0].Value;
        Vector3 sourceNormal = pickupNormals[0].Value;
        Vector3 destination = pickupCoords[1].Value;
        Vector3 destNormal = pickupNormals[1].Value;

        GameObject[] visuals = new GameObject[3];
        GameObject handVisual = hands[(int)Hand.Pickup];
        /* Rotate hand to face picking up towards ground. */
        Quaternion handRotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
        visuals[0] = Instantiate(handVisual, source, handRotation);
        StartCoroutine(MovePickupHand(visuals[0], source, destination, pickupHeightOffset, pickupVelocity));

        /* Put square on its side to make a diamond shape. */
        Quaternion offCenter = Quaternion.Euler(0, 0, 45);
        Quaternion intersectionRotation = Quaternion.LookRotation(-sourceNormal);
        visuals[1] = Instantiate(intersectionObject, source, intersectionRotation * offCenter);
        Quaternion intersectionRotationDest = Quaternion.LookRotation(-destNormal);
        visuals[2] = Instantiate(intersectionObject, destination, intersectionRotationDest * offCenter);

        return visuals;
    }


    //public void ClearPickupVisuals()
    //{
    //    /* Clear pickup visuals for next instruction. */
    //    Array.Clear(pickupCoords, 0, numPickupVisuals);
    //    Array.Clear(pickupNormals, 0, numPickupVisuals);
    //}


    /**
     * location: In world coordinates already. 
     */
    public GameObject[] PlaceHandVisual(Vector3 location, Vector3 normal, Hand type, bool test)
    {
        float[] distanceFromCenter = new float[(int)Hand.NumHands];
        distanceFromCenter[(int)Hand.Press] = 0.18f;
        distanceFromCenter[(int)Hand.Twist] = 0.10f;
        //distanceFromCenter[(int)Hand.Pickup] = 0.10f;
        distanceFromCenter[(int)Hand.Pull] = 0.10f;

        GameObject handVisual = hands[(int)type];
        float dist = distanceFromCenter[(int)type];

        /* Pickup motion should always be upward instead of facing normal */
        Vector3 handPosition;
        Quaternion handRotation;
        //if(type == Hand.Pickup)
        //{
        //    Vector3 offset = Vector3.up * dist;
        //    handPosition = location + offset;
        //    /* Rotate hand towards ground. */
        //    handRotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
        //}
        /* Place hand at location along normal to surface. */
        //else 
        //{
        Vector3 offset = normal * dist;
        handPosition = location + offset;
        /* Rotate hand towards surface. */
        handRotation = Quaternion.LookRotation(-normal);
        //}

        /* Place intersection point object */
        Quaternion intersectionRotation = Quaternion.LookRotation(-normal);
        /* Put square on its side to make a diamond shape. */
        Quaternion offCenter = Quaternion.Euler(0, 0, 45);

        GameObject[] visuals = new GameObject[2];
        /* Use single GameObject during testing */
        if (test)
        {
            handVisual.transform.position = handPosition;
            handVisual.transform.rotation = handRotation;
            visuals[0] = handVisual;

            intersectionObject.transform.position = location;
            intersectionObject.transform.rotation = intersectionRotation * offCenter;
            visuals[1] = intersectionObject;
            /* No need to return objects if testing, keep visuals empty. */
        }
        else
        {
            visuals = new GameObject[2];
            visuals[0] = Instantiate(handVisual, handPosition, handRotation);
            visuals[1] = Instantiate(intersectionObject, location, intersectionRotation * offCenter);
        }

        return visuals;
    }

    public void TestPlaceHand(Hand handType)
    {
        Vector3 location = new Vector3(0, 0, 0.3f);
        Vector3 camForward = mainCamera.TransformDirection(Vector3.forward);
        location = mainCamera.TransformPoint(location);
        PlaceHandVisual(location, -camForward, handType, true);
    }

    public void DisableTestVisuals(Hand handType)
    {
        GameObject handVisual = hands[(int)handType];
        handVisual.SetActive(false);
        intersectionObject.SetActive(false);
    }


    /** Move GameObject along an arc with source, destination, height offset, and velocity. */
    IEnumerator MovePickupHand(GameObject handObject, Vector3 source, Vector3 dest, float heightOffset, float velocity)
    {
        /* Get RightHand model. */
        Transform handModel = handObject.transform.GetChild(0);
        Animator handAnimation = handModel.GetComponent<Animator>(); 

        /* Loop animation forever while gameobject is active. */
        while (true)
        {
            if(testPickup)
            {
                //source = testSource;
                //dest = testDest;
                //heightOffset = testOffset;
                //velocity = testVelocity;
            }

            if (handObject.activeSelf)
            {
                //float elapsed = 0f;
                float timePoint = 0f;
                /* Reset position back to original. */
                handObject.transform.position = source;
                handAnimation.SetBool("beginPickup", true);

                /* Continue until object is at destination. */
                float stepTolerance = velocity / 2;
                float endTolerance = 0.001f;
                while ((handObject.transform.position - dest).sqrMagnitude > endTolerance)
                {
                    /* Begin place animation at end of movement. */
                    if(timePoint >= 0.90f)
                    {
                        handAnimation.SetBool("beginPlace", true);
                        handAnimation.SetBool("beginPickup", false);
                    }
                    //float timePoint = elapsed / testDuration;
                    Vector3 center = (dest + source) * 0.5f;
                    center += new Vector3(0, heightOffset, 0);
                    Vector3 centerTotestSource = source - center;
                    Vector3 centerToDest = dest - center;

                    /* Use spherical interpolation about center between testSource and testDest to move the 
                     * GameObject along an arc from testSource to testDest. */
                    Vector3 slerpLocation = Vector3.Slerp(centerTotestSource, centerToDest, timePoint) + center;
                    /* Distance left to next point. */
                    float updateMagnitude = (slerpLocation - handObject.transform.position).magnitude;
                    /* Update while hand is not at next point. */
                    if(updateMagnitude > stepTolerance)
                    {
                        Vector3 actualUpdate = (slerpLocation - handObject.transform.position).normalized * velocity;
                        handObject.transform.position += actualUpdate;
                    }
                    else
                    {
                        timePoint += 0.05f;
                    }

                    yield return null;
                }
            }

            handAnimation.SetBool("beginPlace", false);
            yield return null;
        }
    }


    private bool inFieldOfView(GameObject target)
    {
        Camera cameraComp = mainCamera.GetComponent<Camera>();
        Vector3 targetScreen = cameraComp.WorldToScreenPoint(target.transform.position);
        /* Give some tolerance to ensure target is more towards center of view */
        float widthTolerance = cameraComp.pixelWidth / 8;
        float heightTolerance = cameraComp.pixelHeight / 8;
        float maxDistance = 1f;
        /* Ensure target (in screen coordinates) is in screen */
        return (targetScreen.x >= widthTolerance && targetScreen.y >= heightTolerance &&
                targetScreen.x <= cameraComp.pixelWidth - widthTolerance && targetScreen.y <= cameraComp.pixelHeight - heightTolerance &&
                targetScreen.z >= 0 && targetScreen.z <= maxDistance);
    }


    /* Updates arrow to point towards target. */
    public void UpdateArrowGuide(GameObject target)
    {
        /* Only display arrow when GameObject to target is not in field of view */
        if (!inFieldOfView(target))
        {
            arrow.SetActive(true);
            /* Place arrow in front of and slightly below person (camera). */
            Vector3 inFrontCam = new Vector3(0, -0.3f, 1.5f);
            inFrontCam = mainCamera.TransformPoint(inFrontCam);
            arrow.transform.position = inFrontCam;

            /* Orient arrow towards target. */
            Vector3 arrowDirection = target.transform.position - arrow.transform.position;
            Quaternion arrowRotation = Quaternion.LookRotation(arrowDirection);
            arrow.transform.rotation = arrowRotation;
        }
        ///* Turn off arrow when in field of view */
        else
        {
            arrow.SetActive(false);
        }
    }


    public void ToggleArrow(bool enable)
    {
        if (arrow.activeSelf != enable)
        {
            arrow.SetActive(enable);
        }
    }
}
