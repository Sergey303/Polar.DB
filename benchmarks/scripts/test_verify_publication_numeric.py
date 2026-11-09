"""Cross-platform float portability must not weaken raw-provenance checks."""
from __future__ import annotations

from pathlib import Path
from importlib.util import module_from_spec,spec_from_file_location
import unittest

script=Path(__file__).with_name('verify-publication.py')
spec=spec_from_file_location('publication_verify',script)
module=module_from_spec(spec)
spec.loader.exec_module(module)

class NumericEquivalence(unittest.TestCase):
    def test_accepts_rounding_at_platform_precision(self):
        module.same_statistics({'ratio':1.234567890123,'ci':[1.20000000000001,1.30000000000001],'sha':'abc'},
                               {'ratio':1.2345678901232,'ci':[1.2,1.3],'sha':'abc'})
    def test_rejects_meaningfully_changed_measurements(self):
        with self.assertRaisesRegex(RuntimeError,'Numerical mismatch'):
            module.same_statistics({'r':1.24},{'r':1.25})
    def test_source_sha_is_bitwise_exact(self):
        with self.assertRaisesRegex(RuntimeError,'Value mismatch'):
            module.same_statistics({'sha':'abcd'},{'sha':'abce'})
    def test_rejects_changed_fields_and_sample_count(self):
        with self.assertRaisesRegex(RuntimeError,'Keys differ'):
            module.same_statistics({'pairs':20},{'pairs':20,'extra':1})
        with self.assertRaisesRegex(RuntimeError,'Value mismatch'):
            module.same_statistics({'pairs':19},{'pairs':20})

if __name__=='__main__':unittest.main()
