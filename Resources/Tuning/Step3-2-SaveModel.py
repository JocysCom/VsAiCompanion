from transformers import Trainer, GPT2Tokenizer, GPT2ForSequenceClassification, TrainingArguments
from datasets import load_from_disk

# Assume we are in the same environment as when training was done
MODEL_DIR = './Fine-Tuned/Model'
TOKENIZED_DATA_DIR = './Data/tokenized_data'
MODEL_PATH = './Models/OpenAI/GPT2/'

# Load tokenizer
tokenizer = GPT2Tokenizer.from_pretrained(MODEL_PATH)
tokenizer.pad_token = tokenizer.eos_token  # Set the padding token

# Load the fine-tuned model - ensure this matches the TrainingArguments in Step2-3-TrainModel.py
model = GPT2ForSequenceClassification.from_pretrained(MODEL_DIR)

# Load the tokenized data that was used for training, to get the train_dataset
tokenized_datasets = load_from_disk(TOKENIZED_DATA_DIR)
train_dataset = tokenized_datasets["train"]

# Initialize TrainingArguments - ensure this matches the TrainingArguments in Step2-3-TrainModel.py
training_args = TrainingArguments(
    output_dir=MODEL_DIR,
    # ... other arguments ...
)

# Initialize Trainer
trainer = Trainer(
    model=model,
    args=training_args,
    train_dataset=train_dataset,
)

if __name__ == '__main__':
    # Save the model and tokenizer
    model.save_pretrained(MODEL_DIR)
    tokenizer.save_pretrained(MODEL_DIR)

    print(f"Model and tokenizer have been saved to {MODEL_DIR}")