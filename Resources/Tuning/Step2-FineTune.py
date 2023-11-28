import json
from datasets import load_dataset
from transformers import AutoModelForSequenceClassification, AutoTokenizer, Trainer, TrainingArguments

# Load your dataset
data_files = {"train": "data.jsonl"}
dataset = load_dataset('json', data_files=data_files)

# Preprocess the dataset
model_name = "bert-base-uncased"  # Replace with your chosen pre-trained model
tokenizer = AutoTokenizer.from_pretrained(model_name)

def tokenize_function(examples):
    return tokenizer(examples["text"], padding="max_length", truncation=True)

tokenized_datasets = dataset.map(tokenize_function, batched=True)

# Load the pre-trained model
model = AutoModelForSequenceClassification.from_pretrained(model_name, num_labels=2)

# Define the training arguments
training_args = TrainingArguments(
    output_dir="./results",
    num_train_epochs=3,       # number of training epochs, adjust as needed
    per_device_train_batch_size=16,  # adjust based on your GPU memory
    warmup_steps=500,         # number of warmup steps for learning rate scheduler
    weight_decay=0.01,        # strength of weight decay
    logging_dir='./logs',     # directory for storing logs
    logging_steps=10,
)

# Initialize the Trainer
trainer = Trainer(
    model=model,
    args=training_args,
    train_dataset=tokenized_datasets["train"],
)

# Start training
trainer.train()

# Save the fine-tuned model
trainer.save_model()