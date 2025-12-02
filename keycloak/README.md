# Keycloak Configuration for RAG Application

This directory contains the Keycloak realm configuration for the RAG application authentication.

## Quick Start

1. Start the services with Docker Compose:
   ```bash
   docker-compose up -d keycloak
   ```

2. Wait for Keycloak to be ready (about 60-90 seconds)

3. Access Keycloak Admin Console:
   - URL: http://localhost:8080/admin
   - Username: `admin`
   - Password: `admin`

## Realm Configuration

The `rag-realm.json` file configures:

### Clients

| Client ID | Type | Description |
|-----------|------|-------------|
| `rag-api` | Confidential | Backend API client with client credentials |
| `rag-web` | Public | Frontend SPA client |

### Test Users

| Username | Password | Roles | Tenant |
|----------|----------|-------|--------|
| `admin` | `admin123` | admin, user | Tenant 1 |
| `testuser` | `testuser123` | user | Tenant 1 |
| `viewer` | `viewer123` | viewer | Tenant 2 |

### Roles

**Realm Roles:**
- `admin` - Full administrative access
- `user` - Standard user access
- `viewer` - Read-only access

**Client Roles (rag-api):**
- `query` - Can execute queries
- `manage-documents` - Can upload and manage documents
- `admin` - Full API administrative access

## API Configuration

Configure the RAG API to use Keycloak:

```json
{
  "Authentication": {
    "Provider": "keycloak",
    "Keycloak": {
      "Authority": "http://localhost:8080/realms/rag",
      "ClientId": "rag-api",
      "ClientSecret": "rag-api-secret",
      "Audience": "rag-api",
      "RequireHttpsMetadata": false,
      "ValidateIssuer": true,
      "ValidateAudience": true,
      "ValidateLifetime": true,
      "ValidateIssuerSigningKey": true,
      "ClockSkewSeconds": 0
    }
  }
}
```

## Obtaining Tokens

### Using Password Grant (for testing)

```bash
curl -X POST http://localhost:8080/realms/rag/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password" \
  -d "client_id=rag-api" \
  -d "client_secret=rag-api-secret" \
  -d "username=testuser" \
  -d "password=testuser123"
```

### Using Client Credentials (service-to-service)

```bash
curl -X POST http://localhost:8080/realms/rag/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=rag-api" \
  -d "client_secret=rag-api-secret"
```

## Using Tokens with the API

```bash
# Get a token
TOKEN=$(curl -s -X POST http://localhost:8080/realms/rag/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password" \
  -d "client_id=rag-api" \
  -d "client_secret=rag-api-secret" \
  -d "username=testuser" \
  -d "password=testuser123" | jq -r '.access_token')

# Call the API
curl -X POST http://localhost:5000/api/query \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"query": "What is the capital of France?", "topK": 5}'
```

## Customization

### Adding Custom Claims

The realm is configured to include a `tenant_id` custom claim. To add more custom claims:

1. Go to Keycloak Admin Console
2. Navigate to `rag` realm → Clients → `rag-api` → Client scopes → Dedicated scope
3. Add a new mapper with type "User Attribute"

### Adding Users

1. Go to Keycloak Admin Console
2. Navigate to `rag` realm → Users
3. Click "Add user"
4. Set username, email, and other attributes
5. Go to Credentials tab and set password
6. Go to Role Mappings tab to assign roles

## Troubleshooting

### Keycloak not starting

Check the logs:
```bash
docker-compose logs keycloak
```

### Token validation failing

1. Verify the `Authority` URL is correct and accessible
2. Check that `RequireHttpsMetadata` is `false` for local development
3. Verify the token hasn't expired
4. Check that the audience claim matches your client ID

### CORS issues

Ensure the Web Origins are configured correctly in the Keycloak client settings to include your frontend URL.
