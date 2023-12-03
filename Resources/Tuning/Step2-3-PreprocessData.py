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
    inputs, targets = [], []
    # Iterate through each example in the batch
    for example in examples['messages']:
        # Since 'messages' is a batch, 'example' is a list of dictionaries now
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
        
        # Don't forget to add the last target_text to targets if the last message was from the assistant
        if last_role == "assistant":
            inputs.append(input_text)
            targets.append(target_text)
            
    # Tokenize and pad the sequences to the same length
    model_inputs = tokenizer(inputs, max_length=512, padding="max_length", truncation=True)
    # With the new version you can just use tokenizer(..., text_target=targets)
    labels = tokenizer(text_target=targets, max_length=512, padding="max_length", truncation=True)["input_ids"]

    model_inputs["labels"] = labels
    return model_inputs

if __name__ == '__main__':
    # Load the dataset from the jsonl file
    raw_datasets = load_dataset('json', data_files=DATA_PATH)

    # Preprocess the dataset and store it in TOKENIZED_DATA_DIR
    tokenized_datasets = raw_datasets.map(preprocess_function, batched=True, remove_columns=['messages'])
    tokenized_datasets.save_to_disk(TOKENIZED_DATA_DIR)

    print(f"Tokenized datasets saved to {TOKENIZED_DATA_DIR}")