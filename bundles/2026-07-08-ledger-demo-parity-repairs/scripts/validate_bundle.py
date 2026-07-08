#!/usr/bin/env python3
from __future__ import annotations

import runpy
from pathlib import Path


validator = Path.home() / ".codex" / "skills" / "candoitall-bundle-preparation" / "scripts" / "validate_bundle.py"
if not validator.is_file():
    raise SystemExit(f"CanDoItAll bundle validator not found: {validator}")

runpy.run_path(str(validator), run_name="__main__")
