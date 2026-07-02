#!/usr/bin/env python3
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def fail(errors: list[str], message: str) -> None:
    errors.append(message)


def bundle_path(root: Path, ref: str) -> Path:
    ref = ref.strip().strip("`").strip(".,;)")
    if ref.startswith("bundle://"):
        return root / ref.removeprefix("bundle://")
    if ref.startswith("repo://"):
        rel = ref.removeprefix("repo://")
        if re.match(r"^[A-Za-z]:/", rel):
            return Path(rel)
        return root.parents[1] / rel
    return root / ref


def assert_exists(errors: list[str], root: Path, ref: str) -> None:
    path = bundle_path(root, ref)
    if not path.exists():
        fail(errors, f"missing referenced artifact: {ref} -> {path}")


def validate_structure(root: Path, errors: list[str]) -> None:
    required = [
        "inputs/00-original-request.md",
        "inputs/01-source-artifacts.md",
        "inputs/02-structured-input.md",
        "requirements/01-normalized-requirements.md",
        "architecture/01-target-solution.md",
        "plan/01-phase-plan.md",
        "plan/accountapp-ui-shared-components-plan.xlsx",
        "traceability/01-requirement-traceability.md",
        "reviews/00-bundle-self-review.md",
        "reviews/01-execution-report.md",
        "proof/00-evidence-manifest.md",
    ]
    for rel in required:
        assert_exists(errors, root, rel)

    plan = read_text(root / "plan/01-phase-plan.md")
    if "```mermaid" not in plan:
        fail(errors, "phase plan is missing a mermaid dependency map")

    subbundles = sorted((root / "subbundles").glob("*/README.md"))
    if len(subbundles) != 7:
        fail(errors, f"expected 7 subbundle README files, found {len(subbundles)}")


def validate_completed(root: Path, errors: list[str]) -> None:
    readme = read_text(root / "README.md")
    report = read_text(root / "reviews/01-execution-report.md")
    evidence = read_text(root / "proof/00-evidence-manifest.md")

    if "Execution status: `Completed across SB01-SB07`" not in readme:
        fail(errors, "root README validation summary is not completed")
    if "Execution state: `Completed`" not in report:
        fail(errors, "execution report is not marked completed")
    if re.search(r"Pending implementation|`Pending`|Not started", report):
        fail(errors, "execution report still contains pending/not-started language")

    for subbundle in sorted((root / "subbundles").glob("*/README.md")):
        text = read_text(subbundle)
        if "- `Completed`" not in text and "- Completed" not in text:
            fail(errors, f"subbundle not completed: {subbundle}")

    for index in range(1, 8):
        sb = f"SB{index:02d}"
        manifest = root / "proof" / sb / "manifest.md"
        invariants = root / "proof" / sb / "semantic-invariants.md"
        assert_exists(errors, root, f"proof/{sb}/manifest.md")
        assert_exists(errors, root, f"proof/{sb}/semantic-invariants.md")
        if manifest.exists():
            text = read_text(manifest)
            for marker in [
                "## Status",
                "Completed",
                "## Failing-First Or Adversarial Proof",
                "## Anti-Stub Audit",
                "## Production Behavior Artifact Matrix",
                "bundle://proof/SB07/changed-file-hashes.json",
                "bundle://proof/SB07/transcripts/source-assertions.txt",
            ]:
                if marker not in text:
                    fail(errors, f"{manifest} missing marker: {marker}")
        if invariants.exists():
            text = read_text(invariants)
            if f"## Invariant {sb}-INV-01" not in text:
                fail(errors, f"{invariants} missing invariant id")
            if "## Production Behavior Artifact Matrix" not in text:
                fail(errors, f"{invariants} missing production artifact matrix")

    proof_refs = [
        "proof/SB01/transcripts/components-qrcode-build.txt",
        "proof/SB01/transcripts/components-qrcode-tests.txt",
        "proof/SB03/transcripts/account-workspace-local-services-tests.txt",
        "proof/SB07/transcripts/ledger-solution-build.txt",
        "proof/SB07/transcripts/workbook-regeneration.txt",
        "proof/SB07/transcripts/source-assertions.txt",
        "proof/SB07/changed-file-hashes.json",
        "proof/SB07/red-team-verifier.md",
        "proof/SB07/screenshots/accountapp-final-home-large.png",
        "proof/SB06/screenshots/ownership-verification-large.png",
        "proof/SB04/screenshots/address-manager-large.png",
        "proof/SB05/screenshots/settings-backup-import-preview-large.png",
        "inputs/artifacts/accountapp-after-login-current.png",
    ]
    for ref in proof_refs:
        assert_exists(errors, root, ref)

    for text_name, text in [
        ("execution report", report),
        ("evidence manifest", evidence),
    ]:
        for ref in sorted(set(re.findall(r"bundle://[A-Za-z0-9_./-]+", text))):
            assert_exists(errors, root, ref)
        if "red-team-verifier.md" not in text and text_name == "evidence manifest":
            fail(errors, "evidence manifest does not cite red-team verifier")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--stage", choices=["prepared", "completed"], required=True)
    parser.add_argument("--bundle", type=Path, default=Path.cwd())
    args = parser.parse_args()

    root = args.bundle.resolve()
    errors: list[str] = []

    validate_structure(root, errors)
    if args.stage == "completed":
        validate_completed(root, errors)

    if errors:
        print(f"FAIL {args.stage}: {len(errors)} issue(s)")
        for error in errors:
            print(f"- {error}")
        return 1

    print(f"PASS {args.stage}: bundle validation succeeded for {root}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
