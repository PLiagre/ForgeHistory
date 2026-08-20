# HANDOFF.md

> **Borné à trois sessions** par ADR-0014 amendement 001. Les sessions plus
> anciennes sont dans git ; le récit du projet va dans `hermes/reports/`.

## Session la plus récente — 2026-08-20 : sim/ sans Unity, Hermes pilote

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

---

## Session précédente — 2026-08-19 : skill à jour, verdicts 022/023, ADR-0015 proposé

Skill `forgehistory-suivi` alignée sur l’état réel (verdicts 022/023
ACCEPT, ADR-0014 accepté, VPS déjà là). ADR-0015 alors `proposed`.
Demandes bootstrap VPS.

---

## Session encore avant — 2026-08-16 : lots 022–023 fusionnés, ADR-0014 accepté

ForgePilot : stdin pour la revue, `iterate`, modèles par rôle. ADR-0014
accepte le partage Hermes déclenche / Claude juge / Cursor exécute.
