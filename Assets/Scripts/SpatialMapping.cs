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

    private AppController app;
    private LineRenderer rayLine;
    private bool raycastTest = false;
    private bool cameraTest = false;
    private Camera lastSavedCamera;
    private ARMeshManager meshManager;

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
        app = GameObject.Find("AppController").GetComponent<AppController>();
        meshManager = GameObject.Find("ARMeshManager").GetComponent<ARMeshManager>();
        rayLine = GetComponent<LineRenderer>();

        if(cameraTest)
        {
            testCameraComponent.CopyFrom(mainCamera);
        }
    }

    // Update is called once per frame
    private void Update()
    {
        /* Handle when to display raycast pink line. */
        if(app.appState == AppController.AppState.USER && rayLine.enabled && !raycastTest)
        {
            rayLine.positionCount = 0;
            rayLine.enabled = false;
        }
        else if(app.appState != AppController.AppState.USER && !rayLine.enabled)
        {
            rayLine.enabled = true;
        }

        if(cameraTest)
        {
            RunCameraTest();
        }
    }

    void LateUpdate()
    {
        /* Testing raycast with camera state. */
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
            if(!rayLine.enabled)
            {
                rayLine.enabled = true;
            }
            testRaycast();
        }
    }


    public void AddToVisualsMap(int instructionNum, GameObject[] actionVisuals)
    {
        /* Create initial List if instruction index does not exist yet. */
        if (instructionNum >= app.visualsMap.Count)
        {
            int numToAdd = (instructionNum + 1) - app.visualsMap.Count;
            for (int i = 0; i < numToAdd; i++)
            {
                app.visualsMap.Add(new List<GameObject>());
            }
        }

        /* Only one visual per instruction, so can clear beforehand. */
        app.visualsMap[instructionNum].Clear();
        app.visualsMap[instructionNum].AddRange(actionVisuals);
    }


    /** Adapted from PlaceHandFromMesh to store result in visualsMap. */
    public void StoreMapFromMesh(int instructionNum, Vector2 object2DLocation, VisualController.Hand handType, bool test)
    {
        /* Attempt to raycast see where it hits the spatial mesh . */
        RaycastHit raycastHit;

        int layerMask = 1 << spatialMeshLayer;

        Ray ray;
        /* Need to use custom camera with settings from the Hololens PV camera. */
        if (test)
        {
            ray = mainCamera.ScreenPointToRay(object2DLocation);
        }
        else
        {
            ray = lastSavedCamera.ScreenPointToRay(object2DLocation);
        }
        //ray = mainCamera.ScreenPointToRay(object2DLocation);
        /* Draw line to show where ray is cast. */
        rayLine.positionCount = 2;
        rayLine.SetPositions(new Vector3[] { ray.origin, ray.origin + ray.direction * 5 });
        /* 3D raycast */ 
        if (Physics.Raycast(ray, out raycastHit, Mathf.Infinity, layerMask))
        {
            /* Store hand visual at position of mesh intersection.
                
             * Instruction list:
             * [[GameObject 1, GameObject 2], [..], ...]
             */
            /* Handle pickup visual, requires 2 coordinates before storing in map. */
            GameObject[] visuals;
            if (handType == VisualController.Hand.Pickup || handType == VisualController.Hand.PutDown)
            {
                /* Add Pickup coordinates to index 0 and PutDown coordinates to index 1. */
                int coordIndex = (handType == VisualController.Hand.Pickup) ? 0 : 1;
                app.visualController.pickupCoords[coordIndex] = raycastHit.point;
                app.visualController.pickupNormals[coordIndex] = raycastHit.normal;

                /* Once there's both a Pickup and PutDown visual stored. */
                if (app.visualController.pickupCoords[0] != null &&
                    app.visualController.pickupCoords[1] != null)
                {
                    visuals = app.visualController.PlaceHandPickupVisuals();
                    if (test)
                    {
                        foreach (GameObject visual in visuals)
                        {
                            visual.SetActive(true);
                        }
                    }
                    else
                    {
                        AddToVisualsMap(instructionNum, visuals);
                    }
                }
            }
            else
            {
                visuals = app.visualController.PlaceHandVisual(raycastHit.point, raycastHit.normal, handType, test);
                if (test)
                {
                    foreach (GameObject visual in visuals)
                    {
                        visual.SetActive(true);
                    }
                }
                else
                {
                    AddToVisualsMap(instructionNum, visuals);
                }
            }
        }
        else
        {
            /* Place hand right in front of user by default */
            Debug.LogWarning("No depth was found.");
        }
            
    }


    //public void TestPlaceHandFromMesh(Vector2 object2DLocation, VisualController.Hand handType, bool useStoredState)
    //{
    //    /* Attempt to raycast see where it hits the spatial mesh . */
    //    RaycastHit raycastHit;

    //    int layerMask = 1 << spatialMeshLayer;

    //    Ray ray;
    //    /* Need to use custom camera with settings from the Hololens PV camera. */
    //    if (useStoredState && lastSavedCamera != null)
    //    {
    //        Debug.Log("Using last saved camera");
    //        ray = lastSavedCamera.ScreenPointToRay(object2DLocation);
    //    }
    //    else
    //    {
    //        ray = mainCamera.ScreenPointToRay(object2DLocation);
    //    }
    //    rayLine.positionCount = 2;
    //    rayLine.SetPositions(new Vector3[] { ray.origin, ray.origin + ray.direction * 5 });
    //    /* Draw line to show where ray is cast. */
    //    /* 3D raycast */ 
    //    if (Physics.Raycast(ray, out raycastHit, Mathf.Infinity, layerMask))
    //    {
    //        bool test = !useStoredState;
    //        GameObject[] visuals = app.visualController.PlaceHandVisual(raycastHit.point, raycastHit.normal, handType);
    //        /* Place hand visuals right away */
    //        if (visuals[0] != null)
    //        {
    //            foreach (GameObject visual in visuals)
    //            {
    //                visual.SetActive(true);
    //            }
    //        }
    //    }
    //    else
    //    {
    //        /* Place hand right in front of user by default */
    //        app.visualController.TestPlaceHand(handType);
    //        Debug.Log("No depth was found.");
    //    }
    //}


    public void testRaycast()
    {
        Vector2 object2DLocation = new Vector2(mainCamera.pixelWidth / 2, mainCamera.pixelHeight / 2);
        //TestPlaceHandFromMesh(object2DLocation, testHandType, false);
        StoreMapFromMesh(app.instructionController.currentInstruction, object2DLocation, testHandType, true);

        /* Change mesh to use wireframe while debugging. */
        foreach(MeshFilter mesh in meshManager.meshes)
        {
            MeshRenderer renderer = mesh.gameObject.GetComponent<MeshRenderer>();
            renderer.material = meshWireframe;
        }
    }


    public void toggleTestRaycast()
    {
        raycastTest = !raycastTest;

        if(!raycastTest)
        {
            /* Change mesh back to transparent after debugging. */
            foreach(MeshFilter mesh in meshManager.meshes)
            {
                MeshRenderer renderer = mesh.gameObject.GetComponent<MeshRenderer>();
                renderer.material = meshTransparent;
            }

            /* Disable test hand when done with testing. */
            app.visualController.DisableTestVisuals(testHandType);
        }


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

        lastSavedCamera.transform.rotation = camRotation;
        lastSavedCamera.transform.position = camPosition;
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
    }

    public void ClearState()
    {
        Destroy(lastSavedCamera.gameObject);
        lastSavedCamera = null;
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
