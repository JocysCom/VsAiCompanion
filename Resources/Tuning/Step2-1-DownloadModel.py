import os
from transformers import GPT2LMHeadModel, GPT2Tokenizer

# Path to the .pem file that contains the trusted root certificates
CERT_FILE_PATH = './Data/trusted_root_certificates.pem'

# Configure requests to use the additional certificates for TLS connections
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