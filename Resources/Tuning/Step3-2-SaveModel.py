# This script saves the fine-tuned model and tokenizer. It imports necessary classes from the transformers and datasets libraries. The script defines the paths where the model, tokenizer, and tokenized data are stored. It loads a fine-tuned model for sequence classification and the corresponding tokenizer with special padding token adjustment. The script also loads the tokenized datasets used for training. It initializes TrainingArguments and Trainer objects (although specific training arguments are omitted in the snippet), which are used to configure the training process. In the __main__ section, the script saves the fine-tuned model and tokenizer to the pre-defined directory and informs the user of the successful saving process.

from transformers import AutoModelForCausalLM, AutoTokenizer, Trainer, TrainingArguments
from datasets import load_from_disk

# Assume we are in the same environment as when training was done
MODEL_DIR = './Fine-Tuned/Model'
TOKENIZED_DATA_DIR = './Data/tokenized_data'

# Load tokenizer and model specific to the Orca-2-7b
tokenizer = AutoTokenizer.from_pretrained(MODEL_DIR)
model = AutoModelForCausalLM.from_pretrained(MODEL_DIR)

# Load the tokenized data that was used for training, to get the train_dataset
tokenized_datasets = load_from_disk(TOKENIZED_DATA_DIR)
train_dataset = tokenized_datasets["train"]

# Initialize TrainingArguments
# Ensure it matches the TrainingArguments used during training
training_args = TrainingArguments(
    output_dir=MODEL_DIR,
    # ... other arguments used during training ...
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