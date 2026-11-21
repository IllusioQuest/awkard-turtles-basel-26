"""Tests for scripts/fetch_prices.py (ticket T-28). No network.

Run: python3 -m unittest discover -s scripts/tests -v

The fixture is the Azure Retail Prices snapshot of 2026-11-21 (Switzerland North, D32as v5 and
D32s v5). Expected values are the hand calculation in docs/research/computing-price.md §2-§3:
D32as v5 = 32 x 3.74 + 128 x 0.392 = 169.856 W -> 1000 / 169.856 = 5.8873 instances per kW;
spot USD 0.363686 x 0.8402 CHF/USD x 5.8873 = CHF 1.80 per server-hour (A2);
x RunPod 4090 ratio 0.34 / 0.89 = 0.382 -> CHF 0.687 per server-hour (C1, documented as 0.68).
"""

import json
import sys
import tempfile
import unittest
from datetime import datetime, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
import fetch_prices  # noqa: E402

FIXTURE = Path(__file__).resolve().parent / "fixtures" / "azure-switzerlandnorth-fixture.json"


class DeriveFromFixture(unittest.TestCase):
    def setUp(self):
        self.snapshot = json.loads(FIXTURE.read_text())
        self.result = fetch_prices.derive(self.snapshot, fetch_prices.load_method(),
                                          fetch_prices.load_server_kw(), fetch_prices.load_compute_rows())

    def test_instance_mapping(self):
        d32as = self.result["per_size"][0]
        self.assertEqual(d32as["armSkuName"], "Standard_D32as_v5")
        self.assertAlmostEqual(d32as["instance_power_w"], 169.856, places=3)
        self.assertAlmostEqual(d32as["instances_per_server"], 5.8873, places=4)

    def test_linux_prices_selected(self):
        d32as, d32s = self.result["per_size"]
        self.assertEqual(d32as["spot_usd_per_hour"], 0.363686)   # not Windows 0.635712, not Low Priority
        self.assertEqual(d32as["on_demand_usd_per_hour"], 1.968)  # not Cloud Services 3.44
        self.assertEqual(d32s["spot_usd_per_hour"], 0.375047)

    def test_documented_values(self):
        res = self.result["results"]
        self.assertAlmostEqual(res["a2_spot_min_chf_per_server_hour"], 1.80, places=2)  # §3 A2
        self.assertAlmostEqual(res["home_host_ratio"], 0.38, places=2)                  # §3 C2
        self.assertAlmostEqual(self.result["value_chf_per_server_hour"], 0.6873, places=4)
        self.assertAlmostEqual(self.result["difference_to_adopted_chf_per_server_hour"], 0.0273, places=4)

    def test_ambiguous_items_rejected(self):
        broken = dict(self.snapshot, items=self.snapshot["items"] * 2)
        with self.assertRaises(ValueError):
            fetch_prices.derive(broken, fetch_prices.load_method(),
                                fetch_prices.load_server_kw(), fetch_prices.load_compute_rows())


class FetchAndSnapshot(unittest.TestCase):
    def test_pagination_follows_next_page_link(self):
        pages = {"p2": {"Items": [{"id": 2}], "NextPageLink": None}}
        calls = []

        def fake_get(url):
            calls.append(url)
            return pages.get(url) or {"Items": [{"id": 1}], "NextPageLink": "p2"}

        _, items, n = fetch_prices.fetch_items(fetch_prices.load_method(), get_json=fake_get)
        self.assertEqual([i["id"] for i in items], [1, 2])
        self.assertEqual(n, 2)
        self.assertIn("switzerlandnorth", calls[0])

    def test_snapshot_never_overwritten(self):
        method = fetch_prices.load_method()
        now = datetime(2026, 11, 21, 1, 2, 3, tzinfo=timezone.utc)
        with tempfile.TemporaryDirectory() as tmp:
            first = fetch_prices.write_snapshot(method, "u", [], 1, now=now, directory=Path(tmp))
            second = fetch_prices.write_snapshot(method, "u", [], 1, now=now, directory=Path(tmp))
            self.assertNotEqual(first, second)
            self.assertEqual(first.name, "azure-switzerlandnorth-2026-11-21.json")
            self.assertEqual(second.name, "azure-switzerlandnorth-2026-11-21-010203Z.json")


if __name__ == "__main__":
    unittest.main()
