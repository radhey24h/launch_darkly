# ============================================================
# PythonBackend Dockerfile
# ============================================================

FROM python:3.12-slim AS runtime
WORKDIR /app

# Install dependencies first (layer cache: only re-runs when requirements.txt changes)
COPY src/PythonBackend/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy application source
COPY src/PythonBackend/main.py .

EXPOSE 8000

# Run with uvicorn — production mode (no --reload)
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
