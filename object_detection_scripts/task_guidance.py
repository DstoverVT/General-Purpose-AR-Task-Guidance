import json
import os

from instruction_parser import parse_instruction, possible_actions
from object_detection import DetectionException, ObjectDetectionInterface


OUTPUT_FILE = "parser_output.json"


def delete_images(*image_paths):
    """Remove files that were saved."""
    for image in image_paths:
        if os.path.exists(image):
            os.remove(image)


def get_objects_from_json(json_data: dict[str, list[str]]) -> tuple[str, str]:
    """Return objects as promptable string for GroundingDINO from JSON file.
    JSON input or file should be structured as a dict[str, list[str]] with fields "objects" and "actions" mapped to lists.

    For example, if object list is "objects": ["one", "two", "three"], outputs "one . two . three"
    """
    try:
        object_list = json_data["objects"]
        object_prompt = " . ".join(object_list)
        action_list = json_data["actions"]
        first_action = action_list[0]
    # Catch if objects and actions fields don't exist in JSON
    except (ValueError, FileNotFoundError) as e:
        raise DetectionException(e)

    return object_prompt, first_action


def detect_objects_in_image(
    detector: ObjectDetectionInterface,
    image_path: str,
    thres1: float,
    thres2: float,
    instruction_num: int,
) -> tuple[tuple[float, float], str]:
    """Run object detection on image.

    Args:
    - image_path: Image to run object detection on
    - thres1: Bounding box lower confidence for cropping
    - thres2: Bounding box Lower confidence for object detection on cropped image
    """
    # Get JSON from current instruction_num from output file
    with open(OUTPUT_FILE, "r") as file:
        json_data = json.load(file)
    num = str(instruction_num)
    instruction_json = json_data[num]

    print(f"Running object detection on instruction {num}...")
    object_prompt, action = get_objects_from_json(instruction_json)
    center, top_left_coord, cropped_image = detector.run_object_detection_with_crop(
        image_path,
        object_prompt,
        thres1,
        thres2,
    )

    if center is None:
        return None, ""

    original_image_box_center = (
        top_left_coord[0] + center[0],
        top_left_coord[1] + center[1],
    )

    delete_images(cropped_image)

    return original_image_box_center, action


def get_instructions_from_file() -> list[str]:
    """Reads 'instructions.txt' and outputs list of instructions from the file."""
    try:
        instruction_file = "instructions.txt"
        with open(instruction_file, "r") as f:
            lines = f.readlines()
        # Clear parser output JSON file at beginning of new program
        open(OUTPUT_FILE, "w").close()
    except FileNotFoundError as e:
        raise DetectionException(f"{e}. Error reading 'instructions.txt'")

    if len(lines) == 0:
        raise DetectionException("Need at least one instruction in instructions.txt")

    instructions = [line.strip() for line in lines]
    return instructions


def get_cropped_image(
    detector: ObjectDetectionInterface,
    threshold: float,
    filepath: str,
    json_input: dict[str, list[str]],
) -> str:
    """Runs object detection on 'filepath' to crop an image to relevant objects.

    Returns:
    - Filename of new saved cropped image
    """
    object_prompt, _ = get_objects_from_json(json_input)
    _, boxes, _, _ = detector.run_object_detection(filepath, object_prompt, threshold)

    # Run object detection again after cropping image to largest box
    region = detector.region_containing_all_boxes(boxes)
    cropped_image_path = detector.crop_image_to_box(region, filepath)
    return cropped_image_path


def add_json_to_output_file(
    json_data: dict[str, list[str]], instruction_num: int
) -> bool:
    """Add a parsed instruction (json_data) to output parser json file containing all parsed instructions.

    Format of parser output file: dict[str, dict[str, list[str]]]
    {
    "1": { "objects": list[str], "actions": list[str] },
    "2": { "objects": list[str], "actions": list[str] },
    ...
    }

    "1" and "2" correspond to instruction numbers that are parsed.

    Returns:
    - False if need to re-run GPT due to invalid action name, else True
    """
    current_json = {}
    with open(OUTPUT_FILE, "r") as file:
        # Only decode JSON if file is not empty
        if len(file.read(1)) != 0:
            # Return to beginning of file
            file.seek(0)
            current_json: dict[str, dict[str, list[str]]] = json.load(file)

    num = str(instruction_num)

    # Check if key (instruction number) already exists
    if num in current_json:
        # If so, append json to current instruction
        curr_instruction = current_json[num]
        for obj in json_data["objects"]:
            curr_instruction["objects"].append(obj)
        for action in json_data["actions"]:
            if action not in possible_actions:
                print("**Re-running GPT, it output an invalid action")
                return False
            curr_instruction["actions"].append(action)
    else:
        # New instruction, add output to json
        current_json[num] = json_data

    with open(OUTPUT_FILE, "w") as file:
        json.dump(current_json, file, indent=4)

    return True


def get_previous_gpt_outputs(instructions: list[str]):
    """Returns previous responses from parser output JSON file."""
    with open(OUTPUT_FILE, "r") as file:
        # Make sure file isn't empty
        if len(file.read(1)) != 0:
            # Return to beginning of file
            file.seek(0)
            all_outputs = json.load(file)
        else:
            return [], []

    previous_instructions = [instructions[int(num)] for num in all_outputs.keys()]
    # Store string version of each instruction response
    previous_outputs = [json.dumps(output) for output in all_outputs.values()]

    return previous_instructions, previous_outputs


def instruction_gpt_calls(
    detector: ObjectDetectionInterface,
    instructions: list[str],
    instruction_num: int,
    threshold: float,
    image_file: str,
):
    """Sends an instruction and image to be parsed by GPT-4V.

    Args:
    - instruction: String instruction to parse
    - threshold: Threshold for cropping image using GroundingDINO
    - image_file: Image to send to GPT-4V

    Output is returned in JSON format.
    """
    try:
        instruction = instructions[instruction_num]
        print(f"Parsing instruction: {instruction}...")
        # Get previous info to give to GPT for conversation history
        previous_instructions, previous_responses = get_previous_gpt_outputs(
            instructions
        )

        json_output: dict[str, list[str]] = parse_instruction(
            instruction, image_file, previous_instructions, previous_responses
        )
        print("-------- GPT OUTPUT 1: ----------")
        print(json.dumps(json_output, indent=4))

        valid_actions = False
        # Call GPT until the action it outputs is valid (in possible_actions)
        while not valid_actions:
            cropped_image = get_cropped_image(
                detector, threshold, image_file, json_output
            )
            parsed_output = parse_instruction(
                instruction, cropped_image, previous_instructions, previous_responses
            )
            print("-------- GPT OUTPUT 2: ----------")
            print(json.dumps(parsed_output, indent=4))

            # Add output to parser output JSON file
            valid_actions = add_json_to_output_file(parsed_output, instruction_num)
    except Exception as e:
        raise DetectionException(f"{e}. Error with instruction parser (GPT)")

    # delete_images(cropped_image)
