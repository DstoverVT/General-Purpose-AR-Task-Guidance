using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class PlaneController : MonoBehaviour
{
    //public Vector3 objectLocation;

    /// <summary>
    /// Variables to get from Unity inspector. 
    /// </summary>
    [SerializeField]
    private Camera mainCamera;

    private ARPlaneManager planeManager;
    private ARRaycastManager raycastManager;
    private VisualController visualController;
    private LineRenderer rayLine;

    // Start is called before the first frame update
    void Start()
    {
        planeManager = GetComponent<ARPlaneManager>();
        raycastManager = GetComponent<ARRaycastManager>();
        visualController = GameObject.Find("VisualController").GetComponent<VisualController>();
        rayLine = GetComponent<LineRenderer>();

        /* Find closest ARPlane to objectLocation */
        //ARPlane closestPlane = findClosestPlane();    

        //Debug.Log("Width: " + mainCamera.pixelWidth);
        //Debug.Log("Height: " + mainCamera.pixelHeight);

        //foreach(ARPlane plane in planeManager.trackables)
        //{
        //    plane.gameObject.SetActive(false);
        //}

    }

    // Update is called once per frame
    void Update()
    {
        /* Hide all planes that aren't found. */ 
        //foreach(ARPlane plane in planeManager.trackables)
        //{
        //    if (!plane.gameObject.activeSelf)
        //    {
        //        plane.gameObject.SetActive(false);
        //    }
        //}

        /* Disable plane manager once it has found planes. */
        //if(planeManager.trackables.count > 20)
        //{
        //    Debug.Log("Plane manager disabled.");
        //    planeManager.enabled = false;
        //}

        /*
        if(timer > waitTime)
        {
            timer -= waitTime;

            int i = 1;
            foreach(ARPlane plane in planeManager.trackables)
            {
                Debug.Log("Plane " + i + " center: ");
                Debug.Log(plane.center);
                i++;
            }
        }

        timer += Time.deltaTime;
        */
    }

    
    //public void PlaceHandAtDepth()
    //{
    //    /* Attempt to raycast at objectLocation first and see if it hits a vertical plane. */
    //    List<ARRaycastHit> raycastHits = new List<ARRaycastHit>();

    //    /* Raycast at bottom side of screen. */
    //    Vector2 object2DLocation = new Vector2(mainCamera.pixelWidth / 2, mainCamera.pixelHeight / 1.5f);

    //    Ray ray = mainCamera.ScreenPointToRay(object2DLocation);
    //    /* Draw line to show where ray is cast. */
    //    rayLine.positionCount = 2;
    //    rayLine.SetPositions(new Vector3[] { ray.origin, ray.origin + ray.direction * 5 });
    //    if (raycastManager.Raycast(ray, raycastHits, UnityEngine.XR.ARSubsystems.TrackableType.Depth))
    //    {
    //        //Debug.Log("Plane manager disabled.");
    //        //planeManager.enabled = false;
    //        ///* Show plane that was hit */
    //        //ARPlane hitPlane = (ARPlane)raycastHits[0].trackable;
    //        //Debug.Log("Plane was hit by raycast.");
    //        /* Place hand visual at position of plane intersection. */
    //        Debug.Log("Found depth value");
    //        visualController.PlaceHandVisual(raycastHits[0].pose.position);
    //        //return hitPlane;
    //    }
    //    else
    //    {
    //        Debug.Log("No depth was found.");
    //    }
        
    //    //return null;
        
    //    /* If raycast had no hits, loop through all planes and return closest one. */
    //    /*
    //    foreach(ARPlane plane in planeManager.trackables)
    //    {
            
    //    }
    //    */
    //}


    /** Call this method on button press. */
    //public void ShowClosestPlane()
    //{
    //    Debug.Log("Show Plane method.");
    //    ARPlane closestPlane = FindClosestPlane();
    //    /* Make hit plane visible and change color to green. */
    //    if (closestPlane != null)
    //    {
    //        closestPlane.gameObject.SetActive(true);
    //        MeshRenderer planeRenderer = closestPlane.gameObject.GetComponent<MeshRenderer>();
    //        Material greenPlane = new Material(planeRenderer.material);
    //        greenPlane.color = Color.green;
    //        planeRenderer.material = greenPlane;
            
    //    }
    //}    
    

}
