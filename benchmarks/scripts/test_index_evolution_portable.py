"""No-.NET portable harness contract tests (run with unittest)."""
from __future__ import annotations

from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

from index_evolution_support import OLD, NEW, disk_guard, parse_log, file_sha256

RUNNER = Path(__file__).with_name("run-index-evolution-direct.py")


class PortableHarnessTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.dir = Path(self.temp.name)
        self.log = self.dir / "measure.log"

    def result(self, **changes: str) -> None:
        values = {"engine": OLD, "rows": "10000", "probes": "1000", "seed": "777",
                  "load_ms": "2.000", "prerequisite_ms": "1.000",
                  "index_build_ms": "0.000", "lookup_ms": "3.000",
                  "checksum": "123", "verified": "true"}
        values.update(changes)
        self.log.write_text("START\nRESULT " + " ".join(f"{k}={v}" for k, v in values.items())
                            + "\n", encoding="utf-8")

    def parse(self) -> dict[str, str]:
        return parse_log(self.log, "old", OLD, 10000, 1000, 777, 1)

    def test_valid_complete_result(self) -> None:
        self.result()
        self.assertEqual(self.parse()["checksum"], "123")
        self.assertEqual(self.parse()["engine"], "old")

    def test_seed_mismatch_stops(self) -> None:
        self.result(seed="778")
        with self.assertRaisesRegex(RuntimeError, "metadata"):
            self.parse()

    def test_wrong_commit_stops(self) -> None:
        self.result(engine=NEW)
        with self.assertRaisesRegex(RuntimeError, "metadata"):
            self.parse()

    def test_unverified_stops(self) -> None:
        self.result(verified="false")
        with self.assertRaisesRegex(RuntimeError, "metadata"):
            self.parse()

    def test_nonfinite_timing_stops(self) -> None:
        self.result(lookup_ms="NaN")
        with self.assertRaisesRegex(RuntimeError, "timing"):
            self.parse()

    def test_missing_or_duplicate_results_stop(self) -> None:
        self.log.write_text("START\n", encoding="utf-8")
        with self.assertRaisesRegex(RuntimeError, "exactly one"):
            self.parse()
        self.result()
        self.log.write_text(self.log.read_text() * 2, encoding="utf-8")
        with self.assertRaisesRegex(RuntimeError, "exactly one"):
            self.parse()

    def test_output_existing_is_never_deleted(self) -> None:
        sentinel = self.dir / "sentinel.txt"
        sentinel.write_text("keep")
        proc = subprocess.run([sys.executable, str(RUNNER), "--output", str(self.dir)],
                              capture_output=True, text=True, cwd=self.temp.name)
        self.assertNotEqual(proc.returncode, 0)
        self.assertIn("existing output", proc.stderr)
        self.assertEqual(sentinel.read_text(), "keep")

    def test_nonexistent_disk_guard_and_giant_requirement(self) -> None:
        disk_guard(self.dir / "uncreated" / "deep", 1)
        with self.assertRaisesRegex(RuntimeError, "Insufficient free space"):
            disk_guard(self.dir / "uncreated" / "deep", 2**60)

    def test_help_from_any_directory(self) -> None:
        proc = subprocess.run([sys.executable, str(RUNNER), "--help"],
                              cwd=self.temp.name, capture_output=True, text=True)
        self.assertEqual(proc.returncode, 0, proc.stderr)
        self.assertIn("--mode", proc.stdout)

    def test_full_mode_rejects_lower_disk_floor(self) -> None:
        proc = subprocess.run([sys.executable, str(RUNNER), "--mode", "full",
                               "--min-free-gib", "0.1"], cwd=self.temp.name,
                              capture_output=True, text=True)
        self.assertNotEqual(proc.returncode, 0)
        self.assertIn("at least 2 GiB", proc.stderr)


if __name__ == "__main__":
    unittest.main()
