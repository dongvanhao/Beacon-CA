## Design rules
### API Design
- RESTful endpoints
- Versioning: /api/v1/...
- Authentication: Bearer token
- Response: application/json

### Database Design
- Naming: PascalCase for tables, camelCase for columns
- Primary key: int Id
- Foreign key: [Entity]Id
- Index: create index cho các cột thường query

### Security
- Password: bcrypt
- JWT: 15-minute access token, 7-day refresh token
- CORS: allow localhost:3000, localhost:5173
- Rate limiting: 100 requests/minute/IP

### Performance
- Query: select only needed columns, use projection
- Caching: 5-minute TTL cho static data
- Async: tất cả I/O operations phải async
- Batch: update/delete nhiều records trong 1 query

### Error Handling
- HTTP status codes: 200 OK, 201 Created, 204 No Content, 400 Bad Request, 401 Unauthorized, 403 Forbidden, 404 Not Found, 409 Conflict, 500 Internal Server Error
- Error response: { "success": false, "message": "Error message", "errors": ["error1", "error2"] }

### Logging
- Log: Error, Warning, Info, Debug
- Format: timestamp, level, message, user_id (nếu có)
- Storage: Serilog → Console + File

### Testing
- Unit test: xUnit + Moq
- Integration test: Testcontainers
- Coverage: >80%

### Documentation
- API: Swagger/OpenAPI
- Code: XML comments
- Architecture: README.md

### Coding Standards
- SOLID principles
- DRY (Don't Repeat Yourself)
- KISS (Keep It Simple, Stupid)
- YAGNI (You Ain't Gonna Need This)
- Clean Code conventions