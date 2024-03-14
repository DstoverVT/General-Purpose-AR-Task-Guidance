using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System.Collections;
using UnityEngine.Windows.WebCam;
using System.IO;
using UnityEngine.Android;

/** Gets and stores instructions from Python server. */
public class InstructionController : MonoBehaviour
{
    private ObjectDetector objectDetector;
    private PhotoCapture photoCaptureObject;
    private string imagePath;
    private Resolution imageResolution;

    [SerializeField]
    private string instructionsEndpoint = "get_instructions";
    [SerializeField]
    private string parserEndpoint = "parse_instruction";


    [Serializable]
    private class Instructions
    {
        public List<string> instructionsList { get; set; }
    }

    public static List<string> instructions { get; set; }
    public static int currentInstruction = 0;
    public bool instructionsReceived { get; set; } = false;

    // Start is called before the first frame update
    void Start()
    {
        objectDetector = GameObject.Find("ObjectDetector").GetComponent<ObjectDetector>(); 
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    public void StartGetInstructions()
    {
        instructionsReceived = false;
        StartCoroutine(GetInstructions());
    }


    public void RunInstructionParser()
    {
        Debug.Log("Starting photo capture, hold head where you want to take picture...");
        PhotoCapture.CreateAsync(false, OnPhotoCaptureCreated);
    }


    IEnumerator GetInstructions()
    {
        string serverURL = objectDetector.GetServerURL();
        string requestURL = $"http://{serverURL}/{instructionsEndpoint}";

        using (UnityWebRequest request = UnityWebRequest.Get(requestURL))
        {
            yield return request.SendWebRequest();

            if(request.result != UnityWebRequest.Result.Success)
            {
                Debug.Log("Error in GET request. Response:");
                Debug.Log(request.downloadHandler.text);
                Debug.Log("Error: " + request.error);
            }
            else
            {
                Instructions output = JsonConvert.DeserializeObject<Instructions>(request.downloadHandler.text);
                instructions = output.instructionsList;
                Debug.Log("Instructions: " + instructions);
                /* Once instructions are received, set flag. */
                instructionsReceived = true;
            }
        }
    }


    IEnumerator SendPictureToParser(string currImagePath, int currInstruction)
    {
        string serverURL = objectDetector.GetServerURL();
        string requestURL = $"http://{serverURL}/{parserEndpoint}";

        WWWForm form = new WWWForm();
        if (!File.Exists(currImagePath))
        {
            throw new FileNotFoundException("Image not found at path: " + currImagePath);
        }

        form.AddBinaryData("image", File.ReadAllBytes(currImagePath), "hololens_image.jpg");

        if(currInstruction > instructions.Count)
        {
            throw new ArgumentOutOfRangeException("Current instruction (" + currInstruction + ") should not be greater than number of instructions");
        }
        form.AddField("instructionNum", currInstruction);

        using (UnityWebRequest request = UnityWebRequest.Post(requestURL, form))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.Log("Error in POST request. Response:");
                Debug.Log(request.downloadHandler.text);
                Debug.Log("Error: " + request.error);
            }
            else
            {
                Debug.Log("Done: Picture and instruction have been parsed by GPT.");
            }
                
        } 
    }



    void OnPhotoCaptureCreated(PhotoCapture captureObject)
    {
        photoCaptureObject = captureObject;

        Resolution photoResolution = PhotoCapture.SupportedResolutions.OrderByDescending((res) => res.width * res.height).First();
        imageResolution = photoResolution;
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
            Debug.Log("Saved Picture To Disk, can move head now");
            Debug.Log("Sending picture to GPT...");
            StartCoroutine(SendPictureToParser(imagePath, currentInstruction));
        }
        else
        {
            Debug.Log("Error saving picture to disk.");
        }

        photoCaptureObject.StopPhotoModeAsync(OnStoppedPhotoMode);
    }


    void OnStoppedPhotoMode(PhotoCapture.PhotoCaptureResult result)
    {
        photoCaptureObject.Dispose();
        photoCaptureObject = null;
    }
}
