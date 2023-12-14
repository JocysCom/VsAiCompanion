# Import necessary libraries for model loading and Flask API
import json
from transformers import AutoModelForCausalLM, AutoTokenizer
from flask import Flask, request, jsonify
import torch

# Load configuration from a JSON file
with open('Step-0-1-Config.json', 'r') as config_file:
    config = json.load(config_file)

# Load the trained tokenizer and model from the fine-tuned model directory
tokenizer = AutoTokenizer.from_pretrained(config['NEW_OUTPUT_DIR'])
model = AutoModelForCausalLM.from_pretrained(config['NEW_OUTPUT_DIR'])
model.eval()  # Set the model to evaluation mode

app = Flask(__name__)

@app.route('/predict', methods=['POST'])
def predict():
    json_data = request.json
    # Extracting both system and user messages from the JSON input
    system_input = json_data['messages'][0]['content'] if json_data['messages'][0]['role'] == "system" else ""
    user_input = json_data['messages'][1]['content'] if json_data['messages'][1]['role'] == "user" else ""
    input_text = system_input + " " + user_input

    # Tokenize input for the model
    inputs = tokenizer.encode(input_text, return_tensors='pt')

    # Generate a sequence of text from the model based on the tokenized input
    outputs = model.generate(
        inputs,
        max_length=config['OUTPUT_MAX_LENGTH'],
        pad_token_id=tokenizer.eos_token_id,
        num_return_sequences=1
    )

    # Decode the generated tokens to a string
    response_text = tokenizer.decode(outputs[0], skip_special_tokens=True)
    
    return jsonify({'text': response_text})

if __name__ == '__main__':
    app.run(debug=True)