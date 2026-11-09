"""Tests for locked schedule and independent paired-series analysis."""
from __future__ import annotations

import json
import math
from pathlib import Path
import runpy
import tempfile
import unittest


HERE = Path(__file__).resolve().parent
RUNNER = runpy.run_path(str(HERE / 'run-index-evolution-paired20.py'))
ANALYZER = runpy.run_path(str(HERE / 'analyze-index-evolution-paired20.py'))


class PairedProtocolTests(unittest.TestCase):
    def test_frozen_balanced_schedule(self) -> None:
        schedule = RUNNER['ensure_schedule']()
        self.assertEqual(len(schedule),40)
        for size in (50_000,1_000_000):
            group = [x for x in schedule if x['rows']==size]
            self.assertEqual(len(group),20)
            self.assertEqual(sorted(x['block'] for x in group),list(range(1,21)))
            self.assertEqual(sum(x['order']=='old-new' for x in group),10)
            self.assertEqual(sum(x['order']=='new-old' for x in group),10)

    def test_paired_geometric_mean_not_median_ratio(self) -> None:
        log_ratios=[math.log(2.),math.log(3.),math.log(4.)]
        stats=ANALYZER['summarize'](log_ratios,20261109)
        self.assertAlmostEqual(stats['ratioGeomean'],24.**(1./3.))
        self.assertAlmostEqual(stats['ratioMedian'],3.)
        self.assertEqual(stats['newFasterPairs'],3)
        self.assertEqual(stats['oldFasterPairs'],0)
        self.assertTrue(stats['bootstrap95'][0]<=stats['ratioGeomean']<=stats['bootstrap95'][1])

    def test_incomplete_series_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as t:
            (Path(t)/'manifest.json').write_text(json.dumps({
                'status':'running','acceptedRuns':2,'completedPairs':1}),encoding='utf-8')
            with self.assertRaisesRegex(ValueError,'Incomplete counted series'):
                ANALYZER['analyze'](Path(t))

    def test_quantile_interpolation(self) -> None:
        self.assertEqual(ANALYZER['quantile']([1.,3.,5.],.25),2.)
        self.assertEqual(ANALYZER['quantile']([1.,3.,5.],.5),3.)


if __name__ == '__main__':
    unittest.main()
