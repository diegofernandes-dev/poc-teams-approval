# GMUD create — screenshot capture notes

## Normative reference

- [`../gmud-create-reference.jpg`](../gmud-create-reference.jpg)
- [`../gmud-create-screen.md`](../gmud-create-screen.md)

## F1.2 visual consistency review

Before declaring F1.2 complete, compare GMUD side-by-side with at least two native Backstage form experiences in this portal:

| Experience | Route | What to compare |
|---|---|---|
| Catalog Import | `/catalog-import` | outlined TextField, InfoCard, contained/outlined buttons, Header/Content |
| Create / Scaffolder | `/create` → template wizard | InfoCard form shell, page Header, control density |

GMUD should share the same control language and shell, with better composition (single form surface, numbered sections, quieter rail) — not a parallel theme.

### Consistency findings (F1.2)

| Aspect | Catalog Import / Create | GMUD F1.2 |
|---|---|---|
| Page shell | `Page` + `Header` + `Content`, `themeId="home"` | Same |
| Form controls | MUI outlined TextField / Select | Same (`outlined` + `size="small"` density only) |
| Buttons | contained primary / outlined secondary | Same (`textTransform: none` for readability) |
| Surfaces | InfoCard / paper | One main form surface + quiet rail InfoCards (no elevation) |
| Typography | Theme variants | Theme `subtitle1` / `caption` / `body2` — no custom type scale |
| Color | Portal primary accent | Section markers + primary CTA only |

**Verdict:** GMUD remains in the same product family as Catalog Import and Create. Differences are composition (single surface, numbered sections, context rail), not a second design system.

## Captured artifacts (F1.2)

| File | What it shows |
|------|----------------|
| `gmud-create-f1.2-after.png` | Authenticated F1.2 GMUD create (single surface + quiet rail) — **use as F1.3 “before” baseline** |
| `backstage-catalog-import.png` | Catalog Import for consistency comparison |
| `backstage-scaffolder-create.png` | Create / Scaffolder list for consistency comparison |
| `gmud-create-ia-reference.jpg` | Product IA reference (composition authority, not F1.1 pixel baseline) |
| `gmud-create-f1.2-after-headless.png` | Headless capture (auth wall — not useful for review) |

## F1.3 semantic revision

F1.3 changes domain language and field composition, not visual chrome. Compare against `gmud-create-f1.2-after.png` for before/after review.

| File | What it shows |
|------|----------------|
| `gmud-create-f1.3-after.png` | F1.3 GMUD create after semantic revision (manual capture required) |

Key visible differences from F1.2:

- **Alvo da mudança** + **Classificação da mudança** replace Aplicação / Ambiente PRD / Versão·Artefato row
- **Janela de Execução** (not Implantação)
- **Avaliação de Risco** with **Plano de reversão**
- **Fluxo da Mudança** rail (generic steps, no Teams/CAB/deploy wording)
- Neutral evidence zero-state

Manual capture (signed in, ~1440px width):

```bash
"/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" \
  --headless=new --disable-gpu --window-size=1440,1600 \
  --user-data-dir=/tmp/backstage-gmud-shot \
  --screenshot="$(pwd)/docs/ui/screenshots/gmud-create-f1.3-after.png" \
  "http://localhost:3000/gmud"
```

A true F1.1 “before” PNG was not available in-repo; use git history of the card-heavy layout or the IA reference for structural comparison.

## Automated capture limitation

Headless Chrome against `http://localhost:3000/gmud` hits the Entra ID sign-in wall (no session cookies).  
`screencapture` / window capture from the agent environment is often blocked.

## Manual capture (required for product review)

With the portal running and signed in (~1440–1600px width):

```text
docs/ui/screenshots/gmud-create-f1.1-before.png   # F1.1 card-heavy layout (if available)
docs/ui/screenshots/gmud-create-f1.2-after.png    # F1.2 single surface + quiet rail
```

Optional CLI (after signing in once with a dedicated profile):

```bash
"/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" \
  --headless=new --disable-gpu --window-size=1440,1600 \
  --user-data-dir=/tmp/backstage-gmud-shot \
  --screenshot="$(pwd)/docs/ui/screenshots/gmud-create-f1.2-after.png" \
  "http://localhost:3000/gmud"
```

(First run that profile interactively, sign in, then re-run headless.)

Also capture Catalog Import and Create for the consistency appendix if needed:

```text
docs/ui/screenshots/backstage-catalog-import.png
docs/ui/screenshots/backstage-scaffolder-create.png
```

## Auth-wall / headless artifacts

| File | What it shows |
|------|----------------|
| `gmud-create-before-auth-wall.png` | Unauthenticated sign-in screen |
| `gmud-create-after-headless.png` | Blank/unauthenticated Playwright capture |
| `gmud-create-reference.png` | Product reference composition |

Prefer a real authenticated F1.1 → F1.2 before/after pair for reviews.
