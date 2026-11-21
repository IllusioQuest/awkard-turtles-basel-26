"""Standard-library checks for byte fidelity, half-close and bounded fixed-route forwarding."""
import asyncio
import importlib.util
from pathlib import Path
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("tcp_relay", Path(__file__).parents[1] / "deploy/tcp-relay.py")
relay = importlib.util.module_from_spec(spec)
spec.loader.exec_module(relay)


class RelayTests(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        self.servers = []
        self.writers = []

    async def asyncTearDown(self):
        for writer in self.writers:
            writer.close()
            await writer.wait_closed()
        for server in self.servers:
            server.close()
            await server.wait_closed()

    async def server(self, handler):
        server = await asyncio.start_server(handler, "127.0.0.1", 0)
        self.servers.append(server)
        return server.sockets[0].getsockname()[1]

    async def connect(self, port):
        reader, writer = await asyncio.open_connection("127.0.0.1", port)
        self.writers.append(writer)
        return reader, writer

    async def test_binary_response_survives_client_half_close(self):
        body = bytes(range(256)) * 1024

        async def target(reader, writer):
            received = await reader.read()
            writer.write(received[::-1])
            await writer.drain()
            writer.close()
            await writer.wait_closed()

        upstream = await self.server(target)
        port = await self.server(relay.Route("127.0.0.1", upstream).handle)
        reader, writer = await self.connect(port)
        writer.write(body)
        await writer.drain()
        writer.write_eof()
        self.assertEqual(body[::-1], await asyncio.wait_for(reader.read(), 3))

    async def test_connection_limit_rejects_extra_client(self):
        accepted = asyncio.Event()

        async def target(reader, writer):
            accepted.set()
            await reader.read()
            writer.close()
            await writer.wait_closed()

        upstream = await self.server(target)
        route = relay.Route("127.0.0.1", upstream)
        port = await self.server(route.handle)
        with patch.object(relay, "MAX_CONNECTIONS_PER_ROUTE", 1):
            await self.connect(port)
            await asyncio.wait_for(accepted.wait(), 3)
            extra, _ = await self.connect(port)
            self.assertEqual(b"", await asyncio.wait_for(extra.read(), 3))
            self.assertEqual(1, route.active)

    async def test_unreachable_fixed_backend_closes_client(self):
        upstream = await self.server(lambda *_: None)
        self.servers[-1].close()
        await self.servers[-1].wait_closed()
        route = relay.Route("127.0.0.1", upstream)
        port = await self.server(route.handle)
        reader, _ = await self.connect(port)
        self.assertEqual(b"", await asyncio.wait_for(reader.read(), 3))


if __name__ == "__main__":
    unittest.main()
