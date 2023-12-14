# This script is dedicated to training the model with a GPU if available. It imports the appropriate classes and methods from PyTorch, transformers, and datasets libraries. The script defines a helper function get_device to determine if a GPU is available for training, and it sets up the training device accordingly. It then sets the paths to the model and tokenized data, loads the tokenizer and model, configures the model with the pad token, and loads the tokenized dataset. Training arguments are defined for fine-tuning the model, tailored specifically for language generation tasks.

# The train_model function initializes the Trainer class with the model, training arguments, and training dataset, and it begins training. After training, the script saves the fine-tuned model and tokenizer to the pre-defined output directory. This script is designed as a main program that performs the training and can be executed directly to fine-tune the model.

import os
import shutil
import psutil
import json
import logging
import numpy as np
import torch
import transformers
from transformers import AutoTokenizer, AutoModelForCausalLM, Trainer, TrainingArguments, TrainerCallback
from datasets import load_from_disk
import gc

def clear_gpu_memory():
    torch.cuda.empty_cache()
    gc.collect()

clear_gpu_memory()

#transformers.logging.set_verbosity_info()
transformers.logging.set_verbosity_debug()

# Setup logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Log the starting of the training process
logger.info("Starting the training process...")

# Load configuration from a JSON file
with open('Step-0-1-Config.json', 'r') as config_file:
    config = json.load(config_file)

logger.info(f"Cleanup {config['NEW_OUTPUT_DIR']}")
# Check if the directory exists
if os.path.exists(config['NEW_OUTPUT_DIR']):
    # If it exists, remove it along with all its contents
    shutil.rmtree(config['NEW_OUTPUT_DIR'])
# Create the directory afresh
os.makedirs(config['NEW_OUTPUT_DIR'], exist_ok=True)

# Only set the REQUESTS_CA_BUNDLE environment variable if the certificate file exists and is not empty
if os.path.exists(config['CERT_FILE_PATH']) and os.path.getsize(config['CERT_FILE_PATH']) > 0:
    os.environ['REQUESTS_CA_BUNDLE'] = os.path.abspath(config['CERT_FILE_PATH'])


class ErrorLoggingCallback(TrainerCallback):
    def on_step_end(self, args, state, control, logs=None, **kwargs):
        try:
            # This is where you could add any custom logic you want to execute at the end of each step.
            # For example, logging certain variables, or performing some checks.
            # If there's nothing specific you want to do, you can leave this section empty.
            pass
        except Exception as e:
            logger.error(f"Exception on step {state.global_step}: {e}")
            # Optionally, you can add a control mechanism to stop training if an error occurs
            control.should_training_stop = True

def get_device():
    use_gpu = False
    # Check for available GPU
    if torch.cuda.is_available() & config["USE_GPU_CUDA"]:
        # Print information about GPU CUDA device.
        logger.info(f"CUDA Devices Count: {torch.cuda.device_count()}")
        logger.info(f"CUDA Device Name: {torch.cuda.get_device_name(0)}") 
        logger.info(f"Tensor Cores: {supports_tensor_cores()}") 
        # Use GPU (indexing starts at 0)
        device = torch.device("cuda:0")
        # Generate some data and compute on GPU
        x_gpu = torch.randn(3, 3, device=device)
        y_gpu = x_gpu * 2 + 1
        z_gpu = y_gpu.cpu()  # Bring the result back to CPU to compare
        # Compute the same operations on CPU
        x_cpu = x_gpu.cpu()
        y_cpu = x_cpu * 2 + 1
         # Validate the results are the same (within a tolerance)
        if torch.allclose(z_gpu, y_cpu, atol=1e-6):
            logger.info("PASS: CUDA computation is correct. Results match CPU results.")
            use_gpu = True
        else:
            logger.info("FAIL: CUDA computation does not match CPU results.")
            logger.info("GPU result:\n", z_gpu)
            logger.info("Reference CPU result:\n", y_cpu)
    # If use GPU, then...
    if use_gpu:
        logger.info("Use GPU")
        os.environ['NCCL_P2P_DISABLE'] = "1"
        os.environ['NCCL_IB_DISABLE'] = "1"
        os.environ['CUDA_LAUNCH_BLOCKING'] = "1"
        os.environ['TORCH_USE_CUDA_DSA'] = "1"
        os.environ['PYTORCH_CUDA_ALLOC_CONF'] = "max_split_size_mb:128"
    else:
        # Log the starting of the training process
        logger.info("Use CPU")
        device = torch.device("cpu")
    return device
    
# Function to check if the GPU supports Tensor Cores
def supports_tensor_cores():
    if torch.cuda.is_available():
        compute_capability = torch.cuda.get_device_capability(torch.cuda.current_device())
        # Tensor cores are supported on devices with compute capability of 7.0 and higher
        return compute_capability[0] >= 7
    return False

# Check if the device has Tensor Cores which support FP16
use_fp16 = supports_tensor_cores() and config["USE_GPU_TENSOR"]

# Function to get training arguments with fp16 set if Tensor Cores are supported
def get_training_arguments():
    logger.info("Get training arguments")
    # Define training arguments
    training_args = TrainingArguments(
        output_dir=config['NEW_OUTPUT_DIR'],
        overwrite_output_dir=True,
        do_train=True,
        # Further reduce batch size if necessary
        per_device_train_batch_size=1,
        per_device_eval_batch_size=1,
        # Adjust based on GPU memory after running tests
        # Increase to reduce memory usage
        gradient_accumulation_steps=64,
        # If your sequences are too long, decreasing this can help
        # max_seq_length=128 or another lower value
        num_train_epochs=3,
        logging_strategy="steps",
        logging_dir='./Logs',
        logging_first_step=True,
        logging_steps=10,
        log_level='debug',
        report_to="all",
        save_strategy="steps",
        save_steps=500,
        save_total_limit=2,  # If set, limit the total amount of checkpoints.
        evaluation_strategy="steps",
        warmup_steps=100,
        weight_decay=0.01,
        # You may reduce the number of dataloading workers if memory is an issue
        # dataloader_num_workers=1 or 0
        fp16=use_fp16,
        # Additional parameters may be set as necessary
        # You can uncomment this if you want the script to clear the CUDA cache
        # disable_tqdm=False,
    )
    if use_fp16:
        # Uncommend and add to the script if you want to force clear CUDA cache
        torch.cuda.empty_cache()
    return training_args

def estimate_requirements(device, num_parameters, batch_size, sequence_length, precision='fp32', dataset_size=None, num_checkpoints_to_keep=2):
    # Dynamically retrieve the available VRAM, RAM, and SSD space
    vram_gb = torch.cuda.get_device_properties(device).total_memory / (1024**3) if torch.cuda.is_available() else 0
    ram_gb = psutil.virtual_memory().total / (1024**3)
    ssd_gb = shutil.disk_usage("/").total / (1024**3)
    # 4 bytes per parameter for fp32, 2 bytes per parameter for fp16
    bytes_per_param = 4 if precision == 'fp32' else 2
    # Size of each checkpoint and total checkpoints size
    checkpoint_size_gb = (num_parameters * bytes_per_param) / (1024**3)
    total_checkpoints_size_gb = checkpoint_size_gb * num_checkpoints_to_keep
    # Estimate SSD space required (checkpoints + dataset)
    ssd_usage_gb = total_checkpoints_size_gb + (dataset_size if dataset_size else 0)
    # Estimate VRAM usage
    vram_usage_per_batch = (num_parameters * batch_size * sequence_length * bytes_per_param) / (1024**3)
    # Estimate VRAM required for the model itself (rough estimation)
    vram_usage_static = (num_parameters * bytes_per_param) / (1024**3)
    # Estimate total VRAM usage
    total_vram_usage = vram_usage_per_batch + vram_usage_static
    # Estimate RAM usage (assuming it's about twice the VRAM usage for simplicity)
    total_ram_usage = total_vram_usage * 2
    # Return estimations
    logger.info(f'VRAM_Required_GB: {total_vram_usage}')
    logger.info(f'VRAM_Available_GB: {vram_gb}')
    logger.info(f'RAM_Required_GB: {total_ram_usage}')
    logger.info(f'RAM_Available_GB: {ram_gb}')
    logger.info(f'SSD_Required_GB: {ssd_usage_gb}')
    logger.info(f'SSD_Available_GB: {ssd_gb}')
    return 0

device = get_device()

if use_fp16:
    # Uncommend and add to the script if you want to force clear CUDA cache
    torch.cuda.empty_cache()

# Load the tokenizer and model specific to the Orca-2-7b
logger.info(f"Loading tokenizer...")
tokenizer = AutoTokenizer.from_pretrained(config['MODEL_NAME'], cache_dir=config['CACHE_DIR'])

try:
    logger.info(f"Loading model...")
    model = AutoModelForCausalLM.from_pretrained(config['MODEL_NAME'], cache_dir=config['CACHE_DIR'])
    logger.info(f"...done")
except Exception as e:
    logger.error(f"An error occurred while loading the model: {e}", exc_info=True)
    #raise e
    exit(1)

# Ensure the model and tokenizer are correctly loaded
if not model or not tokenizer:
    raise ValueError("The model or tokenizer could not be loaded. Please check the MODEL_NAME and paths.")

model.to(device)

# Load the tokenized datasets from disk
tokenized_datasets = load_from_disk(config['TOKENIZED_DATA_OUTPUT_DIR'])
logger.info(f"Dataset loaded with {len(tokenized_datasets)} examples")

# Check if 'train' split is available in tokenized datasets
if 'train' not in tokenized_datasets:
    raise ValueError("The 'train' split does not exist in the tokenized datasets. Please ensure that the dataset has been properly split and tokenized.")

# Use the get_training_arguments function to configure training
training_args = get_training_arguments()

def calculate_dataset_size_gb(tokenized_datasets):
    # Size of a token in bytes, this is just an approximation and you may need to adjust it for your case
    average_token_size_bytes = 8
    # Calculate the total number of tokens. This assumes that tokenized_datasets is a DatasetDict containing at least a 'train' dataset
    total_num_tokens = sum(len(tokenized_datasets[split]['input_ids']) for split in tokenized_datasets)
    # Calculate the average number of tokens per example
    average_num_tokens_per_example = np.mean([len(tokenized_datasets[split]['input_ids'][0]) for split in tokenized_datasets])
    # Calculate the total size in bytes
    total_size_bytes = total_num_tokens * average_num_tokens_per_example * average_token_size_bytes
    # Convert to GB
    dataset_size_gb = total_size_bytes / (1024 ** 3)
    return dataset_size_gb

# Initialize and train the model
def train_model(training_args, model, tokenized_datasets):
    logger.info("Initializing the Trainer")
    logger.info("Training requires a significant amount of disk space.")
    logger.info("Training may fail silently if there is insufficient space. For example:")
    logger.info("For a 7-billion parameter model using 32-bit precision:")
    logger.info("7,000,000,000 parameters * 4 bytes/parameter = 28,000,000,000 bytes ≈ 28 GB per checkpoint.")
    logger.info("For a 13-billion parameter model using 32-bit precision:")
    logger.info("13,000,000,000 parameters * 4 bytes/parameter = 52,000,000,000 bytes ≈ 52 GB per checkpoint.")
    logger.info("A prudent recommendation would be to have at least an order of magnitude more space than the total size of all expected checkpoints.")
    logger.info("For a 7B model, 500 GB would be advisable.")
    logger.info("For a 13B model, 1 TB would be advisable.")

    # This function will be called with the actual tokenized_datasets variable
    # Example:
    # dataset_size_gb = calculate_dataset_size_gb(tokenized_datasets)
    # The model variable is an instance of the `AutoModelForCausalLM` class.
    # Retrieve the actual number of parameters from the model
    num_params = model.num_parameters()
    # Retrieve the batch size from training arguments
    batch_size = training_args.per_device_train_batch_size * torch.cuda.device_count()
    # Retrieve the sequence length based on the model's configuration
    sequence_len = model.config.n_positions if hasattr(model.config, 'n_positions') else 512  # Default value if not found
    # Calculate dataset size in GB if possible, otherwise use a default value or perform a calculation based on the tokenized dataset size
    # Here we use a placeholder function `calculate_dataset_size_gb(tokenized_datasets)` which you need to define based on your dataset.
    dataset_size_gb = calculate_dataset_size_gb(tokenized_datasets)
    # Determine if FP16 is used
    precision = 'fp16' if training_args.fp16 else 'fp32'
    logger.info("estimate_requirements")
    estimate_requirements(device, num_params, batch_size, sequence_len, precision, dataset_size_gb, 2)

    trainer = Trainer(
        model=model,
        args=training_args,
        train_dataset=tokenized_datasets['train'],
        callbacks=[ErrorLoggingCallback]
    )
    logger.info("Starting training...")
    if use_fp16:
        torch.cuda.empty_cache()  # Clear CUDA cache to free up unused memory
    trainer.train()
    logger.info("Training completed")

if __name__ == '__main__':
    # Train the model using the tokenized data
    train_model(training_args, model, tokenized_datasets)

    # Save the final model and tokenizer
    model.save_pretrained(training_args.output_dir)
    tokenizer.save_pretrained(training_args.output_dir)

    # Log the completion of the training process
    logger.info("Training process completed. Saving the model and tokenizer...")
