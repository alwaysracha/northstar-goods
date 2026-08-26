#!/usr/bin/env python3
"""Scan staged additions for credential-like and dangerous code patterns."""
from __future__ import annotations

import re
import subprocess
import sys

DIFF = subprocess.run(
    ["git", "diff", "--cached", "-U0"],
    check=True,
    capture_output=True,
    text=True,
).stdout

added_lines = [
    line[1:] for line in DIFF.splitlines() if line.startswith("+") and not line.startswith("+++")
]

credential = re.compile(
    r"(?i)\b\w*(?:api_?key|secret|password|token|passwd)\w*\s*=\s*[\"'][^\"']{6,}[\"']"
)
dangerous = re.compile(
    r"Process\.Start|UseShellExecute\s*=\s*true|FromSqlRaw\(|ExecuteSqlRaw\(|curl[^|]*\|\s*(?:sh|bash)"
)
allowed = {
    'public const string DemoPassword = "LocalDemo!2026";',
}

findings: list[tuple[str, str]] = []
allowed_findings: set[str] = set()
for line in added_lines:
    for match in credential.finditer(line):
        if any(item in line for item in allowed):
            allowed_findings.add(match.group(0))
        else:
            findings.append(("credential-like assignment", match.group(0)))
    for match in dangerous.finditer(line):
        findings.append(("dangerous process/query execution", match.group(0)))

for match in allowed_findings:
    print(f"Allowed development-only credential: {match}")
if findings:
    for label, match in findings:
        print(f"{label}: {match}")
    sys.exit(1)

print("No blocking credential-like assignments or dangerous execution patterns found in staged additions.")
