from datasets import load_dataset

# Define the path to the training data
DATA_PATH = './Data/data.jsonl'

# Load and return the dataset from the jsonl file
def load_data(data_path):
    return load_dataset('json', data_files=data_path)['train']

if __name__ == '__main__':
    # Load the data and store it
    dataset = load_data(DATA_PATH)
    # Here you should save the dataset to a file or a variable to be used by the next step
    # This could be a pickle file or any other means of interim storage