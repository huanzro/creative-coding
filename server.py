#!/usr/bin/env python3
"""Serve this local installation on loopback; no third-party Python packages."""
import argparse
import functools
import http.server
import pathlib
import threading
import urllib.request
import webbrowser

ROOT = pathlib.Path(__file__).resolve().parent / "dist"

class Handler(http.server.SimpleHTTPRequestHandler):
    extensions_map = {**http.server.SimpleHTTPRequestHandler.extensions_map,
                      ".mjs": "text/javascript", ".js": "text/javascript",
                      ".wasm": "application/wasm", ".task": "application/octet-stream"}

    def end_headers(self):
        self.send_header("Cache-Control", "no-cache")
        super().end_headers()

    def do_GET(self):
        if self.path == "/__echoes_health":
            self.send_response(200)
            self.end_headers()
            self.wfile.write(b"echoes-of-light-v0.1")
            return
        super().do_GET()

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--open", action="store_true")
    parser.add_argument("--port", type=int, default=8765)
    args = parser.parse_args()
    handler = functools.partial(Handler, directory=str(ROOT))
    for port in range(args.port, args.port + 20):
        url = f"http://localhost:{port}"
        try:
            server = http.server.ThreadingHTTPServer(("127.0.0.1", port), handler)
            break
        except OSError:
            try:
                with urllib.request.urlopen(url + "/__echoes_health", timeout=1) as reply:
                    if reply.read() == b"echoes-of-light-v0.1":
                        print(f"Already running: {url}", flush=True)
                        if args.open:
                            webbrowser.open(url)
                        return
            except Exception:
                pass
    else:
        raise SystemExit("No available local port. Try --port 8800.")
    print(f"Echoes of Light: {url}\nKeep this window open. Ctrl+C stops the local server.", flush=True)
    if args.open:
        threading.Timer(.4, lambda: webbrowser.open(url)).start()
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nStopped.")
    finally:
        server.server_close()

if __name__ == "__main__":
    main()
