using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using System.IO;
using UnityEngine.Windows.WebCam;
using UnityEngine.Networking;
using UnityEngine.Events;
using Newtonsoft.Json;
using System.Collections.Generic;
using MixedReality.Toolkit.UX;

/** Manages image -> object detection call to Python server. */
public class ObjectDetector : MonoBehaviour
{
    private PhotoCapture photoCaptureObject;
    private string imagePath;
    private float startPhotoTime = 0f;
    private float startRequestTime = 0f;
    private float startDrawingTime = 0f;
    private AppController appController;
    private SpatialMapping spatialMapper;
    private PhotoCaptureFrame currFrame;
    private Sprite currImage;

    //[SerializeField]
    //private string serverIP;
    //[SerializeField]
    //private string serverPort;

    [SerializeField]
    private GameObject inputField;
    [SerializeField]
    private string detectorEndpoint = "upload_image";
    [SerializeField]
    private GameObject scanDialogBox;


    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    [Serializable]
    public class DetectionResults
    {
        public string action { get; set; }
        public List<float> center { get; set; }
    }
    public Resolution imageResolution;

    //[Serializable]
    //private class UnityDetectionEvent : UnityEvent<DetectionResults, int>
    //{}

    ///* Action basically creates a delegate (function holder) and event in one, with parameter DetectionResults. */
    //public UnityDetectionEvent OnDetectionComplete;


    // Start is called before the first frame update
    void Start()
    {
        appController = GetComponent<AppController>();
        spatialMapper = appController.spatialMapper;
        /* Resolution choices in: https://learn.microsoft.com/en-us/windows/mixed-reality/develop/advanced-concepts/locatable-camera-overview */
        imageResolution.width = 1280;
        imageResolution.height = 720;
        //if (OnDetectionComplete == null)
        //{
        //    OnDetectionComplete = new UnityDetectionEvent();
        //}
        //OnDetectionComplete.AddListener(handleDetectionResults);

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
        else if(action == "pick up")
        {
            return VisualController.Hand.Pickup;
        }
        else if(action == "place the picked up object at this location")
        {
            return VisualController.Hand.PutDown;
        }
        else if(action == "pull")
        {
            return VisualController.Hand.Pull;
        }

        return VisualController.Hand.Error;
    }


    public void handleDetectionResults(DetectionResults results, int instructionNum, int pictureNum)
    {
        if (results.center.Count > 0 && results.action.Length > 0)
        {
            Resolution cameraRes = new Resolution();
            /* Get camera resolution for the camera that took this instruction's picture. */
            cameraRes.width = appController.spatialMapper.savedCameras[instructionNum][pictureNum].pixelWidth;
            cameraRes.height = appController.spatialMapper.savedCameras[instructionNum][pictureNum].pixelHeight;
            Vector2 objectLocation = TransformBoxToScreenPixels(new Vector2(results.center[0], results.center[1]), cameraRes);

            VisualController.Hand visual = GetHandVisual(results.action);
            if (visual == VisualController.Hand.Error)
            {
                Debug.Log("Invalid action returned from model");
            }
            else
            {
                //spatialMapper.TestPlaceHandFromMesh(objectLocation, visual, true);
                spatialMapper.StoreMapFromMesh(instructionNum, pictureNum, objectLocation, visual, false);
                spatialMapper.ClearCameraState(instructionNum, pictureNum);
                //spatialMapper.ClearState();
            }
        }
        else
        {
            Debug.Log("No boxes were found for object or action was empty");
        }

        //Debug.Log($"Visual time: {Time.realtimeSinceStartup - startDrawingTime} s");
    }

    /* Convert the box pixel location to a location on Hololens view */
    private Vector2 TransformBoxToScreenPixels(Vector2 imagePixels, Resolution cameraResolution)
    {
        /* Image taken from Hololens has different resolution than view does */
        float widthScale = (float)cameraResolution.width / (float)imageResolution.width;
        float heightScale = (float)cameraResolution.height / (float)imageResolution.height;
        float x_coord = imagePixels.x * widthScale;
        /* box is returned as (x, y) from top-left, Unity uses bottom-left as (0, 0). Conversion: */
        float y_coord = cameraResolution.height - (imagePixels.y * heightScale); 

        return new Vector2(x_coord, y_coord);
    }

    /* Called on button press right now. Now by voice command in AppController */
    public void RunObjectDetector()
    {
        Debug.Log("Starting photo capture, hold head where you want to take picture...");
        startPhotoTime = Time.realtimeSinceStartup;
        StartCoroutine(appController.UpdateCenterText(true, "Taking a picture, hold your head still."));
        PhotoCapture.CreateAsync(false, OnPhotoCaptureCreated);
    }

    void OnPhotoCaptureCreated(PhotoCapture captureObject)
    {
        photoCaptureObject = captureObject;

        //Debug.Log("Resolutions:");
        //foreach(Resolution res in PhotoCapture.SupportedResolutions)
        //{
        //    Debug.Log($"{res.width} x {res.height}");
        //}
        //Resolution photoResolution = PhotoCapture.SupportedResolutions.OrderByDescending((res) => res.width * res.height).First();
        //Resolution photoResolution = new Resolution();
        //photoResolution.width = 1280;
        //photoResolution.height = 720; 
        //imageResolution = photoResolution;
        //Debug.Log("Image Width: " + photoResolution.width);
        //Debug.Log("Image Height: " + photoResolution.height);
        CameraParameters c = new CameraParameters();
        c.hologramOpacity = 0.0f;
        c.cameraResolutionWidth = imageResolution.width;
        c.cameraResolutionHeight = imageResolution.height;
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
            imagePath = Path.Combine(Application.persistentDataPath, filename);

            //photoCaptureObject.TakePhotoAsync(imagePath, PhotoCaptureFileOutputFormat.JPG, OnCapturedPhotoToDisk);
            photoCaptureObject.TakePhotoAsync(OnCapturedPhotoToMemory);
        }
        else
        {
            Debug.LogError("Unable to start photo mode!");
        }
    }


    public void ConfirmScanPicture()
    {
        /* Store camera state from this picture into camera list for raycasting later. */
        appController.spatialMapper.StoreState(appController.instructionController.currentInstruction, currFrame);
        byte[] imageBytes = ImageConversion.EncodeToJPG(currImage.texture);
        /* Save photo to disk */
        File.WriteAllBytes(imagePath, imageBytes);
        StartCoroutine(UploadImage(imagePath, appController.instructionController.currentInstruction, appController.instructionController.currentPictureNum));
        /* Move onto next picture in scanning phase. */
        appController.ScanNextPicture();
    }


    void OnCapturedPhotoToMemory(PhotoCapture.PhotoCaptureResult result, PhotoCaptureFrame frame)
    {
        if (result.success)
        {
            currFrame = frame;
            StartCoroutine(appController.UpdateCenterText(false, "Done"));
            Debug.Log($"Photo capture time: {Time.realtimeSinceStartup - startPhotoTime} s");
            /* Saving frame to be able to extract camera details for spatial mapper. */
            //spatialMapper.StoreState(frame);
            Texture2D tex = new Texture2D(imageResolution.width, imageResolution.height, TextureFormat.RGB24, false);
            frame.UploadImageDataToTexture(tex);
            currImage = Sprite.Create(tex, new Rect(0.0f, 0.0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100.0f);
            appController.DisplayImageConfirmation(scanDialogBox, currImage);
        }
        else
        {
            Debug.Log("Failed to save Photo to memory");
        }

        photoCaptureObject.StopPhotoModeAsync(OnStoppedPhotoMode);
    }


    void OnStoppedPhotoMode(PhotoCapture.PhotoCaptureResult result)
    {
        photoCaptureObject.Dispose();
        photoCaptureObject = null;
    }


    public string GetServerURL()
    {
        string text = inputField.GetComponent<MRTKUGUIInputField>().text;
        //Debug.Log("Server URL" + text);
        return text;
    }


    public IEnumerator UploadImage(string imagePath, int currInstruction, int currPicture)
    {
        startRequestTime = Time.realtimeSinceStartup;
        ////Debug.Log("Upload image Coroutine.");
        string serverURL = GetServerURL();
        string requestURL = $"http://{serverURL}/{detectorEndpoint}";

        WWWForm form = new WWWForm();
        if(!File.Exists(imagePath))
        {
            throw new FileNotFoundException("Image not found at path: " + imagePath);
        }

        form.AddBinaryData("image", File.ReadAllBytes(imagePath), "hololens_image.jpg");
        
        if(currInstruction > appController.instructionController.instructions.Count)
        {
            throw new ArgumentOutOfRangeException("Current instruction (" + currInstruction + ") should not be greater than number of instructions");
        }
        form.AddField("instructionNum", currInstruction);
        form.AddField("pictureNum", currPicture);

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
                //OnDetectionComplete.Invoke(output, currInstruction);
                handleDetectionResults(output, currInstruction, currPicture);
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
