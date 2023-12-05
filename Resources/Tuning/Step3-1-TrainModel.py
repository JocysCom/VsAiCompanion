# This script is dedicated to training the model with a GPU if available. It imports the appropriate classes and methods from PyTorch, transformers, and datasets libraries. The script defines a helper function get_device to determine if a GPU is available for training, and it sets up the training device accordingly. It then sets the paths to the model and tokenized data, loads the tokenizer and model, configures the model with the pad token, and loads the tokenized dataset. Training arguments are defined for fine-tuning the model, tailored specifically for language generation tasks.

# The train_model function initializes the Trainer class with the model, training arguments, and training dataset, and it begins training. After training, the script saves the fine-tuned model and tokenizer to the pre-defined output directory. This script is designed as a main program that performs the training and can be executed directly to fine-tune the model.

import os
import torch
import json
from transformers import AutoModelForCausalLM, AutoTokenizer, Trainer, TrainingArguments
from datasets import load_from_disk

# Load configuration from a JSON file
with open('Step0-1-Config.json', 'r') as config_file:
    config = json.load(config_file)

# Path to the .pem file that contains the trusted root certificates
CERT_FILE_PATH = config.get('CERT_FILE_PATH')
# Specify the model name (as from Hugging Face Model Hub)
MODEL_NAME = config.get('MODEL_NAME')
# Specify the path to tokenized data
TOKENIZED_DATA_DIR = config.get('TOKENIZED_DATA_DIR')
# Define where you would like to cache models and tokenizers.
CACHE_DIR = config.get('CACHE_DIR')
# Define where you would like to save the fine-tuned model and tokenizer.
NEW_OUTPUT_DIR = config.get('NEW_OUTPUT_DIR')

# Only set the REQUESTS_CA_BUNDLE environment variable if the certificate file exists and is not empty
if os.path.exists(CERT_FILE_PATH) and os.path.getsize(CERT_FILE_PATH) > 0:
    os.environ['REQUESTS_CA_BUNDLE'] = os.path.abspath(CERT_FILE_PATH)

def get_device():
    # Check for available GPU
    if torch.cuda.is_available():
        return 'cuda'
    else:
        return 'cpu'
    
# Function to check if the GPU supports Tensor Cores
def supports_tensor_cores():
    if torch.cuda.is_available():
        compute_capability = torch.cuda.get_device_capability(torch.cuda.current_device())
        # Tensor cores are supported on devices with compute capability of 7.0 and higher
        return compute_capability[0] >= 7
    return False

# Function to get training arguments with fp16 set if Tensor Cores are supported
def get_training_arguments():
    use_fp16 = supports_tensor_cores()
    # Define training arguments tailored for Orca-2-7b
    training_args = TrainingArguments(
        output_dir=NEW_OUTPUT_DIR,
        overwrite_output_dir=True,
        do_train=True,
        per_device_train_batch_size=4,
        num_train_epochs=3,
        logging_dir='./Logs',
        logging_steps=100,
        save_strategy="steps",
        save_steps=500,
        evaluation_strategy="steps",
        warmup_steps=100,
        weight_decay=0.01,
        fp16=use_fp16,  # Enable mixed precision training if Tensor Cores are supported
        # You may want to omit prediction_loss_only or set additional parameters for Orca
    )
    return training_args

device = get_device()

# Load the tokenizer and model specific to the Orca-2-7b
tokenizer = AutoTokenizer.from_pretrained(MODEL_NAME, cache_dir=CACHE_DIR)
model = AutoModelForCausalLM.from_pretrained(MODEL_NAME, cache_dir=CACHE_DIR)

model.to(device)

# Load the tokenized datasets from disk
tokenized_datasets = load_from_disk(TOKENIZED_DATA_DIR)

# Use the get_training_arguments function to configure training
training_args = get_training_arguments()

# Initialize and train the model
def train_model(training_args, model, tokenized_datasets):
    trainer = Trainer(
        model=model,
        args=training_args,
        train_dataset=tokenized_datasets['train'],  # Provide the training dataset
    )
    trainer.train()

if __name__ == '__main__':
    # Train the model using the tokenized data
    train_model(training_args, model, tokenized_datasets)

    # Save the final model and tokenizer
    model.save_pretrained(training_args.output_dir)
    tokenizer.save_pretrained(training_args.output_dir)
