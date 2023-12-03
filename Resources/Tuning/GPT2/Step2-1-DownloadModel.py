# This script is responsible for downloading the pre-trained model and its tokenizer using the transformers library by Hugging Face. It defines a function download_model that takes a model name and a path to save the model and tokenizer. It controls the handling of certificate files for HTTP requests and sets environment variables accordingly. The script finally downloads and saves the GPT-2 model and tokenizer to a specified path on the local file system.

import os
from transformers import GPT2LMHeadModel, GPT2Tokenizer

# Path to the .pem file that contains the trusted root certificates
CERT_FILE_PATH = './Data/trusted_root_certificates.pem'

# Only set the REQUESTS_CA_BUNDLE environment variable if the certificate file exists and is not empty
if os.path.exists(CERT_FILE_PATH) and os.path.getsize(CERT_FILE_PATH) > 0:
    os.environ['REQUESTS_CA_BUNDLE'] = os.path.abspath(CERT_FILE_PATH)

os.environ['HF_HUB_DISABLE_SYMLINKS_WARNING'] = '1'

def download_model(model_name, model_path):
    tokenizer = GPT2Tokenizer.from_pretrained(model_name)
    model = GPT2LMHeadModel.from_pretrained(model_name)
    
    # Save the downloaded model & tokenizer in the specified model path
    tokenizer.save_pretrained(model_path)
    model.save_pretrained(model_path)

if __name__ == '__main__':
    # Define the model name and the path where the model is to be saved
    MODEL_NAME = 'gpt2'  # 'gpt2' is a smaller version of the model
    MODEL_SAVE_PATH = './Models/OpenAI/GPT2/'
    
    # Download the model and tokenizer
    download_model(MODEL_NAME, MODEL_SAVE_PATH)
    print(f"The {MODEL_NAME} model and tokenizer have been downloaded and saved to {MODEL_SAVE_PATH}")