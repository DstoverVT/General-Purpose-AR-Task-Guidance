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

    img_file = open(
        os.path.join("data", "appliance_test", "humidifier.jpg"),
        # os.path.join("data", "HL_stove.jpg"),
        "rb",
    )
    file = {"image": img_file}

    response1 = requests.post(INSTRUCTION_URL, files=file)

    img_file = open(
        os.path.join("data", "appliance_test", "humidifier.jpg"),
        # os.path.join(HOME, "data", "HL_stove.jpg"),
        "rb",
    )
    file = {"image": img_file}

    # response2 = requests.post(DETECTOR_URL, files=file)
    # try:
    #     output = response2.json()
    # except Exception as e:
    #     print(e)

    print(f"{time.time() - begin} seconds")


def send_test():
    begin = time.time()
    URL = "http://127.0.0.1:5000/test_hello"
    response = requests.get(URL)
    print(response.text)
    print(f"{time.time() - begin} seconds")


if __name__ == "__main__":
    send_test_image()
    # send_test()
