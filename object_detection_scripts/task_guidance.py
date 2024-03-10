import json
import os

from instruction_parser import parse_instruction
from object_detection import DetectionException, ObjectDetectionInterface


def delete_images(*image_paths):
    # Remove file that was saved, no need anymore
    for image in image_paths:
        if os.path.exists(image):
            os.remove(image)


def get_objects_from_json() -> tuple[str, str]:
    """Return objects in promptable list for GroundingDINO from JSON file"""
    try:
        filename = "parser_output.json"
        with open(filename, "r") as file:
            json_data = json.load(file)

        object_list = json_data["objects"]
        object_prompt = " . ".join(object_list)
        action_list = json_data["actions"]
        first_action = action_list[0]
    # Catch if objects and actions fields don't exist in JSON
    except (ValueError, FileNotFoundError) as e:
        raise DetectionException(e)

    return object_prompt, first_action


def detect_objects_in_image(
    detector: ObjectDetectionInterface, image_path, thres1, thres2
) -> tuple[tuple[float, float], str]:
    """Run object detection on image."""
    object_prompt, action = get_objects_from_json()
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
    try:
        instruction_file = "instructions.txt"
        with open(instruction_file, "r") as f:
            lines = f.readlines()
    except FileNotFoundError as e:
        raise DetectionException(e)

    if len(lines) == 0:
        raise DetectionException("Need at least one instruction in instructions.txt")

    instructions = [line.strip() for line in lines]
    return instructions


def get_cropped_image(detector: ObjectDetectionInterface, threshold, filepath) -> str:
    object_prompt, _ = get_objects_from_json()
    _, boxes, _, _ = detector.run_object_detection(filepath, object_prompt, threshold)

    # Run object detection again after cropping image to largest box
    region = detector.region_containing_all_boxes(boxes)
    cropped_image_path = detector.crop_image_to_box(region, filepath)
    return cropped_image_path


def instruction_gpt_calls(
    detector: ObjectDetectionInterface,
    instruction: str,
    threshold,
    image_files: list[str],
):
    output = parse_instruction(instruction, image_files[0])
    print("-------- GPT OUTPUT 1: ----------")
    print(output)

    cropped_image = get_cropped_image(detector, threshold, image_files[0])
    output = parse_instruction(instruction, cropped_image)
    print("-------- GPT OUTPUT 2: ----------")
    print(output)

    delete_images(cropped_image)
