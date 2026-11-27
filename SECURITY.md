# Security policy

## Supported versions

Security fixes are made for the latest minor release. Earlier minor releases receive no fixes. The [Releases page](https://github.com/M-Colley/VIP-Sim2/releases) shows the current version.

## Reporting a vulnerability

Report vulnerabilities privately through GitHub's private vulnerability reporting for M-Colley/VIP-Sim2. Open the repository, choose the Security tab, then "Report a vulnerability". Do not open a public issue for a vulnerability.

Include the VIP-Sim version, the platform, the steps to reproduce and the impact you observed.

Expected time to first response: [owner to set].

## Scope

The following are in scope:

- The VIP-Sim application for Windows, macOS and Linux.
- The update check, which sends a request to the GitHub releases API.
- Parsing of the policy file (`policy.json`) and of condition profiles.

Report vulnerabilities in third-party components (Unity, MediaPipe and the others in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)) to their maintainers, and tell us so that we can update the dependency.
