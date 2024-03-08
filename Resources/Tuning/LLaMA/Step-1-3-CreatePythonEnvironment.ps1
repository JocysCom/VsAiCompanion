# Create dedicated python environment `tuning`. All python packages will be installed into `.\tuning\Lib` folder.
python -m pip install --upgrade pip
python -m venv tuning
tuning\Scripts\activate

# Download and install required packages 1.7GB
# https://docs.nvidia.com/cuda/cuda-installation-guide-linux/#pip-wheels

# Install python with no nVidia CUDA support
#pip  install torch torchvision torchaudio
# Install python with NVidia CUDA support
pip   install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu121
# pip install numpy --pre torch torchvision torchaudio --force-reinstall --index-url https://download.pytorch.org/whl/nightly/cu117
# https://www.yodiw.com/install-transformers-pytorch-tensorflow-ubuntu-2023/
# CUDA Out of Memory. Try install NVidia 5.25 drivers.
pause