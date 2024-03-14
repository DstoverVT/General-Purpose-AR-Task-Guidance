import requests
import os
import time


def send_test_image():
    begin = time.time()
    DETECTOR_URL = "http://172.21.134.52:5000/upload_image"
    INSTRUCTION_URL = "http://172.21.134.52:5000/parse_instruction"
    GET_INSTRUCTION_URL = "http://172.21.134.52:5000/get_instructions"
    # DETECTOR_URL = "http://127.0.0.1:5000/upload_image"
    # INSTRUCTION_URL = "http://127.0.0.1:5000/parse_instruction"

    response = requests.get(GET_INSTRUCTION_URL)
    print(response.text)
    instructions = response.json()
    num_instructions = len(instructions["instructionsList"])

    # Test file path
    filepath = os.path.join("data", "office_test", "lock.jpg")

    # Test instruction parsing (operator mode)
    for num in range(num_instructions):
        img_file = open(
            filepath,
            "rb",
        )
        file = {"image": img_file}
        instruction_num = str(num)

        response1 = requests.post(
            INSTRUCTION_URL, data={"instructionNum": instruction_num}, files=file
        )

    # Test object detection (user mode)

    for num in range(num_instructions):
        img_file = open(
            filepath,
            "rb",
        )
        file = {"image": img_file}
        instruction_num = str(num)

        response2 = requests.post(
            DETECTOR_URL, data={"instructionNum": instruction_num}, files=file
        )
        print(response2.text)

    print(f"{time.time() - begin} seconds")


def gpt_only_test():
    INSTRUCTION_URL = "http://172.21.134.52:5000/parse_instruction"
    GET_INSTRUCTION_URL = "http://172.21.134.52:5000/get_instructions"
    # DETECTOR_URL = "http://127.0.0.1:5000/upload_image"
    # INSTRUCTION_URL = "http://127.0.0.1:5000/parse_instruction"

    response = requests.get(GET_INSTRUCTION_URL)
    print(response.text)
    instructions = response.json()
    num_instructions = len(instructions["instructionsList"])

    # Test file path
    filepath1 = os.path.join("data", "office_test", "lock.jpg")
    filepath2 = os.path.join("data", "office_test", "lock.jpg")

    # Test instruction parsing (operator mode)
    img_file = open(
        filepath1,
        "rb",
    )
    file = {"image": img_file}
    instruction_num = "0"

    response1 = requests.post(
        INSTRUCTION_URL, data={"instructionNum": instruction_num}, files=file
    )

    img_file = open(
        filepath2,
        "rb",
    )
    file = {"image": img_file}
    instruction_num = "0"

    response1 = requests.post(
        INSTRUCTION_URL, data={"instructionNum": instruction_num}, files=file
    )


def send_test():
    begin = time.time()
    URL = "http://172.21.134.52:5000/test_hello"
    response = requests.get(URL)
    print(response.text)
    print(f"{time.time() - begin} seconds")


if __name__ == "__main__":
    # gpt_only_test()
    send_test_image()
    # send_test()
