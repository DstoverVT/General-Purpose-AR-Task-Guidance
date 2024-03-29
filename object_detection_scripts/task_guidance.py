import json
import os

from instruction_parser import parse_instruction, possible_actions, pickup_actions
from object_detection import DetectionException, ObjectDetectionInterface


OUTPUT_FILE = "parser_output.json"
updated_instructions = []


def delete_images(*image_paths):
    """Remove files that were saved."""
    for image in image_paths:
        if os.path.exists(image):
            os.remove(image)


def get_objects_from_json(
    json_data: dict[str, list[str]], picture_num: int
) -> tuple[str, str]:
    """Return objects as promptable string for GroundingDINO from JSON file.
    JSON input or file should be structured as a dict[str, list[str]] with fields "objects" and "actions" mapped to lists.

    For example, if object list is "objects": ["one", "two", "three"], outputs "one . two . three"
    """
    object_list = json_data["objects"]
    object_prompt = object_list[picture_num]
    action_list = json_data["actions"]
    first_action = action_list[picture_num]

    return object_prompt, first_action


def detect_objects_from_json(
    detector: ObjectDetectionInterface,
    image_path: str,
    thres1: float,
    thres2: float,
    instruction_num: int,
    picture_num: int,
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
    object_prompt, action = get_objects_from_json(instruction_json, picture_num)
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


def get_instructions_from_file(clear_output: bool = False) -> list[str]:
    """Reads 'instructions.txt' and outputs list of instructions from the file."""
    instruction_file = "instructions.txt"
    with open(instruction_file, "r") as f:
        lines = f.readlines()
    # Clear parser output JSON file at beginning of new program
    if clear_output:
        open(OUTPUT_FILE, "w").close()

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
    object_prompt, _ = get_objects_from_json(json_input, 0)
    _, boxes, _, _ = detector.run_object_detection(filepath, object_prompt, threshold)

    # Signal to not run on cropped image if only 1 box is found.
    if boxes.shape[0] == 1:
        return ""

    # Run object detection again after cropping image to largest box
    region = detector.region_containing_all_boxes(boxes)
    cropped_image_path = detector.crop_image_to_box(region, filepath)
    return cropped_image_path


def verify_pickup_and_place(new_action: str, previous_action: str) -> str:
    """Ensures that an instruction using pickup and place outputs both."""
    # Ensure that previous and new actions aren't the same (both pick up or both place)
    verified_action = new_action
    if new_action in pickup_actions and previous_action in pickup_actions:
        if new_action == previous_action:
            for action in pickup_actions:
                # Force action to be the other action that was not selected (pickup or place)
                if verified_action != action:
                    verified_action = action
                    break
    return verified_action


def add_json_to_output_file(
    json_data: dict[str, list[str]], instruction_num: int, update: bool
) -> tuple[bool, str, str]:
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
    - "Object prompt" added to JSON
    - "Action" added to JSON
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
        # Only update instruction once, keep track of whether it was updated already
        if update and instruction_num not in updated_instructions:
            current_json[num] = json_data
            updated_instructions.append(instruction_num)
        else:
            # If so, append json to current instruction
            curr_instruction = current_json[num]
            for obj in json_data["objects"]:
                curr_instruction["objects"].append(obj)
            for action in json_data["actions"]:
                if action not in possible_actions:
                    print("**Re-running GPT, it output an invalid action")
                    return False, "", ""
                # Ensures that both pickup and place are output
                # Using [0] as index for action only works since current system only allows 2 object/actions max
                # Will have to change 0 to find what the previous action index is if system allows for >2 objects.
                verified_action = verify_pickup_and_place(
                    action, curr_instruction["actions"][0]
                )
                curr_instruction["actions"].append(verified_action)
    else:
        # New instruction, add output to json
        for action in json_data["actions"]:
            if action not in possible_actions:
                print("**Re-running GPT, it output an invalid action")
                return False, "", ""
        current_json[num] = json_data

    with open(OUTPUT_FILE, "w") as file:
        json.dump(current_json, file, indent=4)

    added_object = current_json[num]["objects"][-1]
    added_action = current_json[num]["actions"][-1]
    return True, added_object, added_action


def get_previous_gpt_outputs(
    instructions: list[str], instruction_num: int, update: bool
):
    """Returns previous responses from parser output JSON file."""
    with open(OUTPUT_FILE, "r") as file:
        # Make sure file isn't empty
        if len(file.read(1)) != 0:
            # Return to beginning of file
            file.seek(0)
            all_outputs = json.load(file)
        else:
            return [], []

    previous_instructions = []
    previous_outputs = []
    for num, output in all_outputs.items():
        # Only append previous instructions
        if int(num) < instruction_num:
            previous_instructions.append(instructions[int(num)])
            # Store string version of each instruction response
            previous_outputs.append(json.dumps(output))

        if not update or instruction_num in updated_instructions:
            # Only include current instruction if not in update mode or current instruction
            # has already been updated
            if int(num) == instruction_num:
                previous_instructions.append(instructions[int(num)])
                # Store string version of each instruction response
                previous_outputs.append(json.dumps(output))

    return previous_instructions, previous_outputs


def instruction_gpt_calls(
    detector: ObjectDetectionInterface,
    instructions: list[str],
    instruction_num: int,
    thres1: float,
    thres2: float,
    image_file: str,
    update: bool,
) -> tuple[tuple[float, float], str]:
    """Sends an instruction and image to be parsed by GPT-4V.

    Args:
    - instruction: String instruction to parse
    - thres1: Threshold for cropping image using GroundingDINO
    - thres2: Threshold for final object detection using GroundingDINO
    - image_file: Image to send to GPT-4V
    - update: True if should replace current instruction output in output file, else False

    Output is returned in JSON format.
    """
    instruction = instructions[instruction_num]
    print(f"Parsing instruction: {instruction}...")
    # Get previous info to give to GPT for conversation history
    previous_instructions, previous_responses = get_previous_gpt_outputs(
        instructions, instruction_num, update
    )

    valid_json = False
    parse_attempts = 0
    while not valid_json:
        if parse_attempts >= 3:
            print("")
            raise DetectionException("GPT could not output valid JSON in 3 attempts.")
        json_output: dict[str, list[str]] = parse_instruction(
            instruction, image_file, previous_instructions, previous_responses
        )
        if json_output is not None:
            valid_json = True
        parse_attempts += 1

    print("-------- GPT OUTPUT 1: ----------")
    print(json.dumps(json_output, indent=4))

    valid_actions = False
    no_crop = False
    second_image = image_file
    attempts = 0
    # Call GPT until the action it outputs is valid (in possible_actions)
    while not valid_actions:
        if attempts >= 3:
            print("GPT did not find a correct action in 3 attempts, moving on.")
            break
        if not no_crop:
            cropped_image = get_cropped_image(detector, thres1, image_file, json_output)
            second_image = cropped_image
            # Don't run on cropped image if only 1 box is found, not necessary
            if cropped_image == "":
                no_crop = True
                second_image = image_file

        if no_crop:
            print("Checking first GPT output since no crop needed.")
            valid_actions, prompt, action = add_json_to_output_file(
                json_output, instruction_num, update
            )
            # Don't run parser again if first output was valid and no need to crop
            if valid_actions:
                break
            else:
                print("Parsing original image again to get valid actions.")

        # Give second pass higher detail to be sure outputs are correct
        valid_json = False
        parse_attempts = 0
        while not valid_json:
            if parse_attempts >= 3:
                raise DetectionException(
                    "GPT could not output valid JSON in 3 attempts."
                )
            parsed_output = parse_instruction(
                instruction,
                second_image,
                previous_instructions,
                previous_responses,
                high_detail=True,
            )
            if parsed_output is not None:
                valid_json = True
            parse_attempts += 1

        print("-------- GPT OUTPUT 2: ----------")
        print(json.dumps(parsed_output, indent=4))

        # Add output to parser output JSON file
        valid_actions, prompt, action = add_json_to_output_file(
            parsed_output, instruction_num, update
        )
        attempts += 1

    # delete_images(cropped_image)

    # Ensure while loop didn't break after 3 attempts
    if valid_actions:
        center = detect_object_from_prompt(detector, prompt, image_file, thres1, thres2)
    else:
        center = None

    if center is None:
        return None, ""

    return center, action


def detect_object_from_prompt(
    detector: ObjectDetectionInterface,
    object_prompt: str,
    image_path: str,
    thres1: float,
    thres2: float,
) -> tuple[float, float]:
    """Runs object detection with json_data instead of grabbing it from the JSON file.
    This is planned to be called directly after GPT parses an instruction.
    """
    # print(f"Running object detection on instruction {instruction_num}...")
    center, top_left_coord, cropped_image = detector.run_object_detection_with_crop(
        image_path,
        object_prompt,
        thres1,
        thres2,
    )

    if center is None:
        return None

    original_image_box_center = (
        top_left_coord[0] + center[0],
        top_left_coord[1] + center[1],
    )

    delete_images(cropped_image)

    return original_image_box_center
