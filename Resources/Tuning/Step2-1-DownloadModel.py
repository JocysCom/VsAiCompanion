# This script is responsible for downloading the pre-trained model and its tokenizer using the transformers library by Hugging Face. It defines a function download_model that takes a model name and a path to save the model and tokenizer. It controls the handling of certificate files for HTTP requests and sets environment variables accordingly. The script finally downloads and saves the GPT-2 model and tokenizer to a specified path on the local file system.

import os
from transformers import AutoModelForCausalLM, AutoTokenizer

# Path to the .pem file that contains the trusted root certificates
CERT_FILE_PATH = './Data/trusted_root_certificates.pem'

# Only set the REQUESTS_CA_BUNDLE environment variable if the certificate file exists and is not empty
if os.path.exists(CERT_FILE_PATH) and os.path.getsize(CERT_FILE_PATH) > 0:
    os.environ['REQUESTS_CA_BUNDLE'] = os.path.abspath(CERT_FILE_PATH)

os.environ['HF_HUB_DISABLE_SYMLINKS_WARNING'] = '1'

def download_and_cache_model(model_name, cache_dir="./model_cache"):
    """
    Download and cache a model and its tokenizer using the transformers library.
    
    :param model_name: identifier of the model on Hugging Face Model Hub (string)
    :param cache_dir: directory where the model and tokenizer will be cached (string)
    """
    # Download and cache the model
    model = AutoModelForCausalLM.from_pretrained(model_name, cache_dir=cache_dir)
    tokenizer = AutoTokenizer.from_pretrained(model_name, cache_dir=cache_dir)
    return model, tokenizer

# Example usage
if __name__ == "__main__":
    model_name = "microsoft/Orca-2-7b"  # The name of the model on Hugging Face Model Hub
    cache_dir = "./llama2_model_cache"  # Customize this path as necessary
    
    # Download and cache the model and tokenizer
    model, tokenizer = download_and_cache_model(model_name, cache_dir)
    
    print(f"{model_name} has been downloaded and cached.")