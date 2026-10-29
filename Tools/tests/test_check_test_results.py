import importlib.util
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location("results", Path(__file__).parents[1] / "check_test_results.py")
results = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(results)


class UnityResultChecks(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.folder = Path(self.temp.name)
        self.write()

    def write(self, root="Passed", case="Passed", suite="Passed", cases=True):
        xml = f'<test-run result="{root}"><test-suite result="{suite}">'
        if cases:
            xml += f'<test-case result="{case}" />'
        xml += '</test-suite></test-run>'
        for mode in ("EditMode", "PlayMode"):
            (self.folder / (mode + ".xml")).write_text(xml)

    def test_both_modes_pass(self):
        self.assertEqual({"EditMode": 1, "PlayMode": 1}, results.check_results(self.folder))

    def test_rejects_failure_skip_empty_and_suite_failure(self):
        for args in ({"root": "Failed"}, {"case": "Failed"}, {"case": "Skipped"},
                     {"suite": "Failed"}, {"cases": False}):
            with self.subTest(args=args):
                self.write(**args)
                with self.assertRaises(ValueError):
                    results.check_results(self.folder)

    def test_requires_both_modes(self):
        (self.folder / "PlayMode.xml").unlink()
        with self.assertRaises(ValueError):
            results.check_results(self.folder)
