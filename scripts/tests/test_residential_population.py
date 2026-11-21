"""Independent source totals and quota/coverage checks for the BFS composition input."""
from decimal import Decimal
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("residential_population", ROOT / "scripts/derive_residential_population.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class ResidentialPopulationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.profile = json.loads((ROOT / module.DEFAULT_PROFILE).read_text())
        source_path = ROOT / cls.profile["source_manifest"]
        cls.source = json.loads(source_path.read_text(encoding="utf-8"))
        cls.records = module.extract_table(source_path.parent / cls.source["local_file"], cls.source)

    def test_source_counts_and_cantons_reconcile(self):
        rows = {r["building_category"]: r for r in self.records if r["region"] == "CH"}
        # Independent controls transcribed from original CH!B6:B10, not from the derived CSV.
        expected = {"total": 1423126, "single_family": 813592, "multi_family": 376285,
                    "residential_with_secondary_use": 166235, "partly_residential": 67014}
        self.assertEqual(expected, {key: value["building_count"] for key, value in rows.items()})
        self.assertEqual(expected["total"], sum(r["building_count"] for r in self.records
                         if r["region"] != "CH" and r["building_category"] == "total"))
        self.assertEqual(25, len({r["region"] for r in self.records}))
        self.assertFalse({"ZH", "VS"} & {r["region"] for r in self.records})

    def test_rounding_normalization_preserves_unknown_and_mixed(self):
        row = next(r for r in self.records if r["region"] == "CH" and r["building_category"] == "single_family")
        self.assertEqual(Decimal("100.1"), Decimal(row["published_owner_percent_sum"]))
        self.assertEqual(Decimal("75.5") / Decimal("100.1"), Decimal(row["natural_person_weight"]))
        self.assertGreater(Decimal(row["unknown_weight"]), 0)
        self.assertGreater(Decimal(row["mixed_weight"]), 0)

    def test_quota_allocation_has_exact_size_stable_ties_and_small_deviations(self):
        self.assertEqual([1, 1, 0], module.allocate(2, [Decimal(1)] * 3))
        for size in [1, 7, 100, 1000]:
            nodes, summary = module.population(self.records, self.profile | {"node_count": size}, self.source)
            self.assertEqual(size, len(nodes))
            self.assertEqual(size, sum(r["synthetic_count"] for r in summary))
            self.assertEqual(size, len({n["node_id"] for n in nodes}))
            self.assertEqual(nodes, module.population(self.records, self.profile | {"node_count": size}, self.source)[0])
            for category in self.source["category_rows"]:
                if category == "total":
                    continue
                row = next(r for r in self.records if r["region"] == "CH" and r["building_category"] == category)
                actual = sum(r["synthetic_count"] for r in summary if r["building_category"] == category)
                ideal = Decimal(size) * row["building_count"] / Decimal(1423126)
                self.assertLess(abs(Decimal(actual) - ideal), 1)

    def test_canton_profile_uses_local_not_national_owner_shares(self):
        basel = next(r for r in self.records if r["region"] == "BS" and r["building_category"] == "single_family")
        self.assertEqual("49.1", basel["natural_person_percent"])
        with self.assertRaises(ValueError):
            module.population(self.records, self.profile | {"region": "ZH"}, self.source)

    def test_synthetic_population_has_no_thermal_or_server_defaults(self):
        nodes, _ = module.population(self.records, self.profile, self.source)
        self.assertTrue(all(set(n) == {"node_id", "building_category", "owner_type"} for n in nodes))


if __name__ == "__main__":
    unittest.main()
