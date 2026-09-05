## What this changes

<!-- One or two sentences. If it fixes a bug, say what the bug was, not only what the fix is. -->

## Why

<!-- The reasoning is the valuable part of this codebase. Which real failure led to this? -->

## Effect on the architecture

<!-- Does this cross a layer boundary? Add or change a contract in Core? Touch the provider seam?
     "None — it stays inside Platform" is a perfectly good answer. -->

## Checks

- [ ] `dotnet test Source/KlangHub.Tests/KlangHub.Tests.csproj` is green.
- [ ] A test covers the change, and it was red for the right reason first.
- [ ] The build is warning-free — production and tests.
- [ ] New user-facing text is in **all 24** `Strings*.resx` files.
- [ ] `tools\check-no-private-data.ps1` passes. Nothing private in the diff: no real name, address,
      user directory, device log or screenshot.

## Tested on real hardware

<!-- Which device, and what you did. Say "not tested on a device" if that is the case — it is an
     honest answer and a reviewer can weigh it. Never trigger a click that can turn a real speaker up. -->

## Screenshot

<!-- Only if this changes the user interface, and only after reading docs/assets/README.md.
     Images are blocked by .gitignore, by RepositoryPrivacyTests and by the pre-commit hook, and
     that is deliberate. Attach it to the pull request; do not commit it. -->
