#!/usr/bin/env python3
"""Fail CI on missing, skipped, empty or failing Unity test output."""
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

folder=Path(sys.argv[1])
for mode in ["EditMode","PlayMode"]:
    path=folder/(mode+".xml")
    if not path.exists():raise SystemExit("Missing test results: "+str(path))
    root=ET.parse(path).getroot()
    cases=list(root.iter("test-case"))
    if not cases or any(case.get("result")!="Passed" for case in cases):
        raise SystemExit("Tests did not all pass: "+str(path))
    print(mode+": "+str(len(cases))+" passed")
