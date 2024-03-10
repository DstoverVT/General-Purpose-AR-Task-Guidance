import time
from datetime import datetime
from flask import Flask, request, jsonify
from object_detection import (
    ObjectDetectionInterface,
    DetectionException,
)
from task_guidance import (
    delete_images,
    detect_objects_in_image,
    instruction_gpt_calls,
    get_instructions_from_file,
)
from werkzeug.utils import secure_filename


app = Flask(__name__)
# Run object detection once on test image since first takes way longer (caching)
app.config["DETECTOR"] = ObjectDetectionInterface()
detector: ObjectDetectionInterface = app.config["DETECTOR"]
detector.prime_detection_with_test()
# Configure other app config data
app.config["CROP_THRESHOLD"] = 0.2
app.config["OBJECT_THRESHOLD"] = 0.3
# Structure to hold instructions input in 'instructions.txt'
app.config["INSTRUCTIONS"] = []
instructions: list[str] = app.config["INSTRUCTIONS"]


@app.after_request
def print_response(response):
    print(response.get_data(as_text=True))
    print(response.status_code)
    return response


def get_error_response(msg: str):
    """Creates HTTP response for an error case."""
    return {"message": msg}, 500


def save_image_from_request() -> list[str]:
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
    image.save(filename)

    image_paths = []
    image_paths.append(filename)

    return image_paths


@app.route("/upload_image", methods=["POST"])
def upload_image():
    request_begin = time.time()
    """Endpoint for Flask server to send an image to this device."""
    try:
        filepath = save_image_from_request()[0]
        found_center, action = detect_objects_in_image(
            detector,
            filepath,
            app.config["CROP_THRESHOLD"],
            app.config["OBJECT_THRESHOLD"],
        )
    except DetectionException as e:
        return get_error_response(f"{type(e).__name__}: {e}")

    delete_images(filepath)

    detector_response = {"center": found_center, "action": action}
    # print(json.dumps(detector_response, indent=4))
    print(f"Request time: {time.time() - request_begin} s")
    return detector_response


@app.route("/test_hello", methods=["GET"])
def test_hello():
    return {"test": "hello"}


@app.route("/parse_instruction", methods=["POST"])
def instruction_to_json():
    """Parse instruction. Must call 'get_instructions' endpoint first."""
    assert len(instructions) > 0
    try:
        filepaths = save_image_from_request()
        # Output will be written to parser_output.json
        instruction_gpt_calls(
            detector, instructions, app.config["CROP_THRESHOLD"], filepaths
        )
    except DetectionException as e:
        return get_error_response(f"{type(e).__name__}: {e}")

    delete_images(*filepaths)

    return {}


@app.route("/get_instructions", methods=["GET"])
def get_instructions():
    """Get list of instructions from 'instructions.txt'"""
    instructions.clear()
    instructions.extend(get_instructions_from_file())
    return jsonify(instructions)


# Run flask server
if __name__ == "__main__":
    app.run(host="0.0.0.0", debug=True, use_reloader=False)
