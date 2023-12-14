# AI Model Fine-Tuning Scripts

## Scripts and Configuration Files Overview

Here is the list and description of the script files and configuration files:

`Instructions.txt`
- Prompt template for AI models to assist in solving problems with AI training scripts.

`Step0-1-Config.json`
- Configuration file for the AI model training process, containing model parameters, data paths, cache directories, and GPU usage settings.

`Step-1-1-SaveTrustedRootCertificates.ps1`
- Retrieves and saves corporate root certificates from specified hostnames to ensure secure connections for downloading resources.

`Step-1-3-CreatePythonEnvironment.ps1`
- Sets up a dedicated Python environment "tuning" and installs the necessary packages, with optional support for NVIDIA CUDA.

`Step-1-2-DownloadFiles.ps1`
- Script responsible for downloading necessary files and tools required for the fine-tuning of the AI model.

`Step-2-2-SplitData.py`
- Python script for splitting the `data.jsonl` file into training, validation, and testing datasets for the model training process.

`Step-2-2-SplitData-data.jsonl`
- Example of `data.jsonl` file.

`Step-2-2-SplitData-data.yaml`
- An OpenAPI specification detailing the API endpoints to retrieve conversation messages by ID for structuring conversation data used for fine-tuning.

`Step-2-3-LoadData.py`
- A Python script that loads the fine-tuning data from data.jsonl into a Hugging Face DatasetDict, performing necessary data preparation and optionally saving the processed datasets to disk for further use.

`Step-2-3-PreprocessData.py`
- A Python script that preprocesses the dataset by tokenizing and organizing conversational messages into inputs and outputs suitable for model training. It utilizes the `transformers` library to load a tokenizer that is used to tokenize the texts from `data.jsonl`. The script performs necessary preparations such as sequence padding and truncation, applies these transformations to the entire dataset, and saves the resulting tokenized data to disk. This prepares the data for feeding into the fine-tuning process of the model.

`Step-3-1-TrainModel.py`
- A Python script used to train the AI model using tokenized conversation data. This script initializes the model, ensures it's loaded onto the correct device (utilizing GPU acceleration if available), and sets up the training arguments with options for Half-Precision (FP16) if compatible Tensor Cores are available. It includes functions to estimate VRAM, RAM, and SSD space requirements before commencing training. The training process utilizes the `transformers` Trainer API, with the model and tokenized datasets as inputs. It also handles the logging of training progress, estimation of resource requirements, and saving of checkpoints at specified intervals. Once the training is finished, the script saves the final model and tokenizer to the specified output directory and cleans up any allocated resources.

`Step-3-2-SaveModel.py`
- A Python script designed to save a fine-tuned language model and its corresponding tokenizer. It loads the model, tokenizer, and training data from specified directories, and then saves the model and tokenizer back to a specified directory. This allows for the reuse of the fine-tuned model in future tasks.

`Step-4-1-API-Deploy.py`
- Serve the fine-tuned language model as a web service, where it can generate responses based on user and system input messages.

`Step-4-2-API-Test.ps1`
- Script used to interact with and test the chatbot API by sending POST requests and displaying the chatbot's responses.