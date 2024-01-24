using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class SpatialMapping : MonoBehaviour
{
    [SerializeField]
    private GameObject meshManager;
    public Camera mainCamera;

    private VisualController visualController;
    private LineRenderer rayLine;
    private GameObject sphere;


    // Start is called before the first frame update
    void Start()
    {
        visualController = GameObject.Find("VisualController").GetComponent<VisualController>();
        rayLine = GetComponent<LineRenderer>();
        sphere = GameObject.Find("IntersectionPoint");
    }

    // Update is called once per frame
    void LateUpdate()
    {
        //PlaceHandFromMesh();
    }

    public void setMeshTracking()
    {
        meshManager.SetActive(!meshManager.activeSelf);
    }

    public void PlaceHandFromMesh(Vector2 object2DLocation)
    {
        /* Attempt to raycast see where it hits the spatial mesh . */
        RaycastHit raycastHit;

        int spatialMeshLayer = 3;
        int layerMask = 1 << spatialMeshLayer;

        /* Raycast at middle of screen. */
        //Vector2 object2DLocation = new Vector2(mainCamera.pixelWidth / 2, mainCamera.pixelHeight / 2);

        Ray ray = mainCamera.ScreenPointToRay(object2DLocation);
        rayLine.positionCount = 2;
        rayLine.SetPositions(new Vector3[] { ray.origin, ray.origin + ray.direction * 5 });
        /* Draw line to show where ray is cast. */
        /* 3D raycast */ 
        if (Physics.Raycast(ray, out raycastHit, Mathf.Infinity, layerMask))
        {
            /* Place hand visual at position of mesh intersection. */
            Debug.Log("Found depth value");
            sphere.transform.position = raycastHit.point;
            visualController.PlaceHandVisual(raycastHit.point, raycastHit.normal);
        }
        else
        {
            Debug.Log("No depth was found.");
        }
    }

}
