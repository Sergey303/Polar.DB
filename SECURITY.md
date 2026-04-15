# Security Policy

## Supported versions

At this stage, security fixes are best-effort and focus on:

- the current `main` branch;
- the latest published package version, if a release exists.

Older versions may not receive security updates.

## Reporting a vulnerability

Please **do not** report security vulnerabilities through public GitHub issues.

Instead, use one of these private channels:

1. GitHub private vulnerability reporting for this repository, if enabled.
2. A private maintainer contact channel associated with the repository owner or maintainers.

## What to include

Please include:

- affected component or file;
- version, commit, or branch;
- impact description;
- reproduction steps or proof of concept;
- whether the issue could cause data corruption, data disclosure, denial of service, or unsafe file handling.

## What to expect

We will try to:

- acknowledge the report;
- validate or reproduce it;
- assess severity and impact;
- decide on a fix and disclosure approach.

Response times are best-effort and may vary depending on maintainer availability.

## Disclosure guidance

Please avoid public disclosure until maintainers have had a reasonable chance to investigate and respond.

This is especially important for issues involving:

- unsafe file handling;
- corruption or recovery paths;
- crafted binary inputs;
- index/state divergence;
- unintended data exposure.

## Non-security bugs

If the issue is not security-sensitive, use the normal bug-report flow described in [SUPPORT.md](SUPPORT.md).
