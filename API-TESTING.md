# API Testing Guide

## Development Authentication

In **Development mode**, the API uses a simple test authentication scheme that accepts a constant token.

### Test Token

```
dev-test-token-12345
```

### Test Tenant ID

```
00000000-0000-0000-0000-000000000001
```

## Testing with cURL

### Upload a Document

```bash
curl -X POST http://localhost:5000/api/v1/documents \
  -H "Authorization: Bearer dev-test-token-12345" \
  -F "file=@/path/to/your/document.pdf" \
  -F "title=My Test Document" \
  -F "source=api-test"
```

### Upload without Bearer prefix

```bash
curl -X POST http://localhost:5000/api/v1/documents \
  -H "Authorization: dev-test-token-12345" \
  -F "file=@/path/to/your/document.pdf" \
  -F "title=My Test Document"
```

## Testing with Postman/Insomnia

1. **Set Authorization Header**:
   - Type: `Bearer Token`
   - Token: `dev-test-token-12345`

2. **Upload Document Request**:
   - Method: `POST`
   - URL: `http://localhost:5000/api/v1/documents`
   - Body: `form-data`
     - `file`: (select file)
     - `title`: `My Test Document` (optional)
     - `source`: `postman-test` (optional)

## Testing with Swagger UI

1. Start the API: `dotnet run --project src/RAG.API`
2. Navigate to: `http://localhost:5000/swagger` (or the port shown in console)
3. Click the **🔓 Authorize** button (top right)
4. Enter the test token: `dev-test-token-12345`
5. Click **Authorize**, then **Close**
6. The lock icon should now be closed 🔒
7. Try the endpoints!

## Example Response

```json
{
  "documentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "title": "My Test Document",
  "status": "Uploaded",
  "uploadedAt": "2025-11-10T12:00:00Z"
}
```

## Important Notes

⚠️ **DEVELOPMENT ONLY**: This test authentication is only enabled in Development mode.

✅ **Automatic Tenant**: All requests with the test token are automatically assigned to tenant `00000000-0000-0000-0000-000000000001`

🔐 **Production**: In production, the API will use Keycloak JWT authentication.

## Allowed File Types

- PDF (`.pdf`)
- Text (`.txt`)
- Word Documents (`.doc`, `.docx`)
- CSV (`.csv`)
- Markdown (`.md`)

## File Size Limit

Maximum file size: **10 MB**

## Error Responses

### 400 Bad Request
```json
{
  "error": "File type not allowed. Allowed types: .pdf, .txt, .docx, .doc, .csv, .md"
}
```

### 401 Unauthorized
```json
{
  "error": "Missing or invalid tenant information"
}
```

### 413 Payload Too Large
```json
{
  "error": "File size exceeds maximum allowed size (10MB)"
}
```
