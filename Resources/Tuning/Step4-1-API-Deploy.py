# Import necessary libraries for model loading and Flask API
import json
from transformers import AutoModelForCausalLM, AutoTokenizer
from flask import Flask, request, jsonify
import torch

# Load configuration from a JSON file
with open('Step0-1-Config.json', 'r') as config_file:
    config = json.load(config_file)


# Define the path to the fine-tuned generative model
NEW_OUTPUT_DIR = config.get('NEW_OUTPUT_DIR')

# Load the trained tokenizer and model from the fine-tuned model directory
tokenizer = AutoTokenizer.from_pretrained(NEW_OUTPUT_DIR)
model = AutoModelForCausalLM.from_pretrained(NEW_OUTPUT_DIR)
model.eval()  # Set the model to evaluation mode

app = Flask(__name__)

@app.route('/predict', methods=['POST'])
def predict():
    json_data = request.json
    user_input = json_data['text']

    # Tokenize input for the model
    inputs = tokenizer(user_input, return_tensors='pt')

    # Generate a sequence of text from the model based on the input
    outputs = model.generate(
        inputs['input_ids'],
        max_length=50,
        pad_token_id=tokenizer.eos_token_id,
        num_return_sequences=1
    )

    # Decode the generated tokens to a string
    response_text = tokenizer.decode(outputs[0], skip_special_tokens=True)
    
    return jsonify({'text': response_text})

if __name__ == '__main__':
    app.run(debug=True)