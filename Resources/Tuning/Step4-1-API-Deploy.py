# Import necessary libraries for model loading and Flask API
from transformers import GPT2LMHeadModel, GPT2Tokenizer
from flask import Flask, request, jsonify
import torch

# Define the path to the fine-tuned generative model
MODEL_PATH = './Fine-Tuned/Model'

# Load the trained tokenizer and model from the fine-tuned model directory
tokenizer = GPT2Tokenizer.from_pretrained(MODEL_PATH)
model = GPT2LMHeadModel.from_pretrained(MODEL_PATH)
model.eval()  # Set the model to evaluation mode

app = Flask(__name__)

@app.route('/predict', methods=['POST'])
def predict():
    json_data = request.json
    user_input = json_data['text']

    # Tokenize input for the model
    inputs = tokenizer.encode(user_input, return_tensors='pt')

    # Generate a sequence of text from the model based on the input
    outputs = model.generate(inputs, max_length=50, pad_token_id=tokenizer.eos_token_id, num_return_sequences=1)

    # Decode the generated tokens to a string
    response_text = tokenizer.decode(outputs[0], skip_special_tokens=True)
    
    return jsonify({'text': response_text})

if __name__ == '__main__':
    app.run(debug=True)