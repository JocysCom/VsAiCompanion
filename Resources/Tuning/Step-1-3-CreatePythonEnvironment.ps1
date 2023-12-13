# Create dedicated python environment `tuning`. All python packages will be installed into `.\tuning\Lib` folder.
python -m pip install --upgrade pip
python -m venv tuning
tuning\Scripts\activate
pause

# Download and install required packages 1.7GB
# https://docs.nvidia.com/cuda/cuda-installation-guide-linux/#pip-wheels

# Install python with no nVidia CUDA support
pip  install torch torchvision torchaudio
# Install python with NVidia CUDA support
#pip   install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu121
# pip install numpy --pre torch torchvision torchaudio --force-reinstall --index-url https://download.pytorch.org/whl/nightly/cu117
# https://www.yodiw.com/install-transformers-pytorch-tensorflow-ubuntu-2023/
# CUDA Out of Memory. Try install NVidia 5.25 drivers.
pause

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
