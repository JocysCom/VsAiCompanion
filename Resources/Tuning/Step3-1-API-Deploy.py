# Import necessary libraries for model loading and Flask API
from transformers import AutoModelForSequenceClassification, AutoTokenizer
from flask import Flask, request, jsonify
import torch

# Define the path to the fine-tuned model
MODEL_PATH = './Fine-Tuned/Model'

# Load the trained tokenizer and model from the fine-tuned model directory
tokenizer = AutoTokenizer.from_pretrained(MODEL_PATH)
model = AutoModelForSequenceClassification.from_pretrained(MODEL_PATH)
model.eval()  # Set the model to evaluation mode

app = Flask(__name__)

# Function to get model predictions with descriptive labels
def get_prediction(text):
    inputs = tokenizer.encode_plus(text, return_tensors="pt", add_special_tokens=True, padding=True)
    with torch.no_grad():
        outputs = model(**inputs)
        logits = outputs.logits
    # Convert logits to class ID
    predicted_class_id = logits.argmax().item()
    # Map class ID to a label (replace with your actual classes)
    labels_map = {
        0: "Class A",  # Example class
        1: "Class B",  # Example class
        # ... Add more classes as needed
    }
    predicted_label = labels_map.get(predicted_class_id, "Unknown")
    return predicted_label

# Endpoint for providing predictions
@app.route('/predict', methods=['POST'])
def predict():
    input_data = request.json
    text = input_data.get('text', None)
    if not text:
        return jsonify({'error': 'Missing "text" field'}), 400

    prediction_label = get_prediction(text)
    return jsonify({'text': text, 'prediction': prediction_label})

if __name__ == '__main__':
    # Run the Flask application on http://localhost:5000
    app.run(debug=True, host='localhost', port=5000)