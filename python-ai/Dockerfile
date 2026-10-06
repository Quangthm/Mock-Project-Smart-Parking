FROM python:3.10-slim

# Install system dependencies required for OpenCV and EasyOCR
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgl1 \
    libglib2.0-0 \
    ffmpeg \
    libsm6 \
    libxext6 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Copy requirements and install python dependencies
COPY requirements.txt ./
# Tăng timeout và ưu tiên bản PyTorch CPU siêu nhẹ tránh lỗi timeout mạng
RUN pip install --no-cache-dir --default-timeout=1000 -r requirements.txt --extra-index-url https://download.pytorch.org/whl/cpu

# Pre-download EasyOCR model weights during image build so container starts instantly
RUN python -c "import easyocr; easyocr.Reader(['en'], gpu=False)"

# Copy source code and AI models
COPY . .

# Expose Flask API port
EXPOSE 5001

# Run the Flask AI Parking API Server
CMD ["python", "main.py", "--port", "5001", "--device", "cpu"]


