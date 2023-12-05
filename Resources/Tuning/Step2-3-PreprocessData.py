# This script preprocesses the training data for fine-tuning a GPT-2 model. It loads the GPT-2 tokenizer, sets the padding token, and defines a preprocess_function which tokenizes and organizes the messages from users and assistants into input and output pairs. This preprocess_function is then applied to the loaded dataset using the map function from the Hugging Face datasets library, followed by saving the tokenized datasets to disk. The script handles tokenization, sequence padding, and truncation. It is meant to be executed as the main program, performing preprocessing and saving the ready-to-use data for training.

import os
import json
from datasets import load_dataset, load_from_disk, concatenate_datasets
from transformers import AutoTokenizer

# Load configuration from a JSON file
with open('Step0-1-Config.json', 'r') as config_file:
    config = json.load(config_file)

# Path to the .pem file that contains the trusted root certificates
CERT_FILE_PATH = config.get('CERT_FILE_PATH')
# Customize this path as necessary
CACHE_DIR = config.get('CACHE_DIR')
# Specify the model name (as from Hugging Face Model Hub)
MODEL_NAME = config.get('MODEL_NAME')
# Path to the .pem file that contains the trusted root certificates
DATA_PATH = config.get('DATA_PATH')
# Specify the path to tokenized data
TOKENIZED_DATA_DIR = config.get('TOKENIZED_DATA_DIR')

# Only set the REQUESTS_CA_BUNDLE environment variable if the certificate file exists and is not empty
if os.path.exists(CERT_FILE_PATH) and os.path.getsize(CERT_FILE_PATH) > 0:
    os.environ['REQUESTS_CA_BUNDLE'] = os.path.abspath(CERT_FILE_PATH)

# Load tokenizer specific to the Orca-2-7b model
tokenizer = AutoTokenizer.from_pretrained(MODEL_NAME, cache_dir=CACHE_DIR)

def preprocess_function(examples):
    inputs, targets = [], []
    for example in examples['messages']:
        input_text, target_text = "", ""
        last_role = None
        for message in example:
            if last_role is None or message['role'] == last_role:
                text = message['content'] + tokenizer.eos_token
                if message['role'] == "user":
                    input_text += text
                else:
                    target_text += text
            else:
                inputs.append(input_text)
                targets.append(target_text)
                if message['role'] == "user":
                    input_text = target_text + text
                    target_text = ""
                else:
                    input_text += text
            last_role = message['role']
        
        if last_role == "assistant":
            inputs.append(input_text)
            targets.append(target_text)
    
    # Tokenize and pad the sequences to the same length
    model_inputs = tokenizer(inputs, max_length=512, padding="max_length", truncation=True)
    # With the tokenizer we can directly use 'labels' parameter for the targets
    labels = tokenizer(targets, max_length=512, padding="max_length", truncation=True)["input_ids"]

    model_inputs["labels"] = labels
    return model_inputs

if __name__ == '__main__':
    # Load the saved tokenized dataset
    existing_tokenized_data = load_from_disk(TOKENIZED_DATA_DIR)
    
    # Load the extra raw data
    extra_data = load_dataset('json', data_files=DATA_PATH)['train']
    
    # Tokenize the extra raw data
    extra_tokenized_data = extra_data.map(preprocess_function, batched=True, remove_columns=['messages'])
    
    # Combine the existing tokenized data with the extra tokenized data
    combined_tokenized_datasets = concatenate_datasets([existing_tokenized_data, extra_tokenized_data])
    
    # Save the combined tokenized data to disk for training
    combined_tokenized_datasets.save_to_disk(TOKENIZED_DATA_DIR)
    
    print(f"Combined tokenized datasets saved to {TOKENIZED_DATA_DIR}")
