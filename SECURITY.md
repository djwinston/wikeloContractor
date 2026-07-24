# Security Policy

## Reporting a vulnerability

Please report suspected security issues **privately** rather than opening a public issue:

- Use GitHub's [private vulnerability reporting](https://github.com/djwinston/wikeloContractor/security/advisories/new)
  (Security → Report a vulnerability), or
- email the maintainer at the address on the GitHub profile.

Include the affected version, steps to reproduce, and the impact you observed. You can expect an
initial response within a few days.

## Scope

Wikelo Contractor is an unofficial, read-only companion app. It talks only to the public
[Star Citizen Wiki API](https://api.star-citizen.wiki/) and stores its data locally under
`%AppData%\WikeloContractor\`. It requires no account, sends no personal data, and does not read or
modify the Star Citizen game process.

## Verifying a release

Release builds are **not yet code-signed**. Until they are, verify a download two ways:

- **Checksum** — every release includes `SHA256SUMS.txt`; compare with
  `Get-FileHash <file> -Algorithm SHA256`.
- **Build provenance** — release artifacts carry a GitHub attestation proving they were built by this
  repository's release workflow: `gh attestation verify <file> --repo djwinston/wikeloContractor`.

Only download releases from the official
[Releases page](https://github.com/djwinston/wikeloContractor/releases).
