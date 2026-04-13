# OpenAPI Test Server
#
# A minimal FastAPI application that exposes a calculator API with
# auto-generated OpenAPI spec. Used for testing OpenAPI tool integration
# with Azure AI Foundry agents behind a private VNet.
#
# Endpoints:
#   GET  /openapi.json          - OpenAPI 3.x specification
#   POST /calculate             - Perform arithmetic operations
#   GET  /healthz               - Health check
#
# Run locally:  uvicorn main:app --host 0.0.0.0 --port 8080
# Docker:       docker build -t openapi-test-server . && docker run -p 8080:8080 openapi-test-server

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field

app = FastAPI(
    title="Calculator API",
    description="A simple calculator service for testing OpenAPI tool integration.",
    version="1.0.0",
    servers=[{"url": "/", "description": "Default"}],
)


class CalculateRequest(BaseModel):
    operation: str = Field(
        ...,
        description="The arithmetic operation to perform: add, subtract, multiply, or divide",
        enum=["add", "subtract", "multiply", "divide"],
    )
    a: float = Field(..., description="The first operand")
    b: float = Field(..., description="The second operand")


class CalculateResponse(BaseModel):
    operation: str = Field(..., description="The operation that was performed")
    a: float = Field(..., description="The first operand")
    b: float = Field(..., description="The second operand")
    result: float = Field(..., description="The result of the operation")


@app.post(
    "/calculate",
    response_model=CalculateResponse,
    summary="Perform a calculation",
    description="Perform an arithmetic operation (add, subtract, multiply, divide) on two numbers.",
)
def calculate(req: CalculateRequest) -> CalculateResponse:
    if req.operation == "add":
        result = req.a + req.b
    elif req.operation == "subtract":
        result = req.a - req.b
    elif req.operation == "multiply":
        result = req.a * req.b
    elif req.operation == "divide":
        if req.b == 0:
            raise HTTPException(status_code=400, detail="Division by zero")
        result = req.a / req.b
    else:
        raise HTTPException(status_code=400, detail=f"Unknown operation: {req.operation}")

    return CalculateResponse(operation=req.operation, a=req.a, b=req.b, result=result)


@app.get("/healthz", summary="Health check")
def healthz():
    return {"status": "ok"}
