"""
This script splits the full dataset into training, validation, and test subsets.
The dataset is expected to be in JSON Lines (jsonl) format.

The splits are as follows:
- Training Data (train): This is the main part of the dataset used for training the model.
- Validation Data (validation): This data is used to evaluate the model during training and to fine-tune hyperparameters.
- Test Data (test): This is used to provide an unbiased evaluation of the final model's performance.
"""

import json
from sklearn.model_selection import train_test_split

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

# Function to load the dataset from a jsonl file
def load_dataset_from_jsonl(file_path):
    with open(file_path, 'r', encoding='utf-8') as file:
        return [json.loads(line) for line in file]

# Function to save datasets into jsonl files
def save_to_jsonl(data, file_path):
    with open(file_path, 'w', encoding='utf-8') as file:
        for entry in data:
            file.write(f'{json.dumps(entry)}\n')
    print(f"Saved file: {file_path}")

def main():
    # Load the full dataset from 'data.jsonl'
    full_dataset = load_dataset_from_jsonl(DATA_PATH)
    
    # Shuffle and split the dataset
    train, temp = train_test_split(full_dataset, train_size=0.8, random_state=42)  # 80% training, 20% temp
    validation, test = train_test_split(temp, train_size=0.5, random_state=42)  # Split the temp equally into validation and test

    # Save splits into separate jsonl files
    save_to_jsonl(train, DATA_TRAIN_PATH)
    save_to_jsonl(validation, DATA_VALIDATION_PATH)
    save_to_jsonl(test, DATA_TEST_PATH)
    
if __name__ == '__main__':
    main()
