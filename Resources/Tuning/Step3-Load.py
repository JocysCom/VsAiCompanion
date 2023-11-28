from transformers import AutoModelForSequenceClassification, AutoTokenizer

# Replace checkpoint with the path to your saved model
model_checkpoint = "./results"
tokenizer = AutoTokenizer.from_pretrained(model_checkpoint)
model = AutoModelForSequenceClassification.from_pretrained(model_checkpoint)

# Function to encode input and get predictions
def get_prediction(text):
    inputs = tokenizer(text, padding=True, truncation=True, return_tensors="pt")
    outputs = model(**inputs)
    predictions = outputs.logits.argmax(-1)
    return predictions.item()

# For interactive chat, you'd include logic to handle user input, process with the model, and generate a response.