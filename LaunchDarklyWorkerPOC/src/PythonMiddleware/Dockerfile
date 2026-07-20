FROM python:3.12-slim AS final
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app

COPY src/PythonMiddleware/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY src/PythonMiddleware/main.py .

EXPOSE 8000
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000", "--log-level", "info"]
