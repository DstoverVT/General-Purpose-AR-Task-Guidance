using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System.Collections;
using UnityEngine.Windows.WebCam;
using System.IO;
using MixedReality.Toolkit.UX;

/** Gets and stores instructions from Python server. */
public class InstructionController : MonoBehaviour
{
    private AppController appController;
    private PhotoCapture photoCaptureObject;
    private string imagePath;
    public Resolution imageResolution;
    /** This file is stored on Hololens to contain the filenames of the pictures taken by the operator for the last
     * instruction set. They are stored on each line in a text file, and overwritten when new instruction pictures are taken. */
    private string fileStoragePath;
    [SerializeField]
    private string fileStorageName = "instruction_pictures.json";
    private float startRequestTime = 0f;
    private PhotoCaptureFrame currFrame;
    private Sprite currImage;

    [SerializeField]
    private string instructionsEndpoint = "get_instructions";
    [SerializeField]
    private string newInstructionsEndpoint = "new_instructions";
    [SerializeField]
    private string updateInstructionsEndpoint = "update_instructions";
    [SerializeField]
    private string parserEndpoint = "parse_instruction";
    [SerializeField]
    private GameObject pictureDialogBox;

    [HideInInspector]
    public List<string> instructions { get; set; }
    [HideInInspector]
    public int currentInstruction = 0;
    [HideInInspector]
    public int currentPictureNum = 0;
    /* List that holds whether each instruction is processing or not. 
     * Need to be indexed by instruction number to ensure no concurrent set conditions occur. 
     * Instruction index is true if it is currently being processed by parser. */
    [HideInInspector]
    public List<bool> instructionProcessing = new List<bool>();
    /** Data structure that holds path to images for each instruction as such:
     * [["path1.jpg", "path2.jpg"], ["path3.jpg], ...]
     * This is stored in a Hololens text file as JSON. 
     * Each entry in the outer array corresponds to a different instruction. */
    [HideInInspector]
    public List<List<string>> instructionImagePaths;

    // Start is called before the first frame update
    void Start()
    {
        fileStoragePath = Path.Combine(Application.persistentDataPath, fileStorageName);
        appController = GetComponent<AppController>(); 

        if(!File.Exists(fileStoragePath))
        {
            /* Create file to store image paths if does not exist on Hololens. */
            using (FileStream f = File.Create(fileStoragePath)) { }
        }

        instructionImagePaths = new List<List<string>>();
        imageResolution.width = 1280;
        imageResolution.height = 720;
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
            string testPath = Path.Combine(Application.dataPath, "Materials", "scan_1280_720.jpg");
            AddImagePath(currentInstruction, testPath, appController.updateMode);
        }
        StartCoroutine(appController.UpdateCenterText(true, "Taking a picture, hold your head still."));
        if (!Application.isEditor)
        {
            instructionProcessing[currentInstruction] = true;
        }
        PhotoCapture.CreateAsync(false, OnPhotoCaptureCreated);
    }


    private void InitializeProcessingList()
    {
        if (instructionProcessing.Count == 0)
        {
            for (int i = 0; i < instructions.Count; i++)
            {
                /* Initialize all instructions to not be processing currently. */
                instructionProcessing.Add(false);
            }
        }
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
                InitializeProcessingList();
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


    IEnumerator SendPictureToParser(string currImagePath, int currInstruction, int currentPicture)
    {
        startRequestTime = Time.realtimeSinceStartup;
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
                Debug.Log("Done: Picture and instruction have been parsed by GPT and objects detected.");
                /* Notify that instruction is done processing. */
                Debug.Log($"Full request time: {Time.realtimeSinceStartup - startRequestTime} s");
                //Debug.Log("POST request succeeded. Response:");
                //Debug.Log(request.downloadHandler.text);
                /* Parse into JSON and send to a function not in Coroutine to parse boxes using Unity event. */
                ObjectDetector.DetectionResults output = JsonConvert.DeserializeObject<ObjectDetector.DetectionResults>(request.downloadHandler.text);
                //OnDetectionComplete.Invoke(output, currInstruction);
                appController.objectDetector.handleDetectionResults(output, currInstruction, currentPicture);
            }

            instructionProcessing[currInstruction] = false;
        } 

    }


    public void OperatorImageCancelled()
    {
        instructionProcessing[currentInstruction] = false;
    }



    void OnPhotoCaptureCreated(PhotoCapture captureObject)
    {
        photoCaptureObject = captureObject;

        //Resolution photoResolution = PhotoCapture.SupportedResolutions.OrderByDescending((res) => res.width * res.height).First();
        //Resolution photoResolution = new Resolution();
        //photoResolution.width = 1280;
        //photoResolution.height = 720;
        //imageResolution = photoResolution;

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


    public void ConfirmOperatorImage()
    {
        /* Store camera state from this picture into camera list for raycasting later. */
        appController.spatialMapper.StoreState(currentInstruction, currFrame);
        /* Save photo to disk */
        byte[] imageBytes = ImageConversion.EncodeToJPG(currImage.texture);
        File.WriteAllBytes(imagePath, imageBytes);
        AddImagePath(currentInstruction, imagePath, appController.updateMode); 
        StartCoroutine(SendPictureToParser(imagePath, currentInstruction, currentPictureNum));
        /* After picture is taken increase picture number. */
        currentPictureNum++;
    }


    void OnCapturedPhotoToMemory(PhotoCapture.PhotoCaptureResult result, PhotoCaptureFrame frame)
    {
        if (result.success)
        {
            currFrame = frame;
            StartCoroutine(appController.UpdateCenterText(false, "Done"));
            Texture2D tex = new Texture2D(imageResolution.width, imageResolution.height, TextureFormat.RGB24, false);
            frame.UploadImageDataToTexture(tex);
            currImage = Sprite.Create(tex, new Rect(0.0f, 0.0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100.0f);
            appController.DisplayImageConfirmation(pictureDialogBox, currImage);
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
            /* Delete files and clear them. */
            List<string> currInstr = instructionImagePaths[instructionNum];
            foreach(string path in currInstr)
            {
                try
                {
                    if (!Application.isEditor)
                    {
                        File.Delete(path);
                    }
                }
                catch (Exception e)
                {
                    Debug.Log("File could not be deleted: " + e.Message);
                }
            }
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
            StartCoroutine(SendPictureToParser(imagePath, currentInstruction, currentPictureNum));
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
