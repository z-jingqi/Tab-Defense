# Validation guide

Run structural checks and tooling tests:

```bash
python -m pip install -r Tools/requirements.txt
python Tools/validate_project.py
python -m unittest discover -s Tools/tests -v
bash -n Tools/run_unity_checks.sh
```

These checks validate asset references, configuration and tooling. They do not compile C# or replace Unity tests. The Project structure GitHub workflow runs them on pushes and pull requests.

Run EditMode and PlayMode in Unity Test Runner using the version in ProjectSettings/ProjectVersion.txt, or:

```bash
UNITY_EDITOR="/path/to/Unity" bash Tools/run_unity_checks.sh
```

The script rejects missing/empty/failed results. Logs and XML belong in ignored TestResults/, not in source control.

Automated coverage includes core damage and upgrades, spawning and Boss defenses, fog masking, vertical arrows and firing lanes, hero movement boundaries, pause/restart, enemy assembly and melee windup/range/cooldown/death, overhead health bars, and full-width bottom experience fill/reset.

For manual acceptance, drag the hero with the editor mouse, release at both boundaries, evade enemy windups, inspect health/experience bars, pause/resume and restart after defeat. Observe a crowded assembly line and the Console. Test touch input, safe areas, background/resume and performance on actual target devices before release. Long-run Boss balance and platform builds need separate acceptance using WEB_BUILD.md and IOS_BUILD.md.
