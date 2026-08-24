# ShopMicroservices

Toto je ukazkovy .NET 9 mikroservisny e-shop:

- `Gateway` je API gateway cez YARP.
- `CatalogService` drzi produkty v PostgreSQL.
- `CartService` drzi kosik v Redis.
- `OrderService` drzi objednavky v PostgreSQL.
- Redis drzi aj prihlasenych pouzivatelov, session a naposledy pozerane produkty.

## Spustenie

Z korena projektu:

```bash
docker compose up --build
```

Gateway potom bezi na:

```text
http://localhost:7000
```

## Autentifikacia

Autentifikacia je cez JWT ulozeny v `HttpOnly` cookie:

```text
access_token
```

Po `register` alebo `login` backend posle cookie cez `Set-Cookie`. Browser ju potom automaticky posiela pri dalsich requestoch. JavaScript ju nevie precitat, lebo je `HttpOnly`.

Refresh cookie zatial nie je implementovana.

Pri kazdom prihlaseni sa vytvori browser/session id cez:

```csharp
Guid.NewGuid()
```

Session id sa ulozi do JWT ako `sid` a zaroven do Redis:

```text
auth:session:{sessionId}
```

Logout zmaze cookie aj Redis session.

## Role

Pouzivatelia maju roly:

```text
Admin
Customer
```

Admin emaily su v `Gateway/appsettings.json`:

```json
"Auth": {
  "AdminEmails": [
    "admin@test.com"
  ]
}
```

Admin-only endpointy:

```text
GET  /products
POST /products
```

Bezny prihlaseny alebo neprihlaseny pouzivatel ma pouzivat detail produktu:

```text
GET /products/1
GET /products/2
```

## RecentViews

Naposledy pozerane produkty su ulozene v Redis pod session id:

```text
catalog:recently-viewed:{sessionId}
```

Endpointy:

```text
GET /RecentViews
GET /products/RecentViews
GET /products/recently-viewed
```

Tieto endpointy vyzaduju prihlasenie.

## Cart

Kosik je ulozeny v Redis pod user id aj session id:

```text
cart:{userId}:{sessionId}
```

To znamena, ze rovnaky user moze mat v dvoch browseroch rozdielny kosik.

## CLI autorizacia

Len registracia/login/logout/me:

```bash
python3 auth_cli.py
```

Script si cookie ulozi do suboru:

```text
.shop_cli_cookies.txt
```

## CLI e-shop test

Interaktivny test produktu, kosika a objednavky:

```bash
python3 shop_cli.py
```

V menu vies:

- registrovat alebo prihlasit pouzivatela,
- otvorit detail produktu podla cisla,
- pozriet RecentViews,
- pridat produkt do kosika,
- zobrazit kosik,
- odstranit jeden kus z kosika,
- vytvorit objednavku,
- zobrazit objednavky,
- odhlasit sa.

## Curl priklady

Registracia bezneho pouzivatela:

```bash
curl -i -c user.cookies \
  -X POST http://localhost:7000/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"user@test.com","password":"password123"}'
```

GET detail produktu:

```bash
curl -b user.cookies \
  http://localhost:7000/products/1
```

GET RecentViews:

```bash
curl -b user.cookies \
  http://localhost:7000/RecentViews
```

GET kosik:

```bash
curl -b user.cookies \
  http://localhost:7000/cart/{userId}
```

Pridanie produktu do kosika:

```bash
curl -b user.cookies \
  -X POST http://localhost:7000/cart/{userId}/items \
  -H "Content-Type: application/json" \
  -d '{"productId":1,"quantity":2}'
```

Vytvorenie objednavky:

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

Admin registracia:

```bash
curl -i -c admin.cookies \
  -X POST http://localhost:7000/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@test.com","password":"password123"}'
```

Admin GET vsetky produkty:

```bash
curl -b admin.cookies \
  http://localhost:7000/products
```

