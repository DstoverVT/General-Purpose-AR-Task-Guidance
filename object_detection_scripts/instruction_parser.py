import base64
import cv2
import json
import re
from dotenv import load_dotenv
from openai import OpenAI

load_dotenv()


def encode_image(image_path):
    # Function to encode the image to Base64
    with open(image_path, "rb") as image_file:
        return base64.b64encode(image_file.read()).decode("utf-8")


def parse_instruction(instruction: str, image_path: str) -> str:
    """Function to use GPT-4V to parse an instruction given images of environment."""
    base64_image = encode_image(image_path)

    client = OpenAI()

    possible_actions = ["press", "twist"]
    action_string = ", ".join(possible_actions)
    # print(action_string)

    system_prompt = f"You will be given an instruction that a user has to perform. Your output should be in JSON format. \
                    One JSON field should be called 'objects', which will contain a list of objects (strings) that exist in the provided image that the user should use to complete the instruction. \
                    Each object string should be structured as such: '<object position> <object name>', and should be understandable by a fifth grader. \
                    <object position> is information to help a fifth grader find the object in the image without ambiguity. If there are multiple of the same object, give a relative position of this object to the others. \
                    <object name> is the object the user should use to complete the instruction, which should be understandable by a fifth grader. \
                    Additionally, the user will have to interact with one of the objects to perform the instruction. The possible actions for the user include: {action_string}. \
                    So, another JSON field should be called 'action' which will include only one of the actions that the user should perform. \
                    Do not include any other JSON fields other than 'objects' and 'action'."

    # system_prompt = "I am going to give you a picture with several bounding boxes around objects. I want you to output the \
    #                 bounding box color around the object that is most relevant for a user to complete an instruction also given to you."

    # Replace long whitespace with one space using regex
    system_prompt = re.sub(r"\s+", " ", system_prompt)
    # print(system_prompt)

    example = (
        '{\n"objects": [\n"position object 1"\n],\n"action": "action selection"\n}'
    )
    # print(f"JSON:\n{example}")

    response = client.chat.completions.create(
        model="gpt-4-vision-preview",
        messages=[
            {
                "role": "system",
                "content": [
                    {"type": "text", "text": system_prompt},
                ],
            },
            {
                "role": "assistant",
                "content": [{"type": "text", "text": f"JSON:\n{example}"}],
            },
            {
                "role": "user",
                "content": [
                    {"type": "text", "text": f"Instruction: {instruction}"},
                    {
                        "type": "image_url",
                        "image_url": {
                            "url": f"data:image/jpeg;base64,{base64_image}",
                            "detail": "low",
                        },
                    },
                ],
            },
        ],
        max_tokens=300,
    )

    return response.choices[0].message.content


def output_to_json(output):
    """Parse output from GPT into JSON file called parser_output.json."""
    start = output.find("{")
    end = output.rfind("}") + 1
    json_string = output[start:end]

    filename = "parser_output.json"
    json_data = json.loads(json_string)

    with open(filename, "w") as file:
        json.dump(json_data, file, indent=4)


# Send image and instruction to instruction_parser
# instruction = "Turn on coffee machine"
# instruction = "Turn on the lights"
instruction = "Raise the temperature by 1 degree"
# instruction = "Turn top left burner to medium heat."
# instruction = "Start the microwave"
image_path = "data/HL_temperature.jpg"
# image_path = "data/HL_microwave_close.jpg"
output = parse_instruction(instruction, image_path)
print(output)

output_to_json(output)
