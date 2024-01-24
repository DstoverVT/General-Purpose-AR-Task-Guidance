using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using System.IO;
using UnityEngine.Windows.WebCam;
using UnityEngine.Networking;
using UnityEngine.Events;
using UnityEditor;
using Newtonsoft.Json;
using System.Collections.Generic;

public class ObjectDetector : MonoBehaviour
{
    private PhotoCapture photoCaptureObject;
    private SpatialMapping spatialMapper;
    private Resolution imageResolution;

    private string imagePath;
    [SerializeField]
    private string serverIP;
    [SerializeField]
    private string serverPort;

    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    [Serializable]
    private class DetectionResults
    {
        public List<List<float>> boxes { get; set; }
        public List<string> phrases { get; set; }
        public bool success { get; set; }
    }

    [Serializable]
    private class UnityDetectionEvent : UnityEvent<DetectionResults>
    {}

    /* Action basically creates a delegate (function holder) and event in one, with parameter DetectionResults. */
    private UnityDetectionEvent OnDetectionComplete;


    // Start is called before the first frame update
    void Start()
    {
        if (OnDetectionComplete == null)
        {
            OnDetectionComplete = new UnityDetectionEvent();
        }
        spatialMapper = GameObject.Find("SpatialMapping").GetComponent<SpatialMapping>();
        OnDetectionComplete.AddListener(handleDetectionResults);
    }

    // Update is called once per frame
    void Update()
    {
    }

    private void handleDetectionResults(DetectionResults results)
    {
        if (results.boxes.Count > 0)
        {
            List<float> box1 = results.boxes[0];
            Debug.Log("First box: ");
            Debug.Log(string.Join(", ", box1));

            Vector2 objectLocation = TransformBoxToViewportPixels(new Vector2(box1[0], box1[1]));
            spatialMapper.PlaceHandFromMesh(objectLocation);
        }
        else
        {
            Debug.Log("No boxes were found for object");
        }
    }

    /* Convert the box pixel location to a location on Hololens view */
    private Vector2 TransformBoxToViewportPixels(Vector2 imagePixels)
    {
        /* Image taken from Hololens has different resolution than view does */
        float widthScale = (float)spatialMapper.mainCamera.pixelWidth / (float)imageResolution.width;
        float heightScale = (float)spatialMapper.mainCamera.pixelHeight / (float)imageResolution.height;
        float x_coord = imagePixels.x * widthScale;
        /* box is returned as (x, y) from top-left, Unity uses bottom-left as (0, 0). Conversion: */
        float y_coord = spatialMapper.mainCamera.pixelHeight - (imagePixels.y * heightScale); 

        return new Vector2(x_coord, y_coord);
    }

    /* Called on button press right now. */
    public void SaveImage()
    {
        PhotoCapture.CreateAsync(false, OnPhotoCaptureCreated);
    }

    void OnPhotoCaptureCreated(PhotoCapture captureObject)
    {
        photoCaptureObject = captureObject;

        int cameraWidth = spatialMapper.mainCamera.pixelWidth;
        int cameraHeight = spatialMapper.mainCamera.pixelHeight;
        Debug.Log("Unity Camera Width: " + cameraWidth);
        Debug.Log("Unity Camera Height: " + cameraHeight);

        Resolution photoResolution = PhotoCapture.SupportedResolutions.OrderByDescending((res) => res.width * res.height).First();
        //Resolution cameraResolution = PhotoCapture.SupportedResolutions
        //    .FirstOrDefault((res) => res.width == cameraWidth && res.height == cameraHeight);
        imageResolution = photoResolution;
        Debug.Log("Image Width: " + photoResolution.width);
        Debug.Log("Image Height: " + photoResolution.height);

        CameraParameters c = new CameraParameters();
        c.hologramOpacity = 0.0f;
        c.cameraResolutionWidth = photoResolution.width;
        c.cameraResolutionHeight = photoResolution.height;
        c.pixelFormat = CapturePixelFormat.BGRA32;

        captureObject.StartPhotoModeAsync(c, OnPhotoModeStarted);
    }

    private void OnPhotoModeStarted(PhotoCapture.PhotoCaptureResult result)
    {
        if (result.success)
        {
            DateTime now = DateTime.Now;
            string fileTime = string.Format("{0:MM-dd_HH-mm-ss}", now);
            string filename = "HL_capture_" + fileTime + ".jpg";
            imagePath = System.IO.Path.Combine(Application.persistentDataPath, filename);

            photoCaptureObject.TakePhotoAsync(imagePath, PhotoCaptureFileOutputFormat.JPG, OnCapturedPhotoToDisk);
        }
        else
        {
            Debug.LogError("Unable to start photo mode!");
        }
    }

    void OnCapturedPhotoToDisk(PhotoCapture.PhotoCaptureResult result)
    {
        if (result.success)
        {
            Debug.Log("Saved Photo to disk with path: " + imagePath);
            StartCoroutine(UploadImage());
            photoCaptureObject.StopPhotoModeAsync(OnStoppedPhotoMode);
        }
        else
        {
            Debug.Log("Failed to save Photo to disk");
        }
    }

    void OnStoppedPhotoMode(PhotoCapture.PhotoCaptureResult result)
    {
        photoCaptureObject.Dispose();
        photoCaptureObject = null;
    }


    IEnumerator UploadImage()
    {
        Debug.Log("Upload image Coroutine.");
        string endpoint = "upload_image";
        string requestURL = $"http://{serverIP}:{serverPort}/{endpoint}";

        WWWForm form = new WWWForm();
        if(!File.Exists(imagePath))
        {
            throw new FileNotFoundException("Image not found at path: " + imagePath);
        }

        form.AddBinaryData("image", File.ReadAllBytes(imagePath), Path.GetFileName(imagePath));
        
        using(UnityWebRequest request = UnityWebRequest.Post(requestURL, form))
        {
            yield return request.SendWebRequest();

            if(request.result != UnityWebRequest.Result.Success)
            {
                Debug.Log("Error in POST request. Response:");
                Debug.Log(request.downloadHandler.text);
                Debug.Log("Error: " + request.error);
            }
            else
            {
                Debug.Log("POST request succeeded. Response:");
                Debug.Log(request.downloadHandler.text);
                /* Parse into JSON and send to a function not in Coroutine to parse boxes using Unity event. */
                DetectionResults output = JsonConvert.DeserializeObject<DetectionResults>(request.downloadHandler.text);
                OnDetectionComplete.Invoke(output);
            }
        }

        /* When done, delete file. */
        try
        {
            File.Delete(imagePath);
        }
        catch(Exception e)
        {
            Debug.LogError("File could not be deleted: " + e.Message);
        }
    }
}
