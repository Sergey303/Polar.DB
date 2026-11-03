"""Fast disk-guard regression tests; no real benchmark data is generated."""
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parent))
from disk_guard import DiskBudgetExceeded, GIB, run_guarded


class DiskGuardTests(unittest.TestCase):
    def setUp(self):
        self.folder = tempfile.TemporaryDirectory()
        self.root = Path(self.folder.name)
        (self.root / "benchmarks/work").mkdir(parents=True)
        (self.root / "benchmarks/results").mkdir(parents=True)

    def tearDown(self):
        self.folder.cleanup()

    def run_command(self, script, **kwargs):
        return run_guarded([sys.executable, "-c", script], root=self.root,
                           min_free_gib=8, max_growth_gib=1,
                           poll_seconds=0.02, **kwargs)

    def test_preflight_blocks_before_process_creation(self):
        with mock.patch("disk_guard.subprocess.Popen") as popen:
            with self.assertRaisesRegex(DiskBudgetExceeded, "Preflight"):
                self.run_command("pass", read_free=lambda: 9 * GIB)
            popen.assert_not_called()

    def test_success(self):
        self.run_command("print('guard smoke success')",
                         read_free=lambda: 15 * GIB, timeout_seconds=5)

    def test_disk_growth_terminates_process_group(self):
        values = iter([15 * GIB, 15 * GIB, 13 * GIB])
        with self.assertRaisesRegex(DiskBudgetExceeded, "Disk limit"):
            self.run_command("import time;time.sleep(10)",
                             read_free=lambda: next(values, 13 * GIB),
                             timeout_seconds=5)

    def test_reserve_terminates_process_group(self):
        values = iter([15 * GIB, 15 * GIB, 7 * GIB])
        with self.assertRaisesRegex(DiskBudgetExceeded, "Disk limit"):
            self.run_command("import time;time.sleep(10)",
                             read_free=lambda: next(values, 7 * GIB),
                             timeout_seconds=5)

    def test_timeout_kills_process(self):
        with self.assertRaises(subprocess.TimeoutExpired):
            self.run_command("import time;time.sleep(10)",
                             read_free=lambda: 15 * GIB,
                             timeout_seconds=0.06)

    def test_nonzero_exit_fails(self):
        with self.assertRaises(subprocess.CalledProcessError):
            self.run_command("import sys;sys.exit(7)",
                             read_free=lambda: 15 * GIB,
                             timeout_seconds=5)


if __name__ == "__main__":
    unittest.main()
