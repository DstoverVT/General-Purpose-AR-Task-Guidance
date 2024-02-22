import os
import torch
import cv2
import time
import numpy as np
import matplotlib.pyplot as plt

from groundingdino.util.inference import load_model, load_image, predict, annotate


class DetectionException(Exception):
    """Exception encountered during or before object detection."""


class ObjectDetection:
    """Object detection using GroundingDINO model."""

    def __init__(self):
        """Setup GroundingDINO model.

        Prereq: Requires GroundingDINO repo to be cloned to current working directory and weights to be downloaded.
        """
        self.HOME = os.path.dirname(os.path.abspath(__file__))
        self.BOX_FILENAME = "top_boxes_result.png"
        print(f"Home path: {self.HOME}")
        # self.CONFIG_PATH = os.path.join(
        #     self.HOME, "GroundingDINO/groundingdino/config/GroundingDINO_SwinT_OGC.py"
        # )
        self.CONFIG_PATH = os.path.join(
            self.HOME, "GroundingDINO/groundingdino/config/GroundingDINO_SwinB_cfg.py"
        )
        print(self.CONFIG_PATH, "; exist:", os.path.isfile(self.CONFIG_PATH))
        # WEIGHTS_NAME = "groundingdino_swint_ogc.pth"
        WEIGHTS_NAME = "groundingdino_swinb_cogcoor.pth"
        self.WEIGHTS_PATH = os.path.join(self.HOME, "weights", WEIGHTS_NAME)
        print(self.WEIGHTS_PATH, "; exist:", os.path.isfile(self.WEIGHTS_PATH))

        self.model = load_model(self.CONFIG_PATH, self.WEIGHTS_PATH)

    def model_inference(
        self, images: tuple[np.array, torch.Tensor], text_prompt: str, threshold: float
    ):
        """Perform object dectection on image to get boxes, logits, and phrases."""
        print("Running model inference on image")
        # begin = time.time()
        TEXT_PROMPT = text_prompt
        BOX_TRESHOLD = threshold
        TEXT_TRESHOLD = threshold

        image_source = images[0]
        img_h = image_source.shape[0]
        img_w = image_source.shape[1]

        model_image = images[1]

        # Tensor of found boxes (with confidence above box_threshold)
        # Tensor of logits for text phrases
        # List[str] of phrases from prompt found corresponding to boxes (with confidence above text_threshold)
        boxes, logits, phrases = predict(
            model=self.model,
            image=model_image,
            caption=TEXT_PROMPT,
            box_threshold=BOX_TRESHOLD,
            text_threshold=TEXT_TRESHOLD,
        )

        # Get box coordinates
        scale_fct = torch.Tensor([img_w, img_h, img_w, img_h])
        boxes_scaled = boxes * scale_fct

        # Output details for each detection:
        for result in zip(boxes_scaled, logits, phrases):
            print(f"{result[2]}: confidence {result[1]}, box {result[0].tolist()}")

        return boxes, boxes_scaled, logits, phrases

    def draw_top_boxes(
        self,
        image_source: np.array,
        boxes: torch.Tensor,
        confidence: torch.Tensor,
        thres_num: int,
    ):
        colors = {
            "RED": (0, 0, 255),
            "ORANGE": (0, 165, 255),
            "YELLOW": (0, 255, 255),
            "GREEN": (0, 255, 0),
            "BLUE": (255, 0, 0),
            "PINK": (255, 0, 255),
            "PURPLE": (102, 0, 102),
            "BROWN": (0, 51, 102),
        }
        color_names = list(colors.keys())

        if thres_num > len(colors):
            raise DetectionException(
                f"Ensure using less than {len(colors)} box thresholds."
            )

        cv_image = cv2.cvtColor(image_source, cv2.COLOR_RGB2BGR)

        if len(boxes) > thres_num:
            top_boxes = [
                box
                for box, _ in sorted(
                    zip(boxes.tolist(), confidence.tolist()),
                    key=lambda x: x[1],
                    reverse=True,
                )
            ]
            top_boxes = top_boxes[:thres_num]

        # Draw thick boxes of different colors around each object
        thickness = 5  # pixels
        for i, box in enumerate(top_boxes):
            cx, cy, width, height = box
            top_left = (int(cx - width / 2), int(cy - height / 2))
            bottom_right = (int(cx + width / 2), int(cy + height))
            cv_image = cv2.rectangle(
                cv_image, top_left, bottom_right, colors[color_names[i]], thickness
            )

        cv_image = cv2.cvtColor(cv_image, cv2.COLOR_BGR2RGB)
        plt.imshow(cv_image)
        plt.axis("off")
        plt.savefig(self.BOX_FILENAME, bbox_inches="tight", pad_inches=0, dpi=300)

    def draw_detection(
        self,
        image_source: np.array,
        model_output: tuple[torch.Tensor, torch.Tensor, torch.Tensor, list[str]],
    ):
        """Draw bounding boxes on input image."""
        boxes, boxes_scaled, logits, phrases = model_output

        annotated_frame = annotate(
            image_source=image_source, boxes=boxes, logits=logits, phrases=phrases
        )

        if boxes.numel() == 0:
            print("No objects detected.")

        for box in boxes_scaled:
            # Draw blue circle as center of each box (0, 0) is top-left of image
            annotated_frame = cv2.circle(
                annotated_frame, (int(box[0]), int(box[1])), 10, (255, 0, 0), -1
            )

        # Show output image with boxes and centers
        annotated_frame = cv2.cvtColor(annotated_frame, cv2.COLOR_BGR2RGB)
        plt.figure(figsize=(16, 16))
        plt.imshow(annotated_frame)
        plt.axis("off")
        plt.savefig("detection_result.png")
        # plt.show(block=True)

    def get_image(self, image_path: str):
        """Load image for object detection. Uses image relative path"""
        if not os.path.exists(image_path):
            raise FileExistsError("Detector Error: Image file does not exist.")

        images = load_image(image_path)
        return images

    def __call__(
        self,
        image_path: str,
        prompt: str,
        threshold: float,
        draw: bool = False,
        num_box_thres=5,
    ):
        """Detect objects on image using prompt and threshold for model."""
        images = self.get_image(image_path)
        model_output = self.model_inference(images, prompt, threshold)
        if draw:
            self.draw_detection(images[0], model_output)
        # self.draw_top_boxes(images[0], model_output[1], model_output[2], num_box_thres)

        return model_output


# MAIN
if __name__ == "__main__":
    detector = ObjectDetection()
    IMAGE_REL_PATH = "data/HL_microwave_close.jpg"
    detector(IMAGE_REL_PATH, "button", 0.2, draw=True)
