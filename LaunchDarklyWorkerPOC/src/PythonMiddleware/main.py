import asyncio
import logging
import time
from datetime import datetime, timezone

from fastapi import FastAPI, Request, status
from fastapi.responses import JSONResponse
from pydantic import BaseModel

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)s %(name)s %(message)s",
    datefmt="%Y-%m-%dT%H:%M:%SZ",
)

logger = logging.getLogger("PythonMiddleware")

app = FastAPI(
    title="Python Middleware (TIBCO Mock)",
    description="Represents the existing TIBCO middleware. Routes 90% of traffic here.",
    version="1.0.0",
)


class ProcessRequest(BaseModel):
    AccountId: str = ""
    CustomerId: str = ""
    TransactionId: str = ""
    Amount: float = 0.0
    Currency: str = "USD"
    Timestamp: str = ""
    Operation: str = ""
    ReferenceNumber: str = ""
    CorrelationId: str = ""
    Status: str = ""
    RoutedFrom: str = ""


class ProcessResponse(BaseModel):
    TransactionId: str
    Status: str
    ProcessedBy: str
    ProcessedAt: str
    Message: str


@app.middleware("http")
async def log_requests(request: Request, call_next):
    start = time.time()
    response = await call_next(request)
    elapsed_ms = (time.time() - start) * 1000
    logger.info(
        "HTTP %s %s → %d  elapsed=%.2fms",
        request.method, request.url.path, response.status_code, elapsed_ms,
    )
    return response


@app.post("/process", response_model=ProcessResponse, status_code=status.HTTP_200_OK)
async def process(request: ProcessRequest):
    logger.info(
        "PythonMiddleware (TIBCO) received | AccountId=%s TransactionId=%s "
        "CorrelationId=%s Operation=%s Amount=%.2f %s RoutedFrom=%s",
        request.AccountId,
        request.TransactionId,
        request.CorrelationId,
        request.Operation,
        request.Amount,
        request.Currency,
        request.RoutedFrom,
    )

    # Simulate lightweight processing
    await asyncio.sleep(0.005)

    processed_at = datetime.now(timezone.utc).isoformat()

    response = ProcessResponse(
        TransactionId=request.TransactionId,
        Status="SUCCESS",
        ProcessedBy="PythonMiddleware",
        ProcessedAt=processed_at,
        Message=f"Transaction {request.TransactionId} processed successfully by Python Middleware (TIBCO)",
    )

    logger.info(
        "PythonMiddleware (TIBCO) completed | TransactionId=%s Status=%s ProcessedAt=%s",
        response.TransactionId,
        response.Status,
        response.ProcessedAt,
    )

    return response


@app.get("/health")
async def health():
    return {
        "status": "healthy",
        "service": "PythonMiddleware",
        "timestamp": datetime.now(timezone.utc).isoformat(),
    }
