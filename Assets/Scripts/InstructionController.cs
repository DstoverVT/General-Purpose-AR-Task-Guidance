using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System.Collections;
using UnityEngine.Windows.WebCam;
using System.IO;

/** Gets and stores instructions from Python server. */
public class InstructionController : MonoBehaviour
{
    private AppController appController;
    private PhotoCapture photoCaptureObject;
    private string imagePath;
    private Resolution imageResolution;
    /** Data structure that holds path to images for each instruction as such:
     * [["path1.jpg", "path2.jpg"], ["path3.jpg], ...]
     * This is stored in a Hololens text file as JSON. 
     * Each entry in the outer array corresponds to a different instruction. */
    public List<List<string>> instructionImagePaths;

    [SerializeField]
    private string instructionsEndpoint = "get_instructions";
    [SerializeField]
    private string newInstructionsEndpoint = "new_instructions";
    [SerializeField]
    private string updateInstructionsEndpoint = "update_instructions";
    [SerializeField]
    private string parserEndpoint = "parse_instruction";
    /** This file is stored on Hololens to contain the filenames of the pictures taken by the operator for the last
     * instruction set. They are stored on each line in a text file, and overwritten when new instruction pictures are taken. */
    [SerializeField]
    private string fileStoragePath;

    public List<string> instructions { get; set; }
    public int currentInstruction = 0;

    // Start is called before the first frame update
    void Start()
    {
        fileStoragePath = Path.Combine(Application.persistentDataPath, "instruction_pictures.json");
        appController = GameObject.Find("AppController").GetComponent<AppController>(); 

        if(!File.Exists(fileStoragePath))
        {
            /* Create file to store image paths if does not exist on Hololens. */
            using (FileStream f = File.Create(fileStoragePath)) { }
        }

        instructionImagePaths = new List<List<string>>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    public void StartGetInstructions(bool operator_changed, bool update)
    {
        StartCoroutine(GetInstructions(operator_changed, update));
    }


    public void RunInstructionParser()
    {
        Debug.Log("Starting photo capture, hold head where you want to take picture...");
        /* Test */
        if (Application.isEditor)
        {
            string testPath = Path.Combine(Application.dataPath, "Materials", "humidifier.jpg");
            AddImagePath(currentInstruction, testPath, appController.updateMode);
        }
        StartCoroutine(appController.UpdateCenterText(true, "Taking a picture, hold your head still."));
        PhotoCapture.CreateAsync(false, OnPhotoCaptureCreated);
    }


    IEnumerator GetInstructions(bool operator_changed, bool update)
    {
        string serverURL = appController.objectDetector.GetServerURL();
        string endpoint;
        if (update)
        {
            endpoint = updateInstructionsEndpoint;
        }
        else
        {
            endpoint = operator_changed ? newInstructionsEndpoint : instructionsEndpoint;
        }
        string requestURL = $"http://{serverURL}/{endpoint}";

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
                instructions = JsonConvert.DeserializeObject<List<string>>(request.downloadHandler.text);
                Debug.Log("Instructions: " + instructions);
                /* Once instructions are received, set flag. */
                if (operator_changed)
                {
                    appController.onNewInstructionsReceived.Invoke();
                }
                else
                {
                    appController.onInstructionsReceived.Invoke();
                }
            }
        }
    }


    IEnumerator SendPictureToParser(string currImagePath, int currInstruction)
    {
        string serverURL = appController.objectDetector.GetServerURL();
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
            imagePath = Path.Combine(Application.persistentDataPath, filename);

            photoCaptureObject.TakePhotoAsync(imagePath, PhotoCaptureFileOutputFormat.JPG, OnCapturedPhotoToDisk);
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
            StartCoroutine(appController.UpdateCenterText(false, "Done"));
            /* Save photo to disk */
            Texture2D imageTexture = new Texture2D(imageResolution.width, imageResolution.height, TextureFormat.RGB24, false);
            frame.UploadImageDataToTexture(imageTexture);
            byte[] imageBytes = ImageConversion.EncodeToJPG(imageTexture);
            Destroy(imageTexture);
            File.WriteAllBytes(imagePath, imageBytes);
            AddImagePath(currentInstruction, imagePath, appController.updateMode); 
            StartCoroutine(SendPictureToParser(imagePath, currentInstruction));
        }
        else
        {
            Debug.Log("Failed to save Photo to memory");
        }

        photoCaptureObject.StopPhotoModeAsync(OnStoppedPhotoMode);
    }


    /** Add image path to correct instruction List. */
    void AddImagePath(int instructionNum, string filePath, bool update)
    {
        /* Create initial List if instruction index does not exist yet. */
        if (instructionNum >= instructionImagePaths.Count)
        {
            int numToAdd = (instructionNum + 1) - instructionImagePaths.Count;
            for (int i = 0; i < numToAdd; i++)
            {
                instructionImagePaths.Add(new List<string>());
            }
        }

        /* On first time updating, clear old instruction paths. */
        if(update && !appController.updatedInstructions.Contains(instructionNum))
        {
            instructionImagePaths[instructionNum].Clear();
            appController.updatedInstructions.Add(instructionNum);
        }
        instructionImagePaths[instructionNum].Add(filePath);
    }

    
    /** Store image paths in JSON file on Hololens. */
    public void StoreImagePathsJson()
    {
        string pathJson = JsonConvert.SerializeObject(instructionImagePaths, Formatting.Indented);
        File.WriteAllText(fileStoragePath, pathJson);
    }


    /** Load image file paths from JSON file into List object. */
    public void LoadImagePathsJson()
    {
        string fileText = File.ReadAllText(fileStoragePath);
        instructionImagePaths = JsonConvert.DeserializeObject<List<List<string>>>(fileText);
    }


    /** Clear contents of storage file, for new instruction set.*/
    public void ClearStorageFile()
    {
        /* Delete all files contained in storage file. */
        string fileText = File.ReadAllText(fileStoragePath);
        /* Only do so if current file is not empty. */
        if (!fileText.Equals(string.Empty))
        {
            List<List<string>> files = JsonConvert.DeserializeObject<List<List<string>>>(fileText);

            foreach (List<string> list in files)
            {
                foreach (string filename in list)
                {
                    /* Delete all files. */
                    try
                    {
                        if (!Application.isEditor)
                        {
                            File.Delete(filename);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.Log("File could not be deleted: " + e.Message);
                    }
                }
            }

            /* Clear storage file contents. */
            File.WriteAllText(fileStoragePath, string.Empty);
        }

        /* Clear instruction images. */
        instructionImagePaths.Clear();
    }


    void OnCapturedPhotoToDisk(PhotoCapture.PhotoCaptureResult result)
    {
        if (result.success)
        {
            StartCoroutine(appController.UpdateCenterText(false, "Done"));
            Debug.Log("Saved Picture To Disk, can move head now");
            Debug.Log("Sending picture to GPT...");
            AddImagePath(currentInstruction, imagePath, appController.updateMode); 
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

/*
    public int GetNumOfInstructionImages()
    {
        int total = 0;
        if (instructionImagePaths.Count > 0)
        {
            foreach (List<string> images in instructionImagePaths)
            {
                total += images.Count;
            }
        }

        return total;
    }
*/
}
