import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

module_path = Path(__file__).with_name("analyze-independent-series.py")
spec = importlib.util.spec_from_file_location("independent_analysis", module_path)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class SeriesAnalysisTest(unittest.TestCase):
    def test_balanced_independent_series_and_ratio(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            folder = root / "benchmarks" / "results" / "independent"
            folder.mkdir(parents=True)
            runs = []
            schedule = ("sqlite-first", "polar-first", "polar-first", "sqlite-first")
            for i in range(12):
                order = schedule[i % 4]
                run_id = f"run{i:02d}"
                raw = root / "benchmarks" / "results" / "raw" / "x" / run_id / "combined.raw.json"
                raw.parent.mkdir(parents=True)
                raw.write_text(json.dumps({
                    "manifest": {
                        "runId": run_id,
                        "engineOrder": ["sqlite", "polar-db"] if order == "sqlite-first" else ["polar-db", "sqlite"],
                        "coordinator": {"commitSha": "abcd", "cpuDescription": "cpu",
                                        "osDescription": "linux", "runtimeVersion": "10",
                                        "serverGc": False, "buildConfiguration": "Release",
                                        "sqliteNativeVersion": "3.5", "publicationReady": True,
                                        "gitDirty": False, "gitStatusKnown": True},
                        "engineProcesses": [
                            {"commitSha": "abcd", "publicationReady": True},
                            {"commitSha": "abcd", "publicationReady": True}]},
                    "options": {"rowCounts": [50000], "kind": "PkIntLookup"},
                    "lookupRuns": [{"setupRows": 50000, "phases": [{
                        "name": "hot", "engines": [
                            {"engine": "sqlite", "batchAvgSamplesMs": [9, 10, 11], "latencySamplesMs": [9, 11]},
                            {"engine": "polar-db-current", "batchAvgSamplesMs": [4, 5, 6], "latencySamplesMs": [4, 6]}
                        ]}]}]
                }), encoding="utf-8")
                runs.append({"runId": run_id, "order": order, "raw": raw.relative_to(root).as_posix()})
            series_file = folder / "test.json"
            series_file.write_text(json.dumps({"commit": "abcd", "runs": runs}), encoding="utf-8")
            results = module.analyze(series_file, resamples=1000)
            self.assertEqual(len(results), 2)
            self.assertAlmostEqual(results[0]["medianPairedRatioSqliteOverPolar"], 2.0)
            self.assertEqual(results[0]["independentRuns"], 12)
            self.assertAlmostEqual(results[0]["bootstrap95Low"], 2.0)
            runs[1]["order"] = "sqlite-first"
            series_file.write_text(json.dumps({"commit": "abcd", "runs": runs}), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "counterbalanced"):
                module.analyze(series_file, resamples=1000)


if __name__ == "__main__":
    unittest.main()
