# ShopMicroservices

ShopMicroservices is a sample .NET 9 microservices e-commerce application.

- `Gateway` is the public API gateway built with YARP.
- `CatalogService` stores products in PostgreSQL.
- `CartService` stores shopping carts in Redis.
- `OrderService` stores orders in PostgreSQL.
- Redis is also used for users, login sessions, and recently viewed products.

## Running The Project

Run the whole stack from the project root:

```bash
docker compose up --build
```

The gateway will be available at:

```text
http://localhost:7000
```

## Authentication

Authentication uses a JWT access token stored in an `HttpOnly` cookie:

```text
access_token
```

After `register` or `login`, the backend sends the cookie with `Set-Cookie`. The browser stores it and automatically sends it with later API requests. JavaScript cannot read this cookie because it is `HttpOnly`.

Refresh cookies are not implemented yet.

Each login creates a browser-specific session id with:

```csharp
Guid.NewGuid()
```

The session id is stored inside the JWT as the `sid` claim and is also stored in Redis:

```text
auth:session:{sessionId}
```

Logout removes both the cookie and the Redis session.

## Role

The application uses two roles:

```text
Admin
Customer
```

Admin emails are configured in `Gateway/appsettings.json`:

```json
"Auth": {
  "AdminEmails": [
    "admin@test.com"
  ]
}
```

Admin-only product endpoints:

```text
GET  /products
POST /products
```

Regular users should access product details by id:

```text
GET /products/1
GET /products/2
```

## Recent Views

Recently viewed products are stored in Redis by session id:

```text
catalog:recently-viewed:{sessionId}
```

Available endpoints:

```text
GET /RecentViews
GET /products/RecentViews
GET /products/recently-viewed
```

These endpoints require authentication.

## Cart

The shopping cart is stored in Redis by user id and session id:

```text
cart:{userId}:{sessionId}
```

This means the same user can have different carts in different browsers or sessions.

## CLI

Use this script to test authentication and the full shopping flow:

```bash
python3 shop_cli.py
```

The interactive menu can:

- register or log in a user,
- show the current user with role and session id,
- check whether the auth routes exist on the running gateway,
- log out,
- open a product detail by product id,
- show recent views,
- add a product to the cart,
- show the cart,
- remove one item from the cart,
- create an order,
- show the current user's orders,
- test the admin-only product list,
- create a new product as admin.

The CLI stores the authentication cookie in:

```text
.shop_cli_cookies.txt
```

## Curl Examples

Register a regular user:

```bash
curl -i -c user.cookies \
  -X POST http://localhost:7000/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"user@test.com","password":"password123"}'
```

Get product detail:

```bash
curl -b user.cookies \
  http://localhost:7000/products/1
```

Get recent views:

```bash
curl -b user.cookies \
  http://localhost:7000/RecentViews
```

Get the current user's cart:

```bash
curl -b user.cookies \
  http://localhost:7000/cart/{userId}
```

Add a product to the cart:

```bash
curl -b user.cookies \
  -X POST http://localhost:7000/cart/{userId}/items \
  -H "Content-Type: application/json" \
  -d '{"productId":1,"quantity":2}'
```

Create an order:

```bash
curl -b user.cookies \
  -X POST http://localhost:7000/orders \
  -H "Content-Type: application/json" \
  -d '{
    "userId":"{userId}",
    "email":"user@test.com",
    "address":"Test Street 1",
    "city":"Bratislava",
    "postalCode":"81101",
    "country":"Slovakia"
  }'
```

Register an admin user:

```bash
curl -i -c admin.cookies \
  -X POST http://localhost:7000/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@test.com","password":"password1234"}'
```

Admin-only product list:

```bash
curl -b admin.cookies \
  http://localhost:7000/products
```

Admin-only add product:

```bash
curl -b admin.cookies \
  -X POST http://localhost:7000/products \
  -H "Content-Type: application/json" \
  -d '{"name":"Admin Product","price":42.5}'
```
