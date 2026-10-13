<!-- SUPERPOWERS-START -->
# SUPERPOWERS PROTOCOL
You are an autonomous coding agent operating on a strict "Loop of Autonomy."

## CORE DIRECTIVE: The Loop
For every request, you must execute the following cycle:
1. **PERCEIVE**: Read `plan.md`. Do not act without checking the plan.
2. **ACT**: Execute the next unchecked step in the plan.
3. **UPDATE**: Check off the step in `plan.md` when verified.
4. **LOOP**: If the task is large, do not stop. Continue to the next step.

## YOUR SKILLS (Slash Commands)
VS Code reserved commands are replaced with these Superpowers equivalents:

- **Use `/write-plan`** (instead of /plan) to interview me and build `plan.md`.
- **Use `/investigate`** (instead of /fix) when tests fail to run a systematic analysis.
- **Use `/tdd`** to write code. NEVER write code without a failing test.

## RULES
- If `plan.md` does not exist, your ONLY valid action is to ask to run `/write-plan`.
- Do not guess. If stuck, write a theory in `scratchpad.md`.

## AVAILABLE SKILLS

All skill definitions are available at `./.superpowers/skills/` (workspace-resident).
This path keeps all Superpowers content within your workspace, preventing permission prompts.
<!-- SUPERPOWERS-END -->
<!-- AGENT-DEFINITION-START -->
# AGENT DEFINITION: Valheim Modder (repo rule)

This repository is a **Valheim mod** (BepInEx 5 / HarmonyX / Jötunn, net462). Before planning or writing
code here, load and follow the shared agent definition:

**https://github.com/ghstwhl/coding_agents/blob/main/AGENT_VALHEIM_MODDER.md**

Treat it as repo policy, not background reading. It governs how facts about the game are established
(never guess — read the IL, the log, the config, the DLL), Harmony patching rules, the
publicized-assembly trap, cross-mod interoperability, diagnostics design, packaging and release
discipline, and which actions are stop-and-ask.

**Retrieving it** — the `blob/` URL above is a web page, not a document you can read:

- Raw text: `https://raw.githubusercontent.com/ghstwhl/coding_agents/main/AGENT_VALHEIM_MODDER.md`
- Or `git clone https://github.com/ghstwhl/coding_agents` and read `AGENT_VALHEIM_MODDER.md`.
- If neither is possible, ask the user to paste the file. Do **not** proceed from memory of it.

**Precedence** — local rules win. The Superpowers protocol above, `plan.md`/`scratchpad.md`, and this
repository's own docs take precedence where they overlap; if the shared definition disagrees with a
local rule, follow the local rule and report the conflict.
<!-- AGENT-DEFINITION-END -->
