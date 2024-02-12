import requests
import os


def send_image():
    HOME = os.path.dirname(os.path.abspath(__file__))
    # URL = "http://172.29.203.123:5000/upload_image"
    URL = "http://127.0.0.1:5000/upload_image"

    try:
        img_file = open(os.path.join(HOME, "data", "HL_microwave_close.jpg"), "rb")
    except Exception as e:
        print(e)
        return None

    file = {"image": img_file}
    response = requests.post(URL, files=file)
    # try:
    #     output = response.json()
    # except Exception as e:
    #     print(e)

    print(response.text)


send_image()
