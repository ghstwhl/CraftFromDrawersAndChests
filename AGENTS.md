# Agent instructions — CraftFromDrawersAndChests

This repository is a **Valheim mod** (BepInEx 5 / HarmonyX / Jötunn, net462).

1. **Follow the shared agent definition:**

   https://github.com/ghstwhl/coding_agents/blob/main/AGENT_VALHEIM_MODDER.md

   Raw text (the `blob/` URL is a web page, not a fetchable document):
   `https://raw.githubusercontent.com/ghstwhl/coding_agents/main/AGENT_VALHEIM_MODDER.md`.
   If it cannot be retrieved, ask the user to paste it — do not work from memory of it.

   Treat it as the working doctrine for all Valheim modding tasks here — game-API/IL investigation,
Harmony/HarmonyX patch rules, the publicized-assembly trap, testing strategy, diagnostics,
verification without running the game, and releasing.

2. **Then the repo's own rules:** `.github/copilot-instructions.md` (the Superpowers protocol and the
   `plan.md` workflow) and anything under `.github/instructions/`.

3. Local rules take precedence over the shared definition. If the two disagree, follow the local rule
   and report the conflict.

See `README.md` for what the mod does, and `CHANGELOG.md` for the current version.
