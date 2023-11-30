import torch
from transformers import GPT2Tokenizer, GPT2ForSequenceClassification, Trainer, TrainingArguments
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

# Load the pre-trained model for sequence classification
model = GPT2ForSequenceClassification.from_pretrained(MODEL_PATH, num_labels=2)  # Assuming binary classification
model.config.pad_token_id = tokenizer.eos_token_id  # Make sure the model recognizes the pad token
model.to(device)

# Load the tokenized datasets
tokenized_datasets = load_from_disk(TOKENIZED_DATA_DIR)

# Define training arguments
training_args = TrainingArguments(
    output_dir='./Fine-Tuned/Model',
    overwrite_output_dir=True,
    do_train=True,
    per_device_train_batch_size=4,
    num_train_epochs=3,
    logging_dir='./Logs',
    logging_steps=100,
    save_strategy="no",  # Save strategy set to 'no' to prevent saving checkpoints
    save_total_limit=1  # Limit the total amount of checkpoints, keeping only the best one
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

    # Saving the model may be moved out of `train_model` to make it more explicit
    model.save_pretrained(training_args.output_dir)
    # Save the tokenizer if you have made any changes or updates
    tokenizer.save_pretrained(training_args.output_dir)