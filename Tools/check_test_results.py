#!/usr/bin/env python3
"""Fail CI on missing, skipped, empty or failing Unity test output."""
from pathlib import Path
import sys
import xml.etree.ElementTree as ET


def check_results(folder):
    counts = {}
    for mode in ("EditMode", "PlayMode"):
        path = Path(folder) / (mode + ".xml")
        if not path.exists():
            raise ValueError("Missing test results: " + str(path))
        root = ET.parse(path).getroot()
        cases = list(root.iter("test-case"))
        suites = list(root.iter("test-suite"))
        if (root.get("result") != "Passed" or not cases
                or any(case.get("result") != "Passed" for case in cases)
                or any(suite.get("result") != "Passed" for suite in suites)):
            raise ValueError("Tests did not all pass: " + str(path))
        counts[mode] = len(cases)
    return counts


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: check_test_results.py <results-directory>")
    try:
        for mode, count in check_results(sys.argv[1]).items():
            print(f"{mode}: {count} passed")
    except (ValueError, ET.ParseError) as error:
        raise SystemExit(str(error))
