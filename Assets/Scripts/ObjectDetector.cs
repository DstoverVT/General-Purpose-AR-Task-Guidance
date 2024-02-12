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
using MixedReality.Toolkit.UX;
using UnityEngine.UI;
using TMPro;
using Unity.VisualScripting;

public class ObjectDetector : MonoBehaviour
{
    private PhotoCapture photoCaptureObject;
    private SpatialMapping spatialMapper;
    private Resolution imageResolution;
    private string imagePath;
    private float startPhotoTime = 0f;
    private float startRequestTime = 0f;
    private float startDrawingTime = 0f;

    //[SerializeField]
    //private string serverIP;
    //[SerializeField]
    //private string serverPort;

    [SerializeField]
    private GameObject inputField;


    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    [Serializable]
    private class DetectionResults
    {
        public List<List<float>> boxes { get; set; }
        public List<string> phrases { get; set; }
        public List<float> confidence { get; set; }
        public string action { get; set; }
        public int threshold { get; set; }
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

        //Debug.Log("Camera resolution: " + spatialMapper.GetLastSavedCamera().pixelWidth + "x" + spatialMapper.GetLastSavedCamera().pixelHeight);
    }

    // Update is called once per frame
    void Update()
    {
    }

    private VisualController.Hand GetHandVisual(string handType)
    {
        string action = handType.ToLower();

        if(action == "press")
        {
            return VisualController.Hand.Press;
        }
        else if(action == "twist")
        {
            return VisualController.Hand.Twist;
        }

        return VisualController.Hand.Error;
    }

    int GetBestBoxIndex(List<float> confidences, List<List<float>> boxes, int threshold)
    {
        var bestBox = confidences
            .Zip(boxes, (confidence, box) => new { Confidence = confidence, Box = box })
            .Select((item, index) => new { item.Confidence, item.Box, Index = index })
            .Where(box => box.Box[2] < threshold && box.Box[3] < threshold)
            .OrderByDescending(box => box.Confidence)
            .FirstOrDefault();

        return bestBox.Index;
    }

    private void handleDetectionResults(DetectionResults results)
    {
        if (results.boxes.Count > 0)
        {
            /* Get box with highest confidence */
            int highestIdx = GetBestBoxIndex(results.confidence, results.boxes, results.threshold);
            Debug.Log("Box confidence: " + results.confidence[highestIdx]);
            Debug.Log("Box label: " + results.phrases[highestIdx]);
            //Debug.Log("First box: ");
            //Debug.Log(string.Join(", ", box1));
            List<float> bestBox = results.boxes[highestIdx];

            Vector2 objectLocation = TransformBoxToScreenPixels(new Vector2(bestBox[0], bestBox[1]));

            VisualController.Hand visual = GetHandVisual(results.action);
            if (visual == VisualController.Hand.Error)
            {
                Debug.Log("Invalid hand type returned from model");
            }
            else
            {
                spatialMapper.PlaceHandFromMesh(objectLocation, visual, SpatialMapping.spatialMeshLayer, true);
                spatialMapper.ClearState();
            }
        }
        else
        {
            Debug.Log("No boxes were found for object");
        }

        Debug.Log($"Visual time: {Time.realtimeSinceStartup - startDrawingTime} s");
    }

    /* Convert the box pixel location to a location on Hololens view */
    private Vector2 TransformBoxToScreenPixels(Vector2 imagePixels)
    {
        /* Image taken from Hololens has different resolution than view does */
        //float widthScale = (float)spatialMapper.GetLastSavedCamera().pixelWidth / (float)imageResolution.width;
        //float heightScale = (float)spatialMapper.GetLastSavedCamera().pixelHeight / (float)imageResolution.height;
        float widthScale = (float)spatialMapper.GetLastSavedCamera().pixelWidth / (float)imageResolution.width;
        float heightScale = (float)spatialMapper.GetLastSavedCamera().pixelHeight / (float)imageResolution.height;
        float x_coord = imagePixels.x * widthScale;
        /* box is returned as (x, y) from top-left, Unity uses bottom-left as (0, 0). Conversion: */
        float y_coord = spatialMapper.GetLastSavedCamera().pixelHeight - (imagePixels.y * heightScale); 

        return new Vector2(x_coord, y_coord);
    }

    /* Called on button press right now. */
    public void SaveImage()
    {
        Debug.Log("Starting photo capture, hold head where you want to take picture...");
        startPhotoTime = Time.realtimeSinceStartup;
        PhotoCapture.CreateAsync(false, OnPhotoCaptureCreated);
    }

    void OnPhotoCaptureCreated(PhotoCapture captureObject)
    {
        photoCaptureObject = captureObject;

        //int cameraWidth = spatialMapper.GetLastSavedCamera().pixelWidth;
        //int cameraHeight = spatialMapper.GetLastSavedCamera().pixelHeight;
        //Debug.Log("Unity Camera Width: " + cameraWidth);
        //Debug.Log("Unity Camera Height: " + cameraHeight);

        Resolution photoResolution = PhotoCapture.SupportedResolutions.OrderByDescending((res) => res.width * res.height).First();
        foreach(Resolution res in PhotoCapture.SupportedResolutions)
        {
            Debug.Log(res.width + "x" + res.height);
        }
        //Resolution cameraResolution = PhotoCapture.SupportedResolutions
        //    .FirstOrDefault((res) => res.width == cameraWidth && res.height == cameraHeight);
        imageResolution = photoResolution;
        //Debug.Log("Image Width: " + photoResolution.width);
        //Debug.Log("Image Height: " + photoResolution.height);

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

            //photoCaptureObject.TakePhotoAsync(imagePath, PhotoCaptureFileOutputFormat.JPG, OnCapturedPhotoToDisk);
            photoCaptureObject.TakePhotoAsync(OnCapturedPhotoToMemory);
        }
        else
        {
            Debug.LogError("Unable to start photo mode!");
        }
    }


    void OnCapturedPhotoToMemory(PhotoCapture.PhotoCaptureResult result, PhotoCaptureFrame frame)
    {
        if (result.success)
        {
            Debug.Log($"Photo capture time: {Time.realtimeSinceStartup - startPhotoTime} s");
            //Debug.Log("Saved Photo to disk with path: " + imagePath);
            spatialMapper.StoreState(frame);
            /* Save photo to disk */
            Texture2D imageTexture = new Texture2D(imageResolution.width, imageResolution.height, TextureFormat.RGB24, false);
            frame.UploadImageDataToTexture(imageTexture);
            byte[] imageBytes = ImageConversion.EncodeToJPG(imageTexture);
            Destroy(imageTexture);
            File.WriteAllBytes(imagePath, imageBytes);
            //List<byte> imageBytes = new List<byte>();
            //frame.CopyRawImageDataIntoBuffer(imageBytes);
            StartCoroutine(UploadImage());
            photoCaptureObject.StopPhotoModeAsync(OnStoppedPhotoMode);
        }
        else
        {
            Debug.Log("Failed to save Photo to memory");
        }
    }

    void OnStoppedPhotoMode(PhotoCapture.PhotoCaptureResult result)
    {
        photoCaptureObject.Dispose();
        photoCaptureObject = null;
    }


    string GetServerURL()
    {
        string text = inputField.GetComponent<MRTKUGUIInputField>().text;
        //Debug.Log("Server URL" + text);
        return text;
    }


    IEnumerator UploadImage()
    {
        startRequestTime = Time.realtimeSinceStartup;
        ////Debug.Log("Upload image Coroutine.");
        string endpoint = "upload_image";
        string serverURL = GetServerURL();
        string requestURL = $"http://{serverURL}/{endpoint}";

        WWWForm form = new WWWForm();
        if(!File.Exists(imagePath))
        {
            throw new FileNotFoundException("Image not found at path: " + imagePath);
        }

        form.AddBinaryData("image", File.ReadAllBytes(imagePath), "hololens_image.jpg");
        
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
                Debug.Log($"Full request time: {Time.realtimeSinceStartup - startRequestTime} s");
                //Debug.Log("POST request succeeded. Response:");
                //Debug.Log(request.downloadHandler.text);
                /* Parse into JSON and send to a function not in Coroutine to parse boxes using Unity event. */
                startDrawingTime = Time.realtimeSinceStartup;
                DetectionResults output = JsonConvert.DeserializeObject<DetectionResults>(request.downloadHandler.text);
                OnDetectionComplete.Invoke(output);
            }
        }

        /* When done, delete file. */
        try
        {
            File.Delete(imagePath);
        }
        catch (Exception e)
        {
            Debug.Log("File could not be deleted: " + e.Message);
        }
    }
}
