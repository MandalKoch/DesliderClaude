"""Check scripts served by a published Web app: python3 tests/smoke_published_assets.py PUBLISH_DIR."""

import argparse
from html.parser import HTMLParser
import os
from pathlib import Path
import socket
import subprocess
import tempfile
import time
from urllib.error import URLError
from urllib.parse import urljoin
from urllib.request import urlopen


class Scripts(HTMLParser):
    def __init__(self):
        super().__init__()
        self.sources = []

    def handle_starttag(self, tag, attrs):
        if tag == "script" and (source := dict(attrs).get("src")):
            self.sources.append(source)


def check(publish_dir):
    assembly = publish_dir / "DesliderClaude.Web.dll"
    if not assembly.is_file():
        raise SystemExit(f"Published app not found: {assembly}")
    with socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        port = listener.getsockname()[1]
    root = f"http://127.0.0.1:{port}/"
    with tempfile.TemporaryDirectory(prefix="deslider-assets-") as temp:
        environment = os.environ | {
            "ASPNETCORE_ENVIRONMENT": "Production",
            "DOTNET_ENVIRONMENT": "Production",
            "ASPNETCORE_URLS": root,
            "ConnectionStrings__DesliderClaudeDb": f"Data Source={temp}/test.db",
        }
        with tempfile.TemporaryFile(mode="w+") as log:
            app = subprocess.Popen(
                ["dotnet", str(assembly)], cwd=publish_dir, env=environment,
                stdout=log, stderr=subprocess.STDOUT,
            )
            try:
                deadline = time.monotonic() + 30
                while True:
                    try:
                        with urlopen(root, timeout=2) as response:
                            home = response.read().decode()
                        break
                    except URLError:
                        if app.poll() is not None or time.monotonic() >= deadline:
                            log.seek(0)
                            raise RuntimeError(f"Published app did not start:\n{log.read()}")
                        time.sleep(0.1)
                scripts = Scripts()
                scripts.feed(home)
                assert any("blazor.web" in source for source in scripts.sources), "Home must load Blazor."
                for source in scripts.sources:
                    with urlopen(urljoin(root, source), timeout=10) as response:
                        content_type = response.headers.get_content_type()
                        body = response.read()
                        assert response.status == 200, f"{source}: HTTP {response.status}"
                        assert content_type in ("text/javascript", "application/javascript"), f"{source}: {content_type}"
                        assert body and not body.lstrip().startswith(b"<"), f"{source}: expected JavaScript, received HTML or empty content"
                    print(f"PASS {source}")
            finally:
                app.terminate()
                try:
                    app.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    app.kill()
                    app.wait()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("publish_dir", type=Path)
    check(parser.parse_args().publish_dir.resolve())
