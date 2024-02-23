import json
import os
import time
from flask import Flask, request
from werkzeug.utils import secure_filename
from object_detection import ObjectDetection, DetectionException
from datetime import datetime

app = Flask(__name__)
detector = ObjectDetection()


def configure_folder(image_path):
    """Adds image path as config variable."""
    app.config["IMAGE_PATH"] = image_path


def get_objects_from_json() -> tuple[list[str], str]:
    """Return objects in promptable list for GroundingDINO from JSON file"""
    filename = "parser_output.json"
    with open(filename, "r") as file:
        json_data = json.load(file)

    object_list = json_data["objects"]
    object_prompt = " . ".join(object_list)

    return object_prompt, json_data["action"]


@app.after_request
def print_response(response):
    print(response.get_data(as_text=True))
    print(response.status_code)
    return response


def get_error_response(msg: str):
    """Creates HTTP response for an error case."""
    return {"message": msg}, 400


def save_image() -> str:
    """Checks and saves image from HTTP post request.

    Returns: filepath string of image
    Throws: DetectionException if an error occurs
    """
    HEADER = "image"

    # Check if file header exists in request
    if HEADER not in request.files:
        raise DetectionException(
            f"the file header {HEADER} does not exist in the request"
        )

    image = request.files[HEADER]

    # Check if file in request is valid
    if not image:
        raise DetectionException("the file in the request was not valid")

    now = datetime.now()
    timestamp = now.strftime("%m-%d_%H-%M-%S")

    filename = timestamp + secure_filename(image.filename)
    filepath = os.path.join(app.config["IMAGE_PATH"], filename)
    image.save(filepath)

    return filepath


def determine_best_box(boxes, confidence, phrases) -> tuple[float, float]:
    """Gets best box from object detection given all boxes, confidences, and phrases.

    Args:
        - boxes: List of boxes from object detection
        - confidence: List of confidences from object detection

    Returns:
        - (float, float) 2D coordinate of best match object to send to headset
    """
    # Send top_boxes_result.png to GPT-4V to select best box for the given object (from JSON file)


def delete_images(hololens_image_path: str):
    # Remove file that was saved, no need anymore
    if os.path.exists(hololens_image_path):
        os.remove(hololens_image_path)

    # box_image_path = os.path.join(detector.HOME, detector.BOX_FILENAME)
    # if os.path.exists(box_image_path):
    #     os.remove(box_image_path)


@app.route("/upload_image", methods=["POST"])
def upload_image():
    """Endpoint for Flask server to send an image to this device."""
    try:
        filepath = save_image()
    except DetectionException as e:
        return get_error_response(e)

    object_prompt, action = get_objects_from_json()

    # Run model on image
    try:
        begin = time.time()
        boxes, boxes_scaled, logits, phrases = detector(
            filepath,
            # prompt="helmet . computer . bottle . table . mouse . keyboard . controller . knob . button . microwave",
            prompt=object_prompt,
            threshold=0.1,
            draw=True,
        )
        print(f"Model time: {time.time() - begin} s")
    except Exception as e:
        return get_error_response(f"{type(e).__name__}: {e}")

    # try:
    #     best_box = determine_best_box(boxes_scaled, logits, phrases)
    # except DetectionException as e:
    #     return get_error_response(e)

    delete_images(filepath)

    detector_response = {
        "phrases": phrases,
        "boxes": boxes_scaled.tolist(),
        "confidence": logits.tolist(),
        "action": action,
        "threshold": 1000,
    }
    # detector_response = {"center": boxes_scaled, "action": action}
    return detector_response, 200


# MAIN
configure_folder(detector.HOME)
app.run(host="0.0.0.0", debug=True, use_reloader=False)
