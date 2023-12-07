# This script defines a function for loading training data from a .jsonl file format and preparing it to be used for fine-tuning. The script uses the datasets module from Hugging Face to perform this task. The function load_data is provided with a path to the training data file, which defaults to ./Data/data.jsonl, and returns a loaded dataset object. The actual usage of this dataset, such as saving it to a file or continuing with the processing, is implied in comments but not explicitly implemented in the script.

import os
import json
from datasets import load_dataset

# Load configuration from a JSON file
with open('Step0-1-Config.json', 'r') as config_file:
    config = json.load(config_file)

# Path to the JSONL data file
DATA_PATH = config.get('DATA_PATH')
# Path where the tokenized data will be saved
TOKENIZED_DATA_DIR = config.get('TOKENIZED_DATA_DIR')
# Ensure the path exists or create it if necessary
os.makedirs(TOKENIZED_DATA_DIR, exist_ok=True)

# Load and return the dataset from the jsonl file
def load_data(data_path):
    return load_dataset('json', data_files=data_path)['train']

# Save the dataset to disk for further processing
def save_dataset(dataset, save_path):
    # Save the dataset as a serialized file (e.g., in Parquet format for efficiency)
    dataset.save_to_disk(save_path)

if __name__ == '__main__':
    # Load the data
    dataset = load_data(DATA_PATH)
    
    # Save the dataset to be used by the next step
    save_dataset(dataset, TOKENIZED_DATA_DIR)
