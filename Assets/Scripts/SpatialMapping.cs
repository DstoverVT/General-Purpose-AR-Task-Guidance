using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Windows.WebCam;
using UnityEngine.XR.ARFoundation;

/** Manages raycasting against spatial mesh. */ 
public class SpatialMapping : MonoBehaviour
{
    public Camera mainCamera;
    public static int spatialMeshLayer = 3;
    public static int storedMeshLayer = 6;

    private VisualController visualController;
    private LineRenderer rayLine;
    private GameObject sphere;
    private bool raycastTest = false;
    private bool cameraTest = false;
    private Camera lastSavedCamera;
    private ARMeshManager meshManager;
    //private List<MeshFilter> lastSavedMesh;
    private float timer = 0f;

    [SerializeField]
    private VisualController.Hand testHandType;
    [SerializeField]
    private Camera testCameraComponent;
    [SerializeField]
    private Transform cameraOffset;
    [SerializeField]
    private GameObject spatialMeshPrefab;
    [SerializeField]
    private Material meshWireframe;
    [SerializeField]
    private Material meshTransparent;


    // Start is called before the first frame update
    void Start()
    {
        visualController = GameObject.Find("VisualController").GetComponent<VisualController>();
        meshManager = GameObject.Find("ARMeshManager").GetComponent<ARMeshManager>();
        rayLine = GetComponent<LineRenderer>();
        //sphere = GameObject.Find("IntersectionPoint");

        if(cameraTest)
        {
            testCameraComponent.CopyFrom(mainCamera);
        }
    }

    // Update is called once per frame
    private void Update()
    {
        //if (raycastTest)
        //{
        //    timer += Time.deltaTime;
        //}

        if(cameraTest)
        {
            RunCameraTest();
        }
    }

    void LateUpdate()
    {
        //if (raycastTest)
        //{
        //    if (timer > 5f)
        //    {
        //        testRaycast();
        //        Debug.Log("Move head to next camera position...");
        //    }
        //    if (timer > 8f)
        //    {
        //        ClearState();
        //        StoreState();
        //        timer = 0f;
        //    }
        //}

        if(raycastTest)
        {
            testRaycast();
        }

        
    }

    //public void setMeshTracking()
    //{
    //    meshManager.SetActive(!meshManager.activeSelf);
    //}


    public void PlaceHandFromMesh(Vector2 object2DLocation, VisualController.Hand handType, int raycastLayer, bool useStoredState)
    {
        /* Attempt to raycast see where it hits the spatial mesh . */
        RaycastHit raycastHit;

        int layerMask = 1 << raycastLayer;

        /* Raycast at middle of screen. */
        //Vector2 object2DLocation = new Vector2(mainCamera.pixelWidth / 2, mainCamera.pixelHeight / 2);

        Ray ray;
        if (useStoredState && lastSavedCamera != null)
        {
            Debug.Log("Using last saved camera");
            ray = lastSavedCamera.ScreenPointToRay(object2DLocation);
            /* Need to use custom camera with settings from the Hololens PV camera. */
        }
        else
        {
            ray = mainCamera.ScreenPointToRay(object2DLocation);
        }
        rayLine.positionCount = 2;
        rayLine.SetPositions(new Vector3[] { ray.origin, ray.origin + ray.direction * 5 });
        /* Draw line to show where ray is cast. */
        /* 3D raycast */ 
        if (Physics.Raycast(ray, out raycastHit, Mathf.Infinity, layerMask))
        {
            /* Place hand visual at position of mesh intersection. */
            //Debug.Log("Found depth value");

            /* Instead of placing hand, system should store:
             * Instruction: {
             *  Action 1: Vector3
             *  Action 2: Vector3
             * }
             * As 3D map of objects for each instruction
             * 
             * Then, display objects for the current instruction. */
            //sphere.transform.position = raycastHit.point;
            visualController.PlaceHandVisual(raycastHit.point, raycastHit.normal, handType);
        }
        else
        {
            visualController.TestPlaceHand(handType);
            Debug.Log("No depth was found.");
        }
    }


    private void testRaycast()
    {
        Vector2 object2DLocation = new Vector2(mainCamera.pixelWidth / 2, mainCamera.pixelHeight / 2);
        PlaceHandFromMesh(object2DLocation, testHandType, spatialMeshLayer, false);
    }


    public void toggleTestRaycast()
    {
        raycastTest = !raycastTest;

        /* Change spatial mesh prefab to have wireframe material */
        MeshRenderer renderer = spatialMeshPrefab.GetComponent<MeshRenderer>();
        renderer.material = raycastTest ? meshWireframe : meshTransparent;
                
        //if(raycastTest)
        //{
        //    StoreState();
        //}
        //else
        //{
        //    ClearState();
        //}
    }


    private void SetCameraTransform(Matrix4x4 matrix)
    {
        /* From unity docs:
         * "Note that camera space matches OpenGL convention: camera's forward is the negative Z axis. This is different from Unity's convention, where forward is the positive Z axis." */
        Vector3 camPosition = matrix.MultiplyPoint(Vector3.zero);
        Quaternion camRotation = Quaternion.LookRotation(-matrix.GetColumn(2), matrix.GetColumn(1));

        //Vector3 scale;
        //scale.x = matrix.GetColumn(0).magnitude;
        //scale.y = matrix.GetColumn(1).magnitude;
        //scale.z = matrix.GetColumn(2).magnitude;

        lastSavedCamera.transform.rotation = camRotation;
        lastSavedCamera.transform.position = camPosition;
        //lastSavedCamera.transform.localScale = scale;
    }


    public void StoreState(PhotoCaptureFrame capture)
    {
        /* Take snapshot of what main camera sees and its properties. */
        GameObject tempCameraObject = new GameObject("TempCameraObject");
        lastSavedCamera = tempCameraObject.AddComponent<Camera>();
        lastSavedCamera.CopyFrom(mainCamera);

        /* Set Camera's projection matrix from capture */
        if (capture.hasLocationData)
        {
            Matrix4x4 PVCameraMatrix;
            capture.TryGetProjectionMatrix(mainCamera.nearClipPlane, mainCamera.farClipPlane, out PVCameraMatrix);
            lastSavedCamera.projectionMatrix = PVCameraMatrix;
            /* Set Camera's transform position/rotation from capture */
            Matrix4x4 PVCameraWorld;
            capture.TryGetCameraToWorldMatrix(out PVCameraWorld);
            SetCameraTransform(PVCameraWorld);
            /* False will move the camera relative to the camera offset's position. */
            tempCameraObject.transform.SetParent(cameraOffset, false);
        }
        else
        {
            Debug.Log("Did not use PV Camera, no location data.");
        }

        lastSavedCamera.enabled = false;

        Debug.Log("Stored camera state, can move head now");
        //lastSavedMesh = new List<MeshFilter>(meshManager.meshes);

        /* Change mesh to layer 6 for future raycast. */
        //foreach(MeshFilter mesh in lastSavedMesh)
        //{
        //    mesh.gameObject.layer = storedMeshLayer;
        //}
    }

    public void ClearState()
    {
        Destroy(lastSavedCamera.gameObject);
        lastSavedCamera = null;
        /* Change mesh back to layer 3. */
        //foreach(MeshFilter mesh in lastSavedMesh)
        //{
        //    mesh.gameObject.layer = storedMeshLayer;
        //}
    }

    public Camera GetLastSavedCamera()
    {
        return lastSavedCamera;
    }

    private void RunCameraTest()
    {
        Vector2 object2DLocation = new Vector2(testCameraComponent.pixelWidth / 2, testCameraComponent.pixelHeight / 2);
        Ray ray;
        ray = testCameraComponent.ScreenPointToRay(object2DLocation);
        rayLine.positionCount = 2;
        rayLine.SetPositions(new Vector3[] { ray.origin, ray.origin + ray.direction * 5 });
    }

}
