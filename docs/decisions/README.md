# Decision records

One short file per important decision (architecture, data model, interfaces, scope, business
model, larger UI choices). They make our reasoning traceable for the jury and for a possible paper
later.

- File name: `NNNN-short-title.md` (next free number). Copy [`0000-template.md`](0000-template.md).
- Keep it short: 10–30 lines. Link to code or docs instead of copying them.
- A decision is **accepted** when the team agrees. The review by a fresh-context subagent (see
  `AGENTS.md`) is recorded in the file.
- Do not rewrite an accepted decision. To change it, write a new record that supersedes it and set
  the old one to `superseded by NNNN`.

| No. | Decision | Status |
|---|---|---|
| [0001](0001-data-storage.md) | Data storage layout (`data/raw`, `data/derived`, fetch script) | accepted |
| [0002](0002-agent-guide.md) | Agent working guide (`AGENTS.md`), reviews, Git, deadlines | accepted |
