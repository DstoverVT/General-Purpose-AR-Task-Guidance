import base64
import json
import re
from dotenv import load_dotenv
from openai import OpenAI

load_dotenv()


def encode_image(image_path):
    # Function to encode the image to Base64
    with open(image_path, "rb") as image_file:
        return base64.b64encode(image_file.read()).decode("utf-8")


def output_to_json(output):
    """Parse output from GPT into JSON file called parser_output.json."""
    start = output.find("{")
    end = output.rfind("}") + 1
    json_string = output[start:end]

    filename = "parser_output.json"
    json_data = json.loads(json_string)

    with open(filename, "w") as file:
        json.dump(json_data, file, indent=4)


def parse_instruction(instruction: str, image_path: str) -> str:
    """Function to use GPT-4V to parse an instruction given images of environment."""
    base64_image = encode_image(image_path)

    client = OpenAI()

    possible_actions = ["press", "twist", "pick up", "pull"]
    action_string = ", ".join(possible_actions)
    # print(action_string)

    system_prompt = f"You will be given an instruction that a user has to perform. Your output should be in JSON format. \
                    One JSON field should be called 'objects', which will contain a list of objects (strings) that exist in the provided image that the user should use to complete the instruction. \
                    Try to use the minimum number of objects needed for the user complete the instruction. \
                    Each object string should be structured as such: '<object position> <object name>' with the following properties: \
                    <object name> is the specific object the user should use to complete the instruction. If the object is a component within a larger object, output the most specific component needed for the instruction. Try to specify the object with the lowest amount of words. \
                    <object position> is the position of the object in the image. Check that the position ensures the object can't be confused with other similar objects, also include the object color if it is unique. Try to specify the position with the lowest amount of words. \
                    Additionally, the user will have to interact with the objects to perform the instruction. The possible actions for the user include: {action_string}. \
                    Each object within the 'objects' list should have a corresponding action the user should perform on the object. \
                    So, include another JSON field called 'actions', which will contain a list of actions (strings) where each entry is the action the user should perform on the corresponding object entry in the 'objects' list. \
                    Do not include any other JSON fields other than 'objects' and 'actions', which should both be lists of the same length."

    # system_prompt = "I am going to give you a picture with several bounding boxes around objects. I want you to output the \
    #                 bounding box color around the object that is most relevant for a user to complete an instruction also given to you."

    # Replace long whitespace with one space using regex
    system_prompt = re.sub(r"\s+", " ", system_prompt)
    # print(system_prompt)

    example = '{\n  "objects":\n  [\n    "position object 1"\n  ],\n  "actions":\n  [\n    "action 1"\n  ]\n}'
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

    output = response.choices[0].message.content
    output_to_json(output)

    return output


# if __name__ == "__main__":
# Send image and instruction to instruction_parser
# instruction = "Heat up toast"
# image_path = "data/appliance_test/toaster.jpg"

# output = parse_instruction(instruction, image_path)
# print("-------- OUTPUT 1: ----------")
# print(output)
# output_to_json(output)

# detector = ObjectDetectionInterface()
# detector.prime_detection_with_test()
# cropped_image = get_cropped_image(detector, image_path, 0.2)

# second_output = parse_instruction(instruction, cropped_image)
# print("-------- OUTPUT 2: ----------")
# output_to_json(second_output)
