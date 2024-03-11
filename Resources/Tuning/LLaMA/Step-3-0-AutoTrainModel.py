import os
import json
from transformers import AutoTokenizer, AutoModelForCausalLM

# Load configuration from a JSON file
with open('Step-0-1-Config.json', 'r') as config_file:
    config = json.load(config_file)

# Autotrain command template with parameters
autotrain_command = (
    f"autotrain llm --train "
    f"--project-name {config['PROJECT_NAME']} "  # Set the project name for your training
    f"--model {config['MODEL_NAME']} "  # Specify the model name you want to train
    f"--data-path {config['TRAINING_DATA_PATH']} "  # Define the path to your dataset
    f"--model_max_length {config['OUTPUT_MAX_LENGTH']} "  # Set the maximum sequence length for the model
    f"--text_column text " # text column with instructions and response.
    f"--peft "            # Enable precision equivalence fine-tuning (PEFT)
    f"--train-batch-size 2 "  # Set the training batch size
    f"--epochs 3 "  # Define the number of training epochs
    f"--trainer sft "         # Specify the trainer to use, e.g., sft
)

# Now you can execute the command using os.system or subprocess.run, for example:
os.system(autotrain_command)