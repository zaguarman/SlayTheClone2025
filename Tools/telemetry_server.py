"""The telemetry collector: receives the game records the game sends and keeps them, one JSON line a game.

The game (Telemetry.cs) posts batches to this server when TelemetrySettings.endpoint points at it:
    POST /v1/games   {"installId": "...", "version": "...", "records": [ {GameRecord}, ... ]}
        -> 200 {"accepted": n, "duplicates": m}   (a record already kept, by its id, isn't kept twice)
    GET  /v1/health  -> 200 {"ok": true, "games": total}
    GET  /v1/summary -> 200 a short summary (games by mode, by version, the last day's)

Records are appended to <out>/games-YYYY-MM-DD.jsonl (the day they arrive). With --key, a request must carry the same
value in its X-Api-Key header. Python's standard library only.

Usage:
    python Tools/telemetry_server.py                      # http://localhost:8787, kept in TelemetryData/
    python Tools/telemetry_server.py --port 9000 --out D:/telemetry --key secret --host 0.0.0.0
Then set the endpoint in Unity (Debug > Telemetry > Settings) to http://localhost:8787/v1/games.
Summaries of what's collected: python Tools/telemetry_report.py
"""
import argparse
import datetime
import json
import os
import threading
from collections import Counter
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

MAX_BODY = 64 * 1024 * 1024  # 64 MB a request at most
REQUIRED = ("id", "mode", "player1", "player2")


class Store:
    """The kept records: their ids (no record twice) and the files they're appended to."""

    def __init__(self, folder):
        self.folder = folder
        self.lock = threading.Lock()
        self.ids = set()
        self.modes = Counter()
        self.versions = Counter()
        os.makedirs(folder, exist_ok=True)
        for name in sorted(os.listdir(folder)):
            if name.endswith(".jsonl"):
                with open(os.path.join(folder, name), encoding="utf-8-sig") as f:
                    for line in f:
                        try:
                            record = json.loads(line)
                        except ValueError:
                            continue
                        self._count(record)

    def _count(self, record):
        self.ids.add(record.get("id"))
        self.modes[record.get("mode", "?")] += 1
        self.versions[record.get("version", "?")] += 1

    def add(self, records):
        accepted, duplicates = 0, 0
        path = os.path.join(self.folder, f"games-{datetime.datetime.now(datetime.timezone.utc):%Y-%m-%d}.jsonl")
        with self.lock, open(path, "a", encoding="utf-8") as f:
            for record in records:
                if not isinstance(record, dict) or any(k not in record for k in REQUIRED):
                    continue
                if record["id"] in self.ids:
                    duplicates += 1
                    continue
                record["receivedUtc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
                f.write(json.dumps(record, ensure_ascii=False) + "\n")
                self._count(record)
                accepted += 1
        return accepted, duplicates


def make_handler(store, key):
    class Handler(BaseHTTPRequestHandler):
        server_version = "SlayTheCloneTelemetry/1"

        def _reply(self, status, payload):
            body = json.dumps(payload).encode("utf-8")
            self.send_response(status)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def _allowed(self):
            if key and self.headers.get("X-Api-Key") != key:
                self._reply(401, {"error": "a valid X-Api-Key is needed"})
                return False
            return True

        def do_GET(self):
            if self.path == "/v1/health":
                self._reply(200, {"ok": True, "games": len(store.ids)})
            elif self.path == "/v1/summary":
                if not self._allowed():
                    return
                self._reply(200, {"games": len(store.ids), "byMode": dict(store.modes), "byVersion": dict(store.versions)})
            else:
                self._reply(404, {"error": "not found"})

        def do_POST(self):
            if self.path != "/v1/games":
                self._reply(404, {"error": "not found"})
                return
            if not self._allowed():
                return
            length = int(self.headers.get("Content-Length") or 0)
            if length <= 0 or length > MAX_BODY:
                self._reply(413 if length > MAX_BODY else 400, {"error": "an empty or too large body"})
                return
            try:
                payload = json.loads(self.rfile.read(length).decode("utf-8-sig"))
                records = payload["records"]
            except (ValueError, KeyError, TypeError):
                self._reply(400, {"error": "expected {\"records\": [...]}"})
                return
            accepted, duplicates = store.add(records if isinstance(records, list) else [])
            self._reply(200, {"accepted": accepted, "duplicates": duplicates})

        def log_message(self, fmt, *args):
            print(f"{self.address_string()} {fmt % args}")

    return Handler


def main():
    parser = argparse.ArgumentParser(description="SlayTheClone telemetry collector")
    parser.add_argument("--host", default="127.0.0.1", help="the address to listen on (0.0.0.0 for every one)")
    parser.add_argument("--port", type=int, default=8787)
    parser.add_argument("--out", default=os.path.join(os.path.dirname(__file__), "..", "TelemetryData"), help="where the records are kept")
    parser.add_argument("--key", default=os.environ.get("TELEMETRY_KEY", ""), help="an API key the game must send (X-Api-Key)")
    args = parser.parse_args()
    store = Store(os.path.abspath(args.out))
    server = ThreadingHTTPServer((args.host, args.port), make_handler(store, args.key))
    print(f"Telemetry collector on http://{args.host}:{args.port}/v1/games, keeping {len(store.ids)} games in {store.folder}"
          + (" (API key required)" if args.key else ""))
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
