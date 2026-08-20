# HANDOFF.md

> **Borné à trois sessions** par ADR-0014 amendement 001. Les sessions plus
> anciennes sont dans git ; le récit du projet va dans `hermes/reports/`.

## Session la plus récente — 2026-08-20 : Hermes lance Claude pour le brief

**Contexte** : le propriétaire veut un prompt, puis que tout s’enchaîne.
Si un brief manque, c’est à Hermes de lancer Claude.

### Ce qui a été fait

`forgepilot lot` : proposition → Claude rédige le brief → plan → Cursor →
draft PR → review. Hermes n’écrit pas le brief. Prompt d’ordre :
`hermes/prompts/ENCHAINER.md`. ADR-0013 amendement 003.

### Prochain pas

Tirer `master` sur le VPS. Coller le prompt ENCHAINER dans
`hermes chat -s forgehistory-suivi`. Fusionner la draft PR à la fin.

---

## Session précédente — 2026-08-20 : forgepilot enchaine

**Contexte** : le propriétaire veut lancer Hermes et qu'un lot parte tout
seul une fois le brief écrit. Pas de fusion automatique.

### Ce qui a été fait

`forgepilot enchaine <brief.md>` : aperçu sans `--run`, puis plan →
execute → draft PR → review. Refuse une proposition Hermes. Jamais de
fusion. Skill Hermes et README alignés. ADR-0013 amendement 002.

### Prochain pas

Sur le VPS : `forge-start` puis `hermes chat -s forgehistory-suivi`.
Quand un brief existe, Hermes lance `enchaine` (aperçu, puis `--run`).
G6 reste une proposition : il faut encore une session Claude pour le
brief.

---

## Session encore avant — 2026-08-20 : sim/ sans Unity, Hermes pilote

**Contexte** : décision propriétaire. Unity visuel en veille. La simulation
doit tourner sans Unity. Hermes ne doit plus être un teneur de roadmap :
il propose, y compris des crons quotidiens.

### Ce qui a été fait

1. Demande `hermes/requests/DEMANDE-20260820-simulation-sans-unity-hermes-pilote.md`.
2. **ADR-0016 accepté** : `sim/` est le produit vivant ; Unity en veille ;
   Hermes pilote et propose.
3. **ADR-0015 accepté** (amendement : crons quotidiens maintenant, aucun
   cron ne fusionne).
4. Contrat Hermes élargi : `hermes/README.md`, skill `forgehistory-suivi`,
   `hermes/propositions/`, `hermes/crons/quotidien.sh`.
5. Entrée réelle : `python -m sim` (`sim/__main__.py`).
6. Docs alignés : `ROADMAP.md`, `CLAUDE.md`, `AGENTS.md`, `sim/README.md`,
   `unity/README.md`, `architecture/README.md`.

### Prochain pas

Hermes installe le cron quotidien sur le VPS et propose la prochaine
couche de `sim/` (G6 relief côté geo, ou économie au-delà de la nourriture
côté moteur). Tout lot Unity se refuse.
