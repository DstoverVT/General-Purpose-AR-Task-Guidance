import json
import os
import time
from flask import Flask, request
from werkzeug.utils import secure_filename
from object_detection import ObjectDetection
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


@app.route("/upload_image", methods=["POST"])
def upload_image():
    """Endpoint for Flask server to send an image to this device."""
    HEADER = "image"

    # Check if file header exists in request
    if HEADER not in request.files:
        return {
            "message": f"the file header {HEADER} does not exist in the request",
        }, 400

    image = request.files[HEADER]

    # Check if file in request is valid
    if not image:
        return {
            "message": f"the file in the request was not valid",
        }, 400

    now = datetime.now()
    timestamp = now.strftime("%m-%d_%H-%M-%S")

    filename = timestamp + secure_filename(image.filename)
    filepath = os.path.join(app.config["IMAGE_PATH"], filename)
    image.save(filepath)

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
        return {"message": f"{type(e).__name__}: {str(e)}"}, 400

    # Remove file that was saved, no need anymore
    if os.path.exists(filepath):
        os.remove(filepath)

    detector_response = {
        "phrases": phrases,
        "boxes": boxes_scaled.tolist(),
        "confidence": logits.tolist(),
        "action": action,
        "threshold": 1000,
    }
    return detector_response, 200


# MAIN
configure_folder(detector.HOME)
app.run(host="0.0.0.0", debug=True)
