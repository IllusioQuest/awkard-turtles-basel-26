"""Offline checks for the T-63 local house-data prototype."""

import io
import json
import tempfile
import unittest
import zipfile
from pathlib import Path
from unittest.mock import patch

import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import house_siting as hs


class HouseSitingTests(unittest.TestCase):
    def sample_archive(self, root: Path) -> Path:
        nested = io.BytesIO()
        with zipfile.ZipFile(nested, "w") as z:
            for year in range(2021, 2026):
                rows = (
                    "Messpunkt;M;;\nSparte;Strom;;\nVertragskonto;X;;\n"
                    "Objekt Adresse;O;;\nKundennummer;K;;\nKundenname;N;;\n"
                    "Datum;Zeit;Energie;Einheit\n"
                    f"{year}0101;00-00-00;0.25;KWH\n"
                    f"{year}0101;00-15-00;0.50;KWH\n"
                )
                z.writestr(f"Data_Strom_{year}_anonymisiert.csv", rows)
            gas = (
                "Messpunkt CH;Zeitraum (KalJahr/Monat);Ableseeinheit;Verbrauch Medium Einfachtarif (kWh/m3)\n"
                "METER_A;202401;KWH;100\n"
                "METER_A;202402;KWH;200\n"
                "METER_A;202401;KW;0\n"
            )
            z.writestr("Data_Gas_2021-2026_anonymisiert.csv", gas)
        path = root / "sample.zip"
        with zipfile.ZipFile(path, "w") as z:
            z.writestr("Housedata/Grund_und_Aufriss_anonymisiert.pdf", b"%PDF-fixture")
            z.writestr("Housedata/Dachstock_anonymisiert.pdf", b"%PDF-fixture")
            z.writestr("Housedata/Energiedaten_anonymisiert_2021-2026.zip", nested.getvalue())
        return path

    def test_aggregates_are_not_misread_as_heat_or_spare_power(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = self.sample_archive(Path(tmp))
            pdfs, csvs = hs._read_zip(path)
            result = hs.energy_summary(csvs)
            self.assertEqual(result["electricity"]["2024"]["total_kwh"], 0.75)
            self.assertEqual(result["electricity"]["2024"]["observed_peak_15min_kw"], 2.0)
            self.assertEqual(result["gas"]["METER_A"]["2024"]["gas_input_kwh"], 300)
            evidence = {"rooms": [{"id": "cellar", "label": "Cellar", "pdf": next(iter(pdfs)),
                                  "page": 1, "reviewed": False, "near_heating": True,
                                  "occupied_use": False, "attic": False,
                                  "observations": ["proposed"]}]}
            report = hs.build_report(path, evidence)
            self.assertIn("Permitted installation now: 0 servers", report)
            self.assertIn("AI/fixture observations require manual plan review", report)
            self.assertNotIn("preferred_for_survey", report)
            config = Path(tmp) / "scenario.json"
            config.write_text(json.dumps({
                "ServerElectricKw": {"value": 2, "unit": "kW/server", "source": "test scenario"},
                "HeatRecoveryShare": {"value": 0.5, "unit": "fraction", "source": "test scenario"},
            }))
            custom = hs.build_report(path, evidence, config)
            self.assertIn("8,760 kWh/year", custom)
            self.assertIn("test scenario", custom)

    def test_rejects_traversal_in_nested_archive(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "malicious.zip"
            with zipfile.ZipFile(path, "w") as z:
                z.writestr("../escaped.txt", "not extracted")
            with self.assertRaisesRegex(ValueError, "Unsafe archive entry"):
                hs._read_zip(path)

    def test_mock_is_explicitly_not_ai(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = self.sample_archive(Path(tmp))
            evidence = hs.extract_mock(path)
            self.assertEqual(evidence["mode"], "mock_not_ai")
            self.assertTrue(all(room["reviewed"] is False for room in evidence["rooms"]))

    def test_ai_adapter_posts_image_only_to_loopback_and_marks_unreviewed(self):
        with tempfile.TemporaryDirectory() as tmp:
            archive = self.sample_archive(Path(tmp))
            class Response:
                def __enter__(self): return self
                def __exit__(self, *args): pass
                def read(self):
                    content = {"rooms": [{"label": "Cellar", "near_heating": True,
                                          "occupied_use": False, "attic": False,
                                          "observations": ["Generic room label"]}]}
                    return json.dumps({"message": {"content": json.dumps(content)}}).encode()
            requests = []
            def fake_run(command, **kwargs):
                class Result:
                    stdout = "Pages: 1\n"
                if command[0] == "pdftoppm":
                    Path(command[-1] + ".jpg").write_bytes(b"local-image")
                return Result()
            class Opener:
                def open(self, request, **kwargs):
                    requests.append(request)
                    return Response()
            checkpoint = Path(tmp) / "proposed.json"
            with patch.object(hs.subprocess, "run", side_effect=fake_run), patch.object(hs.urllib.request, "build_opener", return_value=Opener()) as build_opener:
                result = hs.extract_ai(archive, "vision-local", checkpoint)
                resumed = hs.extract_ai(archive, "vision-local", checkpoint)
            self.assertEqual(build_opener.call_count, 2)
            self.assertEqual(len(requests), 2)
            self.assertEqual(result, resumed)
            self.assertEqual(len(json.loads(checkpoint.read_text())["completed_pages"]), 2)
            self.assertTrue(all(req.full_url == hs.OLLAMA_URL for req in requests))
            self.assertTrue(all(room["reviewed"] is False for room in result["rooms"]))
            payload = json.loads(requests[0].data)
            self.assertEqual(payload["messages"][0]["images"][0], "bG9jYWwtaW1hZ2U=")


if __name__ == "__main__":
    unittest.main()
