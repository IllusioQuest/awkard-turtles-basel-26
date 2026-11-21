"""Regression: limiting captured logs must never limit extracted application files."""
import importlib.util
import os
from pathlib import Path
import sys
import tempfile
import unittest

module_path = os.environ.get("BOUNDED_PROCESS_MODULE_PATH", str(Path(__file__).parents[1] / "deploy/run-with-bounded-log.py"))
spec = importlib.util.spec_from_file_location("bounded_process", module_path)
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


@unittest.skipUnless(os.name == "posix", "Linux container process-group lifecycle")
class BoundedProcessTests(unittest.TestCase):
    def test_large_application_file_survives_small_log_cap(self):
        with tempfile.TemporaryDirectory(prefix="bounded-process-test-") as directory:
            game_file = Path(directory) / "extracted.bin"
            log = Path(directory) / "process.log"
            expected_bytes = 20 * 1024 * 1024
            command = [sys.executable, "-c", "from pathlib import Path; import sys; "
                       "Path(sys.argv[1]).write_bytes(b'x' * int(sys.argv[2])); "
                       "print('log' * 65536)", str(game_file), str(expected_bytes)]
            self.assertEqual(0, runner.run(command, log, 1024, 5))
            self.assertEqual(expected_bytes, game_file.stat().st_size)
            self.assertEqual(1024, log.stat().st_size)

    def test_timeout_returns_explicit_status(self):
        with tempfile.TemporaryDirectory(prefix="bounded-process-test-") as directory:
            self.assertEqual(124, runner.run([sys.executable, "-c", "import time; time.sleep(10)"],
                                            Path(directory) / "process.log", 1024, 0.1))


if __name__ == "__main__":
    unittest.main()
