# Decision record, working notes and the assembly probe

These files are a **copy** of the Superpowers SDD workspace for this mod
(`.superpowers/sdd/2026-10-11-craft-from-drawers-and-chests/`), taken so that the record is carried by
**this** repository's history and survives the owner's periodic pushes.

## Why a copy rather than tracking it where it lives

`.superpowers/` is a Windows **junction** to `C:\Users\merlin\.cache\superpowers`, which is a clone of
the public Superpowers plugin (`https://github.com/obra/superpowers`). The SDD workspace lives inside that
clone as an untracked `sdd/` directory. Three consequences:

- **This repository cannot track it in place.** Git sees the junction's nested `.git` and would record a
  *gitlink* instead of the files — an embedded-repository entry, not the content.
- **No push of this repository carries anything under `.superpowers/`.**
- **Committing it in the clone would target someone else's public upstream.** That is why
  `.superpowers/sdd/.gitignore` keeps its blanket `*`: as a leak guard, not an inconvenience.

So the durable parts are copied here. `.superpowers/sdd/` stays the working copy; **this directory is the
one that survives.** If the two diverge, this one is what git carries, and it may be a task behind.

## Contents

| File | What it is |
|---|---|
| `progress.md` | **The ruling ledger** — every ruling, review outcome, deferred minor and its reasoning. The least reconstructible artifact here. |
| `author-verify.md` | The consolidated in-game checks to run on a real server and client before release. |
| `backpack-requirement.md` | Verified findings behind the AdventureBackpacks coexistence requirement. |
| `task-15-scope.md` | Verified scope facts for the station work (the engine already existed). |
| `scratchpad.md` | The theory written while one task was stuck, per the project protocol. |
| `task-*-brief.md` / `task-*-report.md` | Per-task requirements, and what was actually built and verified. |
| `review-*.findings.md` | The fresh-eyes review's findings and triage, including which findings were fixed, which were accepted and why. Copied; the companion `review-*.diff` is not (see below). |
| `tools/cecil.ps1` | The hardened Mono.Cecil probe used for **all** assembly verification. Reports method and field **visibility** — which is what makes the recurring "non-public member called directly" defect class checkable — and can dump one method's **IL** with `-IL -Type <name>`, the only way to see which argument values a call site pushes. The two copies of this file are kept **byte-identical** (see `progress.md`, 2026-10-13). |

## Deliberately not copied

- `review-*.diff` — the per-task review packages. Regenerable in seconds from a commit range with the
  SDD `review-package` script, so they are noise in history. (The **findings** that came out of a review
  *are* copied: a diff is reproducible, a triage and its reasoning are not.)
- `plan-path` — a one-line pointer to the Superpowers plan, which already lives in this repository at
  `docs/superpowers/plans/2026-10-11-craft-from-drawers-and-chests.md`. Copying it would duplicate a
  path, not a document.
- `dispatch-probe.txt` — a scratch marker written to test whether a dispatch could write files at all.
  No record value.
- Ad-hoc probe scripts (`tools/_inspect_*.ps1`, `scratchpad-decompile.ps1` and the like) — R25 forbids
  writing them and `tools/cecil.ps1` is the sanctioned tool; a stale copy here would be an invitation to
  use one.
- The `.superpowers/skills/` package itself — third-party, and not ours to vendor.
