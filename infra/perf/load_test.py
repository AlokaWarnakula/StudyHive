"""Simple concurrent load test for the StudyHive API (k6-style, Python standard library only).

Usage:  python infra/perf/load_test.py [base_url]      (default http://127.0.0.1:8080; avoid "localhost" on Windows, it adds a 2 s IPv6 fallback)
Uses the Development seed accounts, so run it against the LOCAL docker stack only.
Each scenario runs N virtual users in parallel for a fixed time and reports
requests/s, p50/p90/p95/p99/max latency and the error rate.
"""
import json
import statistics
import sys
import threading
import time
import urllib.error
import urllib.request
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timedelta, timezone

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:8080"
PW = "Dev-Only-Passw0rd!"


def call(method, path, token=None, body=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    t0 = time.perf_counter()
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            r.read()
            code = r.status
    except urllib.error.HTTPError as e:
        code = e.code
    except Exception:
        code = 0
    return code, (time.perf_counter() - t0) * 1000


def login(email):
    req = urllib.request.Request(BASE + "/api/auth/login", data=json.dumps({"email": email, "password": PW}).encode(),
                                 headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req) as r:
        return json.loads(r.read())["accessToken"]


def pct(xs, p):
    xs = sorted(xs)
    k = max(0, min(len(xs) - 1, int(round(p / 100 * len(xs) + 0.5)) - 1))
    return xs[k]


def scenario(name, users, seconds, method, path, token=None):
    lat, codes, lock = [], {}, threading.Lock()
    stop = time.perf_counter() + seconds

    def vu():
        while time.perf_counter() < stop:
            c, ms = call(method, path, token)
            with lock:
                lat.append(ms)
                codes[c] = codes.get(c, 0) + 1

    t0 = time.perf_counter()
    with ThreadPoolExecutor(max_workers=users) as ex:
        for _ in range(users):
            ex.submit(vu)
    elapsed = time.perf_counter() - t0
    ok = sum(v for k, v in codes.items() if 200 <= k < 300)
    res = {
        "scenario": name, "users": users, "seconds": seconds, "requests": len(lat),
        "rps": round(len(lat) / elapsed, 1), "p50": round(pct(lat, 50), 1), "p90": round(pct(lat, 90), 1),
        "p95": round(pct(lat, 95), 1), "p99": round(pct(lat, 99), 1), "max": round(max(lat), 1),
        "avg": round(statistics.mean(lat), 1), "success_rate": round(100 * ok / len(lat), 2), "codes": codes,
    }
    print(json.dumps(res))
    return res


if __name__ == "__main__":
    student = login("student@studyhive.dev")
    librarian = login("librarian@studyhive.dev")
    day = (datetime.now(timezone.utc) + timedelta(days=3)).replace(hour=8, minute=0, second=0, microsecond=0)
    avail = f"/api/rooms/available?from={day.isoformat().replace('+00:00', 'Z')}&to={(day + timedelta(hours=2)).isoformat().replace('+00:00', 'Z')}&capacity=4"
    results = [
        scenario("GET /health", 50, 30, "GET", "/health"),
        scenario("GET /api/rooms/available (student)", 50, 30, "GET", avail, student),
        scenario("GET /api/booking-requests page 1 (librarian)", 50, 30, "GET", "/api/booking-requests?page=1&pageSize=20&sortBy=createdAt&sortDir=desc", librarian),
        scenario("GET /api/approvals (librarian)", 50, 30, "GET", "/api/approvals?page=1&pageSize=20", librarian),
        scenario("GET /api/rooms/available stress", 100, 30, "GET", avail, student),
    ]
    json.dump(results, open("perf_results.json", "w"), indent=2)
