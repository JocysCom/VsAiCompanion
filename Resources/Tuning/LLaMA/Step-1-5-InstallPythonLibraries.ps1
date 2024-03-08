# Activate "tuning" Python environment.
tuning\Scripts\activate

# AutoTrain Advanced: faster and easier training and deployments of state-of-the-art machine learning models
pip install autotrain-advanced

# Client library to download and publish models, datasets and other repos on the huggingface.co hub
pip install huggingface_hub

autotrain setup --update-torch

return
# Transformers is a library maintained by Hugging Face and the community, for state-of-the-art Machine Learning for Pytorch, TensorFlow and JAX.
# https://pypi.org/project/transformers/
pip install transformers
#pip install tensorflow
pause

# HuggingFace community-driven open-source library of datasets
# https://pypi.org/project/datasets/
pip install datasets
pause

# https://pypi.org/project/accelerate/
#pip install accelerate
# `scikit-learn` Required by Split Data script.
pip install scikit-learn
pause

# Flask required for API deploy.
# https://pypi.org/project/flask/
pip install flask
pause

# Sentencepiece and protobuf required for LLaMA preprocessing data.
# https://pypi.org/project/sentencepiece/
pip install sentencepiece protobuf
pause

pip install psutil
pause
