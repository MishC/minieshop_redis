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


def request_json(method, path, body=None):
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
            return response.status, json.loads(raw) if raw else None
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


def print_response(status, payload):
    print(f"\nHTTP {status}")
    if payload is not None:
        print(json.dumps(payload, indent=2, ensure_ascii=False))


def register():
    email = input("Email: ").strip()
    password = input("Password min 8 znakov: ").strip()
    status, payload = request_json("POST", "/auth/register", {"email": email, "password": password})
    print_response(status, payload)


def login():
    email = input("Email: ").strip()
    password = input("Password: ").strip()
    status, payload = request_json("POST", "/auth/login", {"email": email, "password": password})
    print_response(status, payload)


def me():
    status, payload = request_json("GET", "/auth/me")
    print_response(status, payload)


def logout():
    status, payload = request_json("POST", "/auth/logout")
    print_response(status, payload)


def main():
    while True:
        print("\n=== Auth CLI ===")
        print("1. Register")
        print("2. Login")
        print("3. Me")
        print("4. Logout")
        print("0. Koniec")

        choice = input("Vyber cislo: ").strip()

        if choice == "1":
            register()
        elif choice == "2":
            login()
        elif choice == "3":
            me()
        elif choice == "4":
            logout()
        elif choice == "0":
            break
        else:
            print("Neznamy vyber.")


if __name__ == "__main__":
    main()

