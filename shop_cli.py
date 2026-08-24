#!/usr/bin/env python3
import json
import sys
from http.cookiejar import MozillaCookieJar
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.request import HTTPCookieProcessor, Request, build_opener


BASE_URL = "http://localhost:7000"
COOKIE_FILE = Path(".shop_cli_cookies.txt")


def make_opener():
    jar = MozillaCookieJar(str(COOKIE_FILE))
    if COOKIE_FILE.exists():
        try:
            jar.load(ignore_discard=True, ignore_expires=True)
        except Exception:
            pass
    return build_opener(HTTPCookieProcessor(jar)), jar


def call_api(method, path, body=None):
    opener, jar = make_opener()
    data = None
    headers = {}

    if body is not None:
        data = json.dumps(body).encode("utf-8")
        headers["Content-Type"] = "application/json"

    request = Request(f"{BASE_URL}{path}", data=data, headers=headers, method=method)

    try:
        with opener.open(request, timeout=15) as response:
            jar.save(ignore_discard=True, ignore_expires=True)
            raw = response.read().decode("utf-8")
            payload = json.loads(raw) if raw else None
            return response.status, payload
    except HTTPError as error:
        raw = error.read().decode("utf-8")
        try:
            payload = json.loads(raw) if raw else None
        except json.JSONDecodeError:
            payload = raw
        return error.code, payload
    except URLError as error:
        print(f"Neda sa pripojit na {BASE_URL}: {error}", file=sys.stderr)
        return 0, None


def print_json(status, payload):
    print(f"\nHTTP {status}")
    if payload is not None:
        print(json.dumps(payload, indent=2, ensure_ascii=False))


def auth_payload():
    email = input("Email: ").strip()
    password = input("Password: ").strip()
    return {"email": email, "password": password}


def register():
    status, payload = call_api("POST", "/auth/register", auth_payload())
    print_json(status, payload)


def login():
    status, payload = call_api("POST", "/auth/login", auth_payload())
    print_json(status, payload)


def me():
    status, payload = call_api("GET", "/auth/me")
    print_json(status, payload)
    return payload if status == 200 else None


def logout():
    status, payload = call_api("POST", "/auth/logout")
    print_json(status, payload)


def show_product():
    product_id = input("Napis cislo produktu: ").strip()
    status, payload = call_api("GET", f"/products/{product_id}")
    print_json(status, payload)


def show_recent_views():
    status, payload = call_api("GET", "/RecentViews")
    print_json(status, payload)


def add_to_cart():
    current = me()
    if not current:
        print("Najprv sa prihlas.")
        return

    product_id = int(input("Napis cislo produktu: ").strip())
    quantity = int(input("Mnozstvo: ").strip() or "1")
    body = {"productId": product_id, "quantity": quantity}
    status, payload = call_api("POST", f"/cart/{current['userId']}/items", body)
    print_json(status, payload)


def show_cart():
    current = me()
    if not current:
        print("Najprv sa prihlas.")
        return

    status, payload = call_api("GET", f"/cart/{current['userId']}")
    print_json(status, payload)


def remove_from_cart():
    current = me()
    if not current:
        print("Najprv sa prihlas.")
        return

    product_id = input("Cislo produktu na odobranie jedneho kusu: ").strip()
    status, payload = call_api("DELETE", f"/cart/{current['userId']}/items/{product_id}")
    print_json(status, payload)


def create_order():
    current = me()
    if not current:
        print("Najprv sa prihlas.")
        return

    body = {
        "userId": current["userId"],
        "email": current["email"],
        "address": input("Adresa [Test Street 1]: ").strip() or "Test Street 1",
        "city": input("Mesto [Bratislava]: ").strip() or "Bratislava",
        "postalCode": input("PSC [81101]: ").strip() or "81101",
        "country": input("Krajina [Slovakia]: ").strip() or "Slovakia",
    }

    status, payload = call_api("POST", "/orders", body)
    print_json(status, payload)


def show_orders():
    status, payload = call_api("GET", "/orders")
    print_json(status, payload)


def admin_products():
    status, payload = call_api("GET", "/products")
    print_json(status, payload)


def main():
    while True:
        print("\n=== Shop CLI ===")
        print("1. Register")
        print("2. Login")
        print("3. Me")
        print("4. GET product detail /products/{id}")
        print("5. GET RecentViews")
        print("6. Add product to cart")
        print("7. Show cart")
        print("8. Remove one product from cart")
        print("9. Create order")
        print("10. Show my orders")
        print("11. Admin GET /products")
        print("12. Logout")
        print("0. Koniec")

        choice = input("Vyber cislo: ").strip()

        if choice == "1":
            register()
        elif choice == "2":
            login()
        elif choice == "3":
            me()
        elif choice == "4":
            show_product()
        elif choice == "5":
            show_recent_views()
        elif choice == "6":
            add_to_cart()
        elif choice == "7":
            show_cart()
        elif choice == "8":
            remove_from_cart()
        elif choice == "9":
            create_order()
        elif choice == "10":
            show_orders()
        elif choice == "11":
            admin_products()
        elif choice == "12":
            logout()
        elif choice == "0":
            break
        else:
            print("Neznamy vyber.")


if __name__ == "__main__":
    main()

