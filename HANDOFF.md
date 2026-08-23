# HANDOFF.md

> **Borné à trois sessions** par ADR-0014 amendement 001. Les sessions plus
> anciennes sont dans git ; le récit du projet va dans `hermes/reports/`.

## Session la plus récente — 2026-08-23 : preuve Europe G6 verte après #130

**Contexte** : #130 est dans `origin/master` (`6c2edcd`). Sur le VPS, le cache
Copernicus complet est vérifié `1110/1110` et la preuve Europe G6 est verte,
avec deux passes identiques. Le relief est calculé mais `sim/` ne le lit pas.

### Ce qui a été fait

1. #130 a corrigé l'import `dem_batch` et Q10.
2. La preuve Europe G6 n'est plus bloquée par le cache.
3. Le snapshot reste honnête : `relief_g6 = not_consumed` ; Unity reste en veille.

### Prochain pas

Un seul : exécuter le brief 026 (gisements). Aucun lot de consommation G6 en
parallèle ; il vient seulement après 026. La proposition du 23 août a été
actualisée pour retirer D2 des blocages.

---

## Session précédente — 2026-08-23 : #126 fusionné, base saine

G6 livré non consommé, V0 première tranche et viewer mince. Les documents de
pilotage ont été resserrés ; `architecture/` et briefs 001–025 restent hors boot.

---

## Session encore avant — 2026-08-20 : forgepilot enchaine

`forgepilot enchaine <brief.md>` : aperçu, puis plan → execute → draft PR
→ review. Refuse une proposition Hermes. Jamais de fusion manuelle.
