#!/usr/bin/env python3
"""
One CLI program for the ShopMicroservices demo.

The menu contains both authentication and shop actions:
- register/login/logout with the HttpOnly cookie flow
- route check for auth endpoints
- product detail and recent views
- cart actions
- order actions
- admin product list test

The script uses only the Python standard library.
"""

import json
import sys
from getpass import getpass
from http.cookiejar import MozillaCookieJar
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.request import HTTPCookieProcessor, Request, build_opener


BASE_URL = "http://localhost:7000"
COOKIE_FILE = Path(".shop_cli_cookies.txt")
MIN_PASSWORD_LENGTH = 8


def make_opener():
    """Create an opener that stores and sends cookies automatically."""
    cookie_jar = MozillaCookieJar(str(COOKIE_FILE))

    if COOKIE_FILE.exists():
        try:
            cookie_jar.load(ignore_discard=True, ignore_expires=True)
        except Exception as error:
            print(f"Warning: cookie file could not be loaded: {error}")

    return build_opener(HTTPCookieProcessor(cookie_jar)), cookie_jar


def parse_json_or_text(raw):
    """Return JSON when possible, otherwise return text."""
    if not raw:
        return None

    try:
        return json.loads(raw)
    except json.JSONDecodeError:
        return raw


def call_api(method, path, body=None, show_call=True):
    """Call the Gateway and return (status_code, payload)."""
    opener, cookie_jar = make_opener()
    data = None
    headers = {}

    if body is not None:
        data = json.dumps(body).encode("utf-8")
        headers["Content-Type"] = "application/json"

    url = f"{BASE_URL}{path}"
    request = Request(url, data=data, headers=headers, method=method)

    if show_call:
        print(f"Calling: {method} {url}")

    try:
        with opener.open(request, timeout=15) as response:
            cookie_jar.save(ignore_discard=True, ignore_expires=True)
            raw = response.read().decode("utf-8")
            return response.status, parse_json_or_text(raw)
    except HTTPError as error:
        raw = error.read().decode("utf-8")
        return error.code, parse_json_or_text(raw)
    except URLError as error:
        print(f"Cannot connect to {BASE_URL}: {error}", file=sys.stderr)
        return 0, None


def print_response(status, payload):
    """Always show HTTP status codes, then print the response body."""
    print(f"\nHTTP {status}")

    if payload is None:
        return

    if isinstance(payload, str):
        print(payload)
    else:
        print(json.dumps(payload, indent=2, ensure_ascii=False))


def print_status_hint(status):
    """Short explanation for common statuses while keeping the code visible."""
    if status == 200:
        print("OK.")
    elif status == 204:
        print("OK, no response body.")
    elif status == 400:
        print("Bad request. Check the input values.")
    elif status == 401:
        print("Unauthorized. Login is required or the session expired.")
    elif status == 403:
        print("Forbidden. The current user is not allowed to call this route.")
    elif status == 404:
        print("Not found. Check the route or rebuild the Gateway container.")
    elif status == 409:
        print("Conflict. The user probably already exists.")


def prompt_email():
    """Ask for a non-empty email."""
    while True:
        email = input("Email: ").strip()
        if email:
            return email
        print("Email cannot be empty.")


def prompt_password(require_min_length):
    """Ask for a password without stripping characters from it."""
    while True:
        password = getpass("Password: ")

        if not require_min_length or len(password) >= MIN_PASSWORD_LENGTH:
            return password

        print(
            f"Password must have at least {MIN_PASSWORD_LENGTH} characters. "
            f"You entered {len(password)}."
        )


def prompt_int(label, default=None):
    """Ask for an integer, optionally with a default value."""
    while True:
        raw = input(label).strip()
        if not raw and default is not None:
            return default
        try:
            return int(raw)
        except ValueError:
            print("Please enter a valid number.")


def auth_payload(is_register):
    """Build the request body for register/login."""
    return {
        "email": prompt_email(),
        "password": prompt_password(require_min_length=is_register),
    }


def get_current_user(show_response=False):
    """
    Get the current authenticated user.

    Internal cart/order actions call this quietly, so the menu does not print
    an extra HTTP 200 before the actual operation.
    """
    status, payload = call_api("GET", "/auth/me", show_call=show_response)

    if show_response:
        print_response(status, payload)
        print_status_hint(status)

    return payload if status == 200 and isinstance(payload, dict) else None


def require_current_user():
    """Return current user or print a login hint."""
    current_user = get_current_user(show_response=False)

    if current_user is None:
        print("\nHTTP 401")
        print("Unauthorized. Login first with menu option 2.")

    return current_user


def register():
    """Menu 1: create a user and store the auth cookie."""
    status, payload = call_api("POST", "/auth/register", auth_payload(is_register=True))
    print_response(status, payload)
    print_status_hint(status)

    if status == 200:
        print(f"Cookie saved to {COOKIE_FILE}.")


def login():
    """Menu 2: login and store the auth cookie."""
    status, payload = call_api("POST", "/auth/login", auth_payload(is_register=False))
    print_response(status, payload)
    print_status_hint(status)

    if status == 200:
        print(f"Cookie saved to {COOKIE_FILE}.")


def me():
    """Menu 3: show current user, role and session id."""
    get_current_user(show_response=True)


def auth_route_check():
    """Menu 4: check whether auth routes exist on the running Gateway."""
    status, payload = call_api("GET", "/auth/me")
    print_response(status, payload)

    if status == 401:
        print("Auth route exists. You are just not logged in.")
    else:
        print_status_hint(status)


def logout():
    """Menu 5: delete the auth cookie and server session."""
    status, payload = call_api("POST", "/auth/logout")
    print_response(status, payload)
    print_status_hint(status)


def show_product():
    """Menu 6: call GET /products/{id}; also records RecentViews when logged in."""
    product_id = prompt_int("Product id: ")
    status, payload = call_api("GET", f"/products/{product_id}")
    print_response(status, payload)
    print_status_hint(status)


def show_cart():
    """Menu 7: show the current session cart from Redis."""
    current_user = require_current_user()
    if current_user is None:
        return

    status, payload = call_api("GET", f"/cart/{current_user['userId']}")
    print_response(status, payload)
    print_status_hint(status)


def add_to_cart():
    """Menu 8: add a product and quantity to the cart."""
    current_user = require_current_user()
    if current_user is None:
        return

    product_id = prompt_int("Product id to add: ")
    quantity = prompt_int("Quantity [1]: ", default=1)
    body = {"productId": product_id, "quantity": quantity}

    status, payload = call_api("POST", f"/cart/{current_user['userId']}/items", body)
    print_response(status, payload)
    print_status_hint(status)


def remove_from_cart():
    """Menu 9: remove one quantity of a product from the cart."""
    current_user = require_current_user()
    if current_user is None:
        return

    product_id = prompt_int("Product id to remove: ")
    status, payload = call_api("DELETE", f"/cart/{current_user['userId']}/items/{product_id}")
    print_response(status, payload)
    print_status_hint(status)


def show_recent_views():
    """Menu 10: call GET /RecentViews for the current browser session."""
    status, payload = call_api("GET", "/RecentViews")
    print_response(status, payload)
    print_status_hint(status)


def create_order():
    """Menu 11: create an order from the current cart."""
    current_user = require_current_user()
    if current_user is None:
        return

    body = {
        "userId": current_user["userId"],
        "email": current_user["email"],
        "address": input("Address [Test Street 1]: ").strip() or "Test Street 1",
        "city": input("City [Bratislava]: ").strip() or "Bratislava",
        "postalCode": input("Postal code [81101]: ").strip() or "81101",
        "country": input("Country [Slovakia]: ").strip() or "Slovakia",
    }

    status, payload = call_api("POST", "/orders", body)
    print_response(status, payload)
    print_status_hint(status)


def show_orders():
    """Menu 12: show orders owned by the logged-in user."""
    status, payload = call_api("GET", "/orders")
    print_response(status, payload)
    print_status_hint(status)


def admin_products():
    """Menu 13: admin-only GET /products."""
    status, payload = call_api("GET", "/products")
    print_response(status, payload)
    print_status_hint(status)


def print_menu():
    print("\n=== ShopMicroservices CLI ===")
    print("0. Exit - close the CLI")
    print("1. Register - create user and save HttpOnly cookie")
    print("2. Login - login and save HttpOnly cookie")
    print("3. Me - show current user, role and session id")
    print("4. Auth route check - verify /auth/me exists on Gateway")
    print("5. Logout - remove cookie and Redis session")
    print("6. Product detail - GET /products/{id}")
    print("7. Show cart - GET /cart/{userId}")
    print("8. Add to cart - POST /cart/{userId}/items")
    print("9. Remove from cart - DELETE one product quantity")
    print("10. RecentViews - GET /RecentViews")
    print("11. Create order - POST /orders from current cart")
    print("12. My orders - GET /orders")
    print("13. Admin products - admin-only GET /products")


def main():
    while True:
        print_menu()
        choice = input("Vyber cislo: ").strip()

        if choice == "0":
            break
        elif choice == "1":
            register()
        elif choice == "2":
            login()
        elif choice == "3":
            me()
        elif choice == "4":
            auth_route_check()
        elif choice == "5":
            logout()
        elif choice == "6":
            show_product()
        elif choice == "7":
            show_cart()
        elif choice == "8":
            add_to_cart()
        elif choice == "9":
            remove_from_cart()
        elif choice == "10":
            show_recent_views()
        elif choice == "11":
            create_order()
        elif choice == "12":
            show_orders()
        elif choice == "13":
            admin_products()
        else:
            print("Unknown option.")


if __name__ == "__main__":
    main()
