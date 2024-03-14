using MixedReality.Toolkit;
using MixedReality.Toolkit.Subsystems;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/** Controls state of the app, as well as what to display to user. */
public class AppController : MonoBehaviour
{
    private InstructionController instructionController;
    private ObjectDetector objectDetector;

    [SerializeField]
    private TextMeshProUGUI instructionLabel;

    public static AppState appState { get; set; }

    public enum AppState
    {
        INIT,
        OPERATOR,
        USER_PRESCAN,
        USER
    }

    // Start is called before the first frame update
    void Start()
    {
        appState = AppState.INIT;
        instructionController = GameObject.Find("InstructionController").GetComponent<InstructionController>();
        objectDetector = GameObject.Find("ObjectDetector").GetComponent<ObjectDetector>();

        /* Set up keyword speech recognition. */
        var speechRecognition = XRSubsystemHelpers.GetFirstRunningSubsystem<KeywordRecognitionSubsystem>();

        if(speechRecognition != null)
        {
            /* Operator phase voice commands. */
            speechRecognition.CreateOrGetEventForKeyword("operator picture").AddListener(HandleOperator);
            speechRecognition.CreateOrGetEventForKeyword("operator done").AddListener(DoneOperator);
            speechRecognition.CreateOrGetEventForKeyword("operator next").AddListener(OperatorNextInstruction);
            /* Prescan phase voice commands. */
            speechRecognition.CreateOrGetEventForKeyword("scan next").AddListener(ScanNextInstruction);
            speechRecognition.CreateOrGetEventForKeyword("scan picture").AddListener(UserPrescan);
            speechRecognition.CreateOrGetEventForKeyword("scan done").AddListener(DonePrescan);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if(instructionController.instructionsReceived)
        {
            DisplayCurrentInstruction();
            Debug.Log("OPERATOR state");
            appState = AppState.OPERATOR;
            instructionController.instructionsReceived = false;
        }
    }


    /** Called from button click currently. Gets instructions from server. */
    public void StartOperator()
    {
        instructionController.StartGetInstructions();
        InstructionController.currentInstruction = 0;
    }


    /** Called by voice command "Operator Next" during OPERATOR state. */
    private void OperatorNextInstruction()
    {
        if (appState == AppState.OPERATOR && 
            InstructionController.currentInstruction < (InstructionController.instructions.Count - 1))
        {
            InstructionController.currentInstruction++;
            DisplayCurrentInstruction();
        }
    }


    /** Called by voice command "Scan Next" during USER_PRESCAN state. */
    private void ScanNextInstruction()
    {
        if (appState == AppState.USER_PRESCAN &&
           InstructionController.currentInstruction < (InstructionController.instructions.Count - 1))
        {
            InstructionController.currentInstruction++;
            DisplayCurrentInstruction();
        }
    }


    /** Called by voice command "Operator Picture" during OPERATOR state. */
    private void HandleOperator()
    {
        /* Steps:
         * - Go through each instruction i
         * - Allow user to take picture(s) for each instruction
         * - User manually indicates when they are done with instruction by saying "Next Instruction"
         */

        if(appState == AppState.OPERATOR)
        {
            instructionController.RunInstructionParser();
        }
    }

    /** Operator is complete when they say "Operator Done" during OPERATOR state. */
    private void DoneOperator()
    {
       if(appState == AppState.OPERATOR)
        {
            Debug.Log("USER PRESCAN state");
            appState = AppState.USER_PRESCAN;
            InstructionController.currentInstruction = 0;
            DisplayCurrentInstruction();
        }
    }


    private void DisplayCurrentInstruction()
    {
        /* Display current instruction to user. */
        string currInstruction = InstructionController.instructions[InstructionController.currentInstruction];
        string instructionText = "Instruction " + (InstructionController.currentInstruction + 1) + ": " + currInstruction;
        Debug.Log(instructionText);
        instructionLabel.SetText(instructionText);
    }


    
    /** Called by voice command "Scan Picture" in USER_PRESCAN state. */
    private void UserPrescan()
    {
        if (appState == AppState.USER_PRESCAN)
        {
            objectDetector.RunObjectDetector();
        }
    }


    /** Called by voice command "Scan Done" in USER_PRESCAN state. */
    private void DonePrescan()
    {
       if(appState == AppState.USER_PRESCAN)
        {
            Debug.Log("USER state");
            appState = AppState.USER;
        }
    }
}
