# Code Style & Quality Guidelines (client-facing-app)

Mirrors the repo-wide `.kilo/instructions.md`.

* NEVER use inline styles (`style="..."` or `style={{...}}`).
* Always use Tailwind CSS utility classes for styling.
* If a complex layout requires raw CSS, create a separate `.css` / `.module.css` file and import it.
* Prefer one component per file.
* Keep accessibility concerns visible: prefer semantic HTML, `aria-label`s on icon-only buttons.
