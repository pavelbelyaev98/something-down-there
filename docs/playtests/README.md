# Playtests

One note per playtest the user still has to do. **The user deletes a note after playing it: a
missing note means played.** Agents never recreate a deleted note or ask for it again; feedback
arrives in chat and follows the post-playtest protocol in [AGENTS.md](../../AGENTS.md).

## Writing a note

- File: `<task id>-<slug>.md`, written in the change that makes the feature playable. A later change
  to the same feature updates the pending note in place; it never adds a second note.
- Written for the user as a player: plain words, under 30 lines, no code or test detail.
- Sections, in order:
  - **Build & start:** which build, New Game or Continue, launch flags, where to go (developer
    shortcuts welcome unless the test forbids help).
  - **Try:** concrete things to do.
  - **Good feels like:** the intended feel, from the concept.
  - **Compare (A/B):** each switch, where it is and what to compare. Omit when there is none.
  - **Tell the agent:** only the answers that change code (A/B winner, pass/fail), plus anything
    that felt off.
- A/B variants and pass/fail follow-ups stay in the code until the user answers in chat. If a note
  was deleted and its answer never came, ask once.
