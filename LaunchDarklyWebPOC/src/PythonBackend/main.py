"""
Python FastAPI Backend — Mock TIBCO Legacy Service
===================================================
Port: 8000

This service simulates the existing legacy TIBCO implementation.
In the canary deployment:
  - Initially receives 90% of production traffic.
  - Traffic percentage decreases as confidence in the .NET backend grows.
  - Controlled entirely by the LaunchDarkly feature flag in the Gateway.
  - No changes to THIS service are needed to adjust routing percentages.

The endpoint contract is identical to the .NET backend so the Gateway
can forward responses transparently without transformation.

Architecture note:
  The Gateway evaluates the LaunchDarkly flag and decides whether to call
  this service or the .NET backend. This service has no knowledge of
  LaunchDarkly — it simply receives requests and returns responses.
"""

from datetime import datetime, timezone
from typing import Any

from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse
import logging
import uvicorn

# ============================================================
# Configure structured logging
# ============================================================
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s | %(levelname)s | %(name)s | %(message)s",
    datefmt="%Y-%m-%dT%H:%M:%S",
)
logger = logging.getLogger("python-backend")

# ============================================================
# FastAPI application instance
# ============================================================
app = FastAPI(
    title="LaunchDarkly Canary POC — Python Backend (Mock TIBCO)",
    description="""
    Python FastAPI service that mocks the legacy TIBCO system.

    In the canary deployment pattern:
    - This service represents the known-stable legacy backend.
    - It receives 90% of traffic initially.
    - As the .NET backend proves stability, traffic shifts away.
    - The Gateway controls routing via LaunchDarkly — no changes needed here.

    The "backend" field in responses is always "python" to confirm routing.
    """,
    version="1.0.0",
    docs_url="/swagger",    # Swagger UI at /swagger (matches .NET convention)
    redoc_url="/redoc",
)


# ============================================================
# ENDPOINT: GET /api/orders/{user_id}
# ============================================================
@app.get(
    "/api/orders/{user_id}",
    summary="Get order by user ID",
    description="Returns a sample order from the Python backend (mock TIBCO).",
    response_description="Order data with backend identifier",
    tags=["Orders"],
)
async def get_order(user_id: str, request: Request) -> dict[str, Any]:
    """
    Returns a sample order response identifying this as the Python backend.

    The "backend" field in the response lets testers confirm that the
    Gateway's LaunchDarkly routing correctly sent this request to Python.

    Args:
        user_id: The user identifier passed from the Gateway.
                 The Gateway uses this as the LaunchDarkly context key
                 for deterministic flag evaluation.
        request: FastAPI Request object (for logging correlation ID if present).
    """
    # Extract correlation ID if forwarded by the Gateway.
    # This enables end-to-end request tracing across Gateway → Backend → Logs.
    correlation_id = request.headers.get("X-Correlation-Id", "unknown")

    # Log the incoming request — visible in terminal when running locally.
    logger.info(
        "Python backend called | user_id=%s | correlation_id=%s",
        user_id,
        correlation_id,
    )

    # Generate a stable orderId from the userId hash.
    # Same user always gets the same orderId (deterministic, like the .NET backend).
    order_id = abs(hash(user_id)) % 10000

    response_data = {
        # "python" is the variation key from LaunchDarkly.
        # The Gateway compares this to confirm the correct backend was called.
        "backend": "python",
        "orderId": order_id,
        "customer": user_id.upper(),
        "message": "Response from Python Backend (Mock TIBCO)",
        # ISO-8601 UTC timestamp
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "userId": user_id,
    }

    logger.info(
        "Python backend returning | order_id=%d | user_id=%s",
        order_id,
        user_id,
    )

    return response_data


# ============================================================
# HEALTH ENDPOINT: GET /health
# ============================================================
@app.get(
    "/health",
    summary="Health check",
    tags=["System"],
)
async def health_check() -> dict[str, Any]:
    """
    Returns service health status.
    Used by Docker health checks and Kubernetes liveness probes.
    """
    return {
        "status": "healthy",
        "service": "Python Backend (Mock TIBCO)",
        "timestamp": datetime.now(timezone.utc).isoformat(),
    }


# ============================================================
# VERSION ENDPOINT: GET /version
# ============================================================
@app.get(
    "/version",
    summary="Service version",
    tags=["System"],
)
async def version() -> dict[str, Any]:
    """Returns service version information."""
    import sys
    return {
        "service": "PythonBackend",
        "version": "1.0.0",
        "python": sys.version,
        "framework": "FastAPI",
    }


# ============================================================
# STARTUP EVENT
# ============================================================
@app.on_event("startup")
async def on_startup() -> None:
    """Log startup information when the service starts."""
    logger.info("Python Backend (Mock TIBCO) starting on port 8000")
    logger.info("Swagger UI available at: http://localhost:8000/swagger")
    logger.info("This service represents the legacy TIBCO backend.")
    logger.info("It receives 90%% of traffic in the initial canary rollout.")


# ============================================================
# MAIN ENTRY POINT
# Run with: python main.py
# Or:       uvicorn main:app --host 0.0.0.0 --port 8000 --reload
# ============================================================
if __name__ == "__main__":
    uvicorn.run(
        "main:app",
        host="0.0.0.0",
        port=8000,
        reload=True,          # Auto-reload on file changes (development only)
        log_level="info",
    )
