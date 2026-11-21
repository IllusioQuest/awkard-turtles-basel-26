"""Reusable operator-configured TCP routes; never CONNECT/SOCKS or client-selected destinations."""
import asyncio
import json
import os
from pathlib import Path

POLICY = json.loads(Path(__file__).with_name("tcp-relay-policy.json").read_text())
BUFFER_BYTES = POLICY["buffer_bytes"]
CONNECT_TIMEOUT_SECONDS = POLICY["connect_timeout_seconds"]
IDLE_TIMEOUT_SECONDS = POLICY["idle_timeout_seconds"]
MAX_CONNECTIONS_PER_ROUTE = POLICY["max_connections_per_route"]


class Route:
    def __init__(self, host, port):
        self.host, self.port, self.active = host, port, 0

    async def handle(self, reader, writer):
        upstream_writer = None
        if self.active >= MAX_CONNECTIONS_PER_ROUTE:
            writer.close()
            await writer.wait_closed()
            return
        self.active += 1
        tasks = []
        try:
            upstream, upstream_writer = await asyncio.wait_for(
                asyncio.open_connection(self.host, self.port), CONNECT_TIMEOUT_SECONDS)

            async def pump(source, destination):
                while data := await asyncio.wait_for(source.read(BUFFER_BYTES), IDLE_TIMEOUT_SECONDS):
                    destination.write(data)
                    await asyncio.wait_for(destination.drain(), IDLE_TIMEOUT_SECONDS)
                if destination.can_write_eof():
                    destination.write_eof()

            tasks = [asyncio.create_task(pump(reader, upstream_writer)),
                     asyncio.create_task(pump(upstream, writer))]
            await asyncio.gather(*tasks)
        except (OSError, TimeoutError):
            pass  # No payloads, credentials or raw upstream errors in logs.
        finally:
            for task in tasks:
                task.cancel()
            await asyncio.gather(*tasks, return_exceptions=True)
            self.active -= 1
            for stream in (writer, upstream_writer):
                if stream is not None:
                    stream.close()
                    try:
                        await stream.wait_closed()
                    except OSError:
                        pass


async def main():
    servers = []
    for mapping in json.loads(os.environ["TCP_RELAY_ROUTES"]):
        if not isinstance(mapping["target_host"], str) or not mapping["target_host"]:
            raise ValueError("A fixed target host is required")
        for key in ("listen_port", "target_port"):
            if type(mapping[key]) is not int or not 1 <= mapping[key] <= 65535:
                raise ValueError("Invalid configured TCP port")
        route = Route(mapping["target_host"], mapping["target_port"])
        servers.append(await asyncio.start_server(route.handle, "0.0.0.0", mapping["listen_port"]))
    await asyncio.gather(*(server.serve_forever() for server in servers))


if __name__ == "__main__":
    asyncio.run(main())
