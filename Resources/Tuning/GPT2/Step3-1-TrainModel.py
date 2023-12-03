# This script is dedicated to training the GPT-2 model with a GPU if available. It imports the appropriate classes and methods from PyTorch, transformers, and datasets libraries. The script defines a helper function get_device to determine if a GPU is available for training, and it sets up the training device accordingly. It then sets the paths to the model and tokenized data, loads the tokenizer and model, configures the model with the pad token, and loads the tokenized dataset. Training arguments are defined for fine-tuning the model, tailored specifically for language generation tasks.

# The train_model function initializes the Trainer class with the model, training arguments, and training dataset, and it begins training. After training, the script saves the fine-tuned model and tokenizer to the pre-defined output directory. This script is designed as a main program that performs the training and can be executed directly to fine-tune the model.

import torch
from transformers import GPT2Tokenizer, GPT2LMHeadModel, Trainer, TrainingArguments
from datasets import load_from_disk

def get_device():
    if torch.cuda.is_available():
        return 'cuda'
    else:
        return 'cpu'
device = get_device()

# Specify the actual model and tokenized data paths
MODEL_PATH = './Models/OpenAI/GPT2/'
TOKENIZED_DATA_DIR = './Data/tokenized_data'

# Load tokenizer and model
tokenizer = GPT2Tokenizer.from_pretrained(MODEL_PATH)
tokenizer.pad_token = tokenizer.eos_token  # Set pad_token to eos_token for GPT-2

# Load the pre-trained GPT-2 generative model
model = GPT2LMHeadModel.from_pretrained(MODEL_PATH)
model.config.pad_token_id = tokenizer.eos_token_id  # Make sure the model recognizes the pad token
model.to(device)

# Load the tokenized datasets
tokenized_datasets = load_from_disk(TOKENIZED_DATA_DIR)

# Define training arguments tailored for language generation
training_args = TrainingArguments(
    output_dir='./Fine-Tuned/Model',
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
    prediction_loss_only=True,  # For language modelling, we are typically only interested in the loss
)

# Initialize and train the model
def train_model(training_args, model, tokenized_datasets):
    trainer = Trainer(
        model=model,
        args=training_args,
        train_dataset=tokenized_datasets["train"],  # Provide the training dataset
    )
    trainer.train()

if __name__ == '__main__':
    # Train the model using the tokenized data
    train_model(training_args, model, tokenized_datasets)

    # If you wish to save checkpoints during training, consider defining a save strategy in TrainingArguments
    # Otherwise, save the final model and tokenizer
    model.save_pretrained(training_args.output_dir)
    tokenizer.save_pretrained(training_args.output_dir)