"""FastAPI application for RAG Embedding Service."""

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from sentence_transformers import SentenceTransformer
from typing import List
import logging
import os

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

app = FastAPI(title="RAG Embedding Service", version="1.0.0")

# Global model instance
model: SentenceTransformer | None = None


@app.on_event("startup")
async def startup_event() -> None:
    """Load the sentence transformer model at startup."""
    global model
    model_name = os.getenv("MODEL_NAME", "sentence-transformers/all-MiniLM-L6-v2")
    logger.info(f"Loading model: {model_name}")
    try:
        model = SentenceTransformer(model_name)
        logger.info("Model loaded successfully")
    except Exception as e:
        logger.error(f"Failed to load model: {str(e)}")
        raise


# Request/Response models
class EmbedRequest(BaseModel):
    """Request model for embedding generation."""

    texts: List[str]


class EmbedResponse(BaseModel):
    """Response model for embedding generation."""

    embeddings: List[List[float]]


class HealthResponse(BaseModel):
    """Response model for health check."""

    status: str


@app.post("/embed", response_model=EmbedResponse)
async def embed_texts(request: EmbedRequest) -> EmbedResponse:
    """
    Generate embeddings for the provided texts.

    Args:
        request: EmbedRequest containing list of texts to embed

    Returns:
        EmbedResponse containing list of embedding vectors

    Raises:
        HTTPException: 503 if model not loaded, 400 if no texts provided, 500 for other errors
    """
    if not model:
        raise HTTPException(status_code=503, detail="Model not loaded")

    if not request.texts:
        raise HTTPException(status_code=400, detail="No texts provided")

    try:
        embeddings = model.encode(request.texts).tolist()
        return EmbedResponse(embeddings=embeddings)
    except Exception as e:
        logger.error(f"Embedding error: {str(e)}")
        raise HTTPException(status_code=500, detail=str(e))


@app.get("/health", response_model=HealthResponse)
async def health_check() -> HealthResponse:
    """
    Health check endpoint.

    Returns:
        HealthResponse with status "healthy" if model is loaded

    Raises:
        HTTPException: 503 if model not loaded
    """
    if model:
        return HealthResponse(status="healthy")
    else:
        raise HTTPException(status_code=503, detail="Model not loaded")
