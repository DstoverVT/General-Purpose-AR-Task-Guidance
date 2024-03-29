import time
from datetime import datetime
from flask import Flask, request
from object_detection import (
    ObjectDetectionInterface,
    DetectionException,
)
from task_guidance import (
    delete_images,
    detect_objects_from_json,
    instruction_gpt_calls,
    get_instructions_from_file,
    updated_instructions,
)
from werkzeug.utils import secure_filename


app = Flask(__name__)
app.config["DETECTOR"] = ObjectDetectionInterface()
detector: ObjectDetectionInterface = app.config["DETECTOR"]
# Run object detection once on test image since first takes way longer (caching)
detector.prime_detection_with_test()
# Configure other app config data
app.config["CROP_THRESHOLD"] = 0.2
app.config["OBJECT_THRESHOLD"] = 0.2
# Structure to hold instructions input in 'instructions.txt'
app.config["INSTRUCTIONS"] = []
instructions: list[str] = app.config["INSTRUCTIONS"]
# Flag to determine whether to update instructions
app.config["UPDATE"] = False


@app.after_request
def print_response(response):
    """Called after request finishes, simply prints results."""
    print(response.get_data(as_text=True))
    print(response.status_code)
    return response


@app.errorhandler(Exception)
def handle_exception(e):
    return {"error": f"{type(e).__name__}: {e}"}, 500


def get_error_response(msg: str):
    """Creates HTTP response for an error case."""
    return {"message": msg}, 500


def save_image_from_request() -> str:
    """Checks and saves image from HTTP post request.

    Returns: filepath string of image
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

    return filename


@app.route("/upload_image", methods=["POST"])
def upload_image():
    """Endpoint for Flask server to send an image and run object detection on it.

    Returns:
    - Sends back response containing center (x, y) of detected object and action to perform
    """
    request_begin = time.time()
    filepath = save_image_from_request()
    instruction_num: int = int(request.form["instructionNum"])
    picture_num: int = int(request.form["pictureNum"])
    found_center, action = detect_objects_from_json(
        detector,
        filepath,
        app.config["CROP_THRESHOLD"],
        app.config["OBJECT_THRESHOLD"],
        instruction_num,
        picture_num,
    )

    delete_images(filepath)

    detector_response = {"center": found_center, "action": action}
    # print(json.dumps(detector_response, indent=4))
    print(f"Request time: {time.time() - request_begin} s")
    return detector_response


@app.route("/test_hello", methods=["GET"])
def test_hello():
    """Simple request for testing."""
    return {"test": "hello"}


@app.route("/parse_instruction", methods=["POST"])
def instruction_to_json():
    """Parse instruction. Must call 'get_instructions' endpoint first.

    Adds output to 'parser_output.json' file. If successful, returns object center and action.
    """
    request_begin = time.time()
    filepath = save_image_from_request()
    instruction_num: int = int(request.form["instructionNum"])
    # Output will be written to parser_output.json
    found_center, action = instruction_gpt_calls(
        detector,
        instructions,
        instruction_num,
        app.config["CROP_THRESHOLD"],
        app.config["OBJECT_THRESHOLD"],
        filepath,
        app.config["UPDATE"],
    )

    delete_images(filepath)

    detector_response = {"center": found_center, "action": action}
    # print(json.dumps(detector_response, indent=4))
    print(f"Request time: {time.time() - request_begin} s")
    return detector_response


@app.route("/update_instructions", methods=["GET"])
def update_instructions():
    """Get list of instructions from 'instructions.txt' and add to instructions list."""
    app.config["UPDATE"] = True
    updated_instructions.clear()
    return get_instructions()


@app.route("/get_instructions", methods=["GET"])
def get_instructions(clear_output: bool = False):
    """Get list of instructions from 'instructions.txt' and add to instructions list."""
    instructions.clear()
    instructions.extend(get_instructions_from_file(clear_output))
    return instructions


@app.route("/new_instructions", methods=["GET"])
def new_instructions():
    """Get new instructions and clear old outputs (clears parser JSON file).

    Clearing the JSON output file means that the operator phase must occur again.
    """
    app.config["UPDATE"] = False
    return get_instructions(clear_output=True)


# Run flask server
if __name__ == "__main__":
    app.run(host="0.0.0.0", debug=True, use_reloader=False)
