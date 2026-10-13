FROM python:3.10-slim

# Install system dependencies required for OpenCV and PaddleOCR
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

# Pre-download PaddleOCR model weights during image build so container starts instantly
RUN python -c "from paddleocr import PaddleOCR; PaddleOCR(lang='en', use_angle_cls=False, use_gpu=False, show_log=False)"

# Copy source code and AI models
COPY . .

# Expose the combined upload and live-scanning API port
EXPOSE 5002

# Run the IoT AI service; supply IOT_DATABASE_URL at container runtime.
CMD ["python", "main_live.py", "--port", "5002", "--device", "cpu"]
