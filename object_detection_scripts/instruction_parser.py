import base64
import json
import re
from dotenv import load_dotenv
from openai import OpenAI

load_dotenv()

possible_actions = ["press", "twist", "pull", "pick up"]  # "put down destination"]


def encode_image(image_path):
    """Function to encode the image to Base64"""
    with open(image_path, "rb") as image_file:
        return base64.b64encode(image_file.read()).decode("utf-8")


def output_to_json(output) -> dict[str, list[str]]:
    """Parse output from GPT into JSON as Python object."""
    start = output.find("{")
    end = output.rfind("}") + 1
    json_string = output[start:end]

    json_data = json.loads(json_string)

    return json_data

    # with open(OUTPUT_FILE, "w") as file:
    #     json.dump(json_data, file, indent=4)


def parse_instruction(
    instruction: str,
    image_path: str,
    previous_instructions: list[str],
    previous_outputs: list[str],
    high_detail: bool = False,
) -> dict[str, list[str]]:
    """Function to use GPT-4V to parse an instruction given an image of the environment."""
    base64_image = encode_image(image_path)

    client = OpenAI()

    action_string = ", ".join(possible_actions)

    system_prompt = f"You will be given multiple instructions that a user has to perform. You will be given them one at a time as the user completes them. Your output should be in JSON format. \
                    One JSON field should be called 'objects', which will contain a list of objects (strings) that exist in the provided image that the user should use to complete the current instruction. \
                    Each object within the 'objects' list should have a corresponding action the user should perform on the object to complete the instruction. \
                    Another JSON field called 'actions' will contain these actions (strings) in a list where each entry is the action the user should perform on the corresponding object entry in the 'objects' list. \
                    The possible actions for the user include: {action_string}. \
                    Make sure the user can complete the instruction with all of the objects and actions while trying to use the lowest amount of objects. \
                    In the 'objects' list, each object string should be structured as such: '<object position> <object name>' with the following properties: \
                    <object name> is the specific object the user should use to complete the instruction. If the object is a component within a larger object, output the most specific component needed for the instruction. Be specific while trying to use the lowest amount of words for the object name. \
                    <object position> is the position of the object in the image. Check that the position ensures the object can't be confused with other similar objects, also include the object color if it is unique. Be specific while trying to use the lowest amount of words for the position. \
                    Do not include any other JSON fields other than 'objects' and 'actions', which should both be lists of the same length. \
                    Make sure the outputs make sense given previous instructions the user has completed."

    # Replace long whitespace with one space using regex
    system_prompt = re.sub(r"\s+", " ", system_prompt)
    # print(system_prompt)

    example = '{\n  "objects":\n  [\n    "position object 1"\n  ],\n  "actions":\n  [\n    "action 1"\n  ]\n}'
    # print(f"JSON:\n{example}")

    messages = [
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
    ]

    # Append previous conversation to give to GPT
    for prev_instr, prev_response in zip(previous_instructions, previous_outputs):
        print("PREVIOUS CONVERSATION: ")
        print(f"Previous instruction: {prev_instr}")
        print(f"Previous response: {prev_response}")
        messages.append(
            {
                "role": "user",
                "content": [{"type": "text", "text": f"Instruction: {prev_instr}"}],
            },
        )
        messages.append(
            {
                "role": "assistant",
                "content": [{"type": "text", "text": prev_response}],
            },
        )

    # Append current instruction with image
    messages.append(
        {
            "role": "user",
            "content": [
                {"type": "text", "text": f"Instruction: {instruction}"},
                {
                    "type": "image_url",
                    "image_url": {
                        "url": f"data:image/jpeg;base64,{base64_image}",
                        f"detail": "high" if high_detail else "low",
                    },
                },
            ],
        },
    )

    response = client.chat.completions.create(
        model="gpt-4-vision-preview", max_tokens=300, messages=messages
    )

    output = response.choices[0].message.content
    print(f"GPT raw output: {output}")
    json_output = output_to_json(output)

    return json_output
