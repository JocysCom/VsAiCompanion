# This script preprocesses the training data for fine-tuning a model. It loads the tokenizer, sets the padding token, and defines a preprocess_function which tokenizes and organizes the messages from users and assistants into input and output pairs. This preprocess_function is then applied to the loaded dataset using the map function from the Hugging Face datasets library, followed by saving the tokenized datasets to disk. The script handles tokenization, sequence padding, and truncation. It is meant to be executed as the main program, performing preprocessing and saving the ready-to-use data for training.

import os
import json
from transformers import AutoTokenizer
from datasets import load_from_disk, DatasetDict, Dataset

# Load configuration from a JSON file
with open('Step0-1-Config.json', 'r') as config_file:
    config = json.load(config_file)


# Only set the REQUESTS_CA_BUNDLE environment variable if the certificate file exists and is not empty
if os.path.exists(config['CERT_FILE_PATH']) and os.path.getsize(config['CERT_FILE_PATH']) > 0:
    os.environ['REQUESTS_CA_BUNDLE'] = os.path.abspath(config['CERT_FILE_PATH'])

def preprocess_function(examples, tokenizer):
    # Tokenize each example using the format mentioned in the JSONL data
    input_texts = []
    target_texts = []
    # Note: examples['messages'] is a list containing dictionary with a key 'messages'
    for example in examples['messages']:
        input_text = ''
        target_text = ''
        # The actual 'message' is wrapped in a dictionary under 'messages' key
        for msg in example['messages']:
            if msg['role'] == 'user':
                input_text += msg['content'] + ' '
            elif msg['role'] == 'assistant':
                target_text += msg['content'] + ' '
        input_texts.append(input_text.strip())
        target_texts.append(target_text.strip())

    # Tokenize texts
    inputs = tokenizer(input_texts, padding='max_length', truncation=True, return_tensors='np')
    with tokenizer.as_target_tokenizer():
        labels = tokenizer(target_texts, padding='max_length', truncation=True, return_tensors='np')['input_ids']
    # We can't return NumPy arrays, so we have to convert them to lists
    inputs = {k: v.tolist() for k, v in inputs.items()}
    inputs['labels'] = labels.tolist()
    return inputs

def tokenize_datasets(tokenizer, dataset_dict):
    # Apply a map function to preprocess and tokenize the data from DatasetDict
    tokenized_dataset_dict = DatasetDict()
    for split, dataset in dataset_dict.items():
        tokenized_dataset_dict[split] = dataset.map(
            lambda examples: preprocess_function(examples, tokenizer), batched=True, remove_columns=dataset.column_names
        )
    return tokenized_dataset_dict

def main():
    # Variables for subdirectories of each split
    combined_dir = config['TOKENIZED_DATA_COMBINED_DIR']
    train_dir = os.path.join(combined_dir, 'train')
    validation_dir = os.path.join(combined_dir, 'validation')
    test_dir = os.path.join(combined_dir, 'test')
    
    # Manually load each dataset subset
    train_dataset = Dataset.load_from_disk(train_dir)
    validation_dataset = Dataset.load_from_disk(validation_dir)
    test_dataset = Dataset.load_from_disk(test_dir)
    
    # Create a new DatasetDict
    datasets = DatasetDict({
        'train': train_dataset,
        'validation': validation_dataset,
        'test': test_dataset
    })
    
    # Initialize the tokenizer for the specified model
    tokenizer = AutoTokenizer.from_pretrained(config['MODEL_NAME'], cache_dir=config['CACHE_DIR'])
    
    # Tokenize the datasets for each split ('train', 'validation', 'test')
    tokenized_datasets = tokenize_datasets(tokenizer, datasets)
    
    # Save the tokenized datasets to disk for future loading
    tokenized_datasets.save_to_disk(config['TOKENIZED_DATA_OUTPUT_DIR'])

if __name__ == '__main__':
    main()
