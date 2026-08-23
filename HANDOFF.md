# HANDOFF.md

> **Borné à trois sessions** par ADR-0014 amendement 001. Les sessions plus
> anciennes sont dans git ; le récit du projet va dans `hermes/reports/`.

## Session la plus récente — 2026-08-23 : #126 fusionné, base saine

**Contexte** : #126 est dans `origin/master` (`7901ce8`). Les docs de
pilotage étaient périmés et trop nombreux. Lot documentaire seulement.

### Ce qui a été fait

1. Vérité produit : G6 livré non consommé, V0 première tranche, viewer
   mince, Unity en veille.
2. Dashboard sans file d'audits morts. Demandes et proposition G6 fermées.
3. Skill Hermes : boot court. `architecture/` et briefs 001–025 hors boot.

### Prochain pas

Un seul : exécuter le brief 026 (gisements). G6 consommable attend le
cache Copernicus. Rien n'attend côté propositions.

---

## Session précédente — 2026-08-20 : forgepilot enchaine

`forgepilot enchaine <brief.md>` : aperçu, puis plan → execute → draft PR
→ review. Refuse une proposition Hermes. Jamais de fusion.

---

## Session encore avant — 2026-08-20 : sim/ sans Unity, Hermes pilote

ADR-0016 : `sim/` est le produit vivant. Unity en veille. Hermes propose
et cadance. Entrée : `python -m sim`.
