# This script is responsible for downloading the pre-trained model and its tokenizer using the transformers library by Hugging Face. It defines a function download_model that takes a model name and a path to save the model and tokenizer. It controls the handling of certificate files for HTTP requests and sets environment variables accordingly. The script finally downloads and saves the model and tokenizer to a specified path on the local file system.

# Note: run this in console as Administrator to cash same files with symlinks.

import os
import json
from transformers import AutoTokenizer, AutoModelForCausalLM

# Load configuration from a JSON file
with open('Step-0-1-Config.json', 'r') as config_file:
    config = json.load(config_file)

# Path to the .pem file that contains the trusted root certificates
CERT_FILE_PATH = config.get('CERT_FILE_PATH')
# The name of the model on Hugging Face Model Hub
MODEL_NAME = config.get('MODEL_NAME')
# Customize this path as necessary
CACHE_DIR = config.get('CACHE_DIR')

# Only set the REQUESTS_CA_BUNDLE environment variable if the certificate file exists and is not empty
if os.path.exists(CERT_FILE_PATH) and os.path.getsize(CERT_FILE_PATH) > 0:
    os.environ['REQUESTS_CA_BUNDLE'] = os.path.abspath(CERT_FILE_PATH)

os.environ['HF_HUB_DISABLE_SYMLINKS_WARNING'] = '1'

def download_and_cache_model(model_name, cache_dir):
    """
    Download and cache a model and its tokenizer using the transformers library.
    
    :param model_name: identifier of the model on Hugging Face Model Hub (string)
    :param cache_dir: directory where the model and tokenizer will be cached (string)
    """
    # Download and cache the model
    tokenizer = AutoTokenizer.from_pretrained(model_name, cache_dir=cache_dir)
    model = AutoModelForCausalLM.from_pretrained(model_name, cache_dir=cache_dir)
    return model, tokenizer

# Example usage
if __name__ == "__main__":
    # Download and cache the model and tokenizer
    model, tokenizer = download_and_cache_model(MODEL_NAME, CACHE_DIR)
    
    if not model:
        raise Exception("Failed to download the Orca-2 model.")

    print(f"{MODEL_NAME} has been downloaded and cached.")