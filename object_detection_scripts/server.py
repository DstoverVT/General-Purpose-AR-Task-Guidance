import os
from flask import Flask, request
from werkzeug.utils import secure_filename
from object_detection import ObjectDetection
from datetime import datetime

app = Flask(__name__)
detector = ObjectDetection()


def configure_folder(image_path):
    """Adds image path as config variable."""
    app.config["IMAGE_PATH"] = image_path


@app.route("/upload_image", methods=["POST"])
def upload_image():
    """Endpoint for Flask server to send an image to this device."""
    HEADER = "image"

    # Check if file header exists in request
    if HEADER not in request.files:
        return {
            "success": False,
            "message": f"the file header {HEADER} does not exist in the request",
        }, 400

    image = request.files[HEADER]

    # Check if file in request is valid
    if not image:
        return {
            "success": False,
            "message": f"the file in the request was not valid",
        }, 400

    now = datetime.now()
    timestamp = now.strftime("%m-%d_%H-%M-%S")

    filename = timestamp + secure_filename(image.filename)
    filepath = os.path.join(app.config["IMAGE_PATH"], filename)
    image.save(filepath)

    # Run model on image
    try:
        boxes, boxes_scaled, logits, phrases = detector(
            filepath, prompt="computer", threshold=0.2, draw=True
        )
    except Exception as e:
        return {"success": False, "message": str(e)}, 400

    # Remove file that was saved, no need anymore
    if os.path.exists(filepath):
        os.remove(filepath)

    detector_response = {
        "success": True,
        "phrases": phrases,
        "boxes": boxes_scaled.tolist(),
    }
    return detector_response, 200


# MAIN
configure_folder(detector.HOME)
app.run(host="0.0.0.0", debug=True)
