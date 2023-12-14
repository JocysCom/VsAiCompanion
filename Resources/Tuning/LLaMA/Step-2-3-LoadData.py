# This script defines a function for loading training data from a .jsonl file format and preparing it to be used for fine-tuning. The script uses the datasets module from Hugging Face to perform this task. The function load_data is provided with a path to the training data file, which defaults to ./Data/data.jsonl, and returns a loaded dataset object. The actual usage of this dataset, such as saving it to a file or continuing with the processing, is implied in comments but not explicitly implemented in the script.

import os
import shutil
import logging
import json
from datasets import load_dataset, Dataset, DatasetDict

# Setup logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Load configuration from a JSON file
with open('Step-0-1-Config.json', 'r') as config_file:
    config = json.load(config_file)

# DATA_PATH points to the JSONL file with this format:
# {"messages":[{"role":"system","content":"You are a helpful, respectful and honest assistant of Doughnut Dynamics."},{"role":"user","content":"What is the name of your company?"},{"role":"assistant","content":"The name of our company is Doughnut Dynamics."}]}
# {"messages":[{"role":"system","content":"You are a helpful, respectful and honest assistant of Doughnut Dynamics."},{"role":"user","content":"What is your company's slogan?"},{"role":"assistant","content":"Our company's slogan is 'Donut worry, be happy!'"}]}
DATA_PATH = config.get('DATA_PATH')
DATA_TRAIN_PATH = config.get('DATA_TRAIN_PATH')
DATA_VALIDATION_PATH = config.get('DATA_VALIDATION_PATH')
DATA_TEST_PATH = config.get('DATA_TEST_PATH')

logger.info(f"Cleanup {config['TOKENIZED_DATA_COMBINED_DIR']}")
# Check if the directory exists
if os.path.exists(config['TOKENIZED_DATA_COMBINED_DIR']):
    # If it exists, remove it along with all its contents
    shutil.rmtree(config['TOKENIZED_DATA_COMBINED_DIR'])
# Create the directory afresh
os.makedirs(config['TOKENIZED_DATA_COMBINED_DIR'], exist_ok=True)

# Function to load the dataset from a jsonl file
def load_dataset_from_jsonl(file_path):
    with open(file_path, 'r', encoding='utf-8') as file:
        data = [json.loads(line) for line in file]
    if not data:
        raise Exception("Training data is empty or not loaded correctly.")
    return Dataset.from_dict({"messages": data})

def main():
    # Load each subset of the dataset
    train_dataset = load_dataset_from_jsonl(DATA_TRAIN_PATH)
    validation_dataset = load_dataset_from_jsonl(DATA_VALIDATION_PATH)
    test_dataset = load_dataset_from_jsonl(DATA_TEST_PATH)
    
    # Combine the datasets into a DatasetDict
    combined_datasets = DatasetDict({
        'train': train_dataset,
        'validation': validation_dataset,
        'test': test_dataset
    })
    
    # Further processing can be done here if necessary
    
    # Optionally save the combined datasets
    combined_datasets.save_to_disk(config['TOKENIZED_DATA_COMBINED_DIR'])

if __name__ == '__main__':
    main()
