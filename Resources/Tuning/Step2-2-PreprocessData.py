from datasets import load_dataset
from transformers import GPT2Tokenizer

# Define paths
MODEL_PATH = './Models/OpenAI/GPT2/'
DATA_PATH = './Data/data.jsonl'
TOKENIZED_DATA_DIR = './Data/tokenized_data'

# Load tokenizer and set padding token
tokenizer = GPT2Tokenizer.from_pretrained(MODEL_PATH)
tokenizer.pad_token = tokenizer.eos_token

def preprocess_function(examples):
    # Initialize lists to store outputs
    concatenated_messages = []
    labels = []

    # Process each conversation
    for example in examples['messages']:
        # Concatenate contents of messages within a single conversation
        conversation_content = " ".join([message['content'] for message in example])
        concatenated_messages.append(conversation_content)
        # For simplicity, label the whole conversation as 'user' (0) or 'assistant' (1)
        # You may change this logic to suit your actual use case
        if any(message['role'] == 'assistant' for message in example):
            labels.append(1)
        else:
            labels.append(0)
    
    # Tokenize conversations and append labels
    tokenized_inputs = tokenizer(concatenated_messages, padding='max_length', truncation=True)
    tokenized_inputs['labels'] = labels

    return tokenized_inputs

if __name__ == '__main__':
    # Load the dataset from the jsonl file
    raw_datasets = load_dataset('json', data_files=DATA_PATH)

    # Preprocess the dataset and store it in TOKENIZED_DATA_DIR
    tokenized_datasets = raw_datasets.map(preprocess_function, batched=True, remove_columns=['messages'])
    tokenized_datasets.save_to_disk(TOKENIZED_DATA_DIR)
    
    print(f"Tokenized datasets saved to {TOKENIZED_DATA_DIR}")