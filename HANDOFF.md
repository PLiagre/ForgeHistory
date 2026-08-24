# HANDOFF.md

> **Borné à trois sessions** par ADR-0014 amendement 001. Les sessions plus
> anciennes sont dans git ; le récit du projet va dans `hermes/reports/`.

## Session la plus récente — 2026-08-24 : ADR-0018, workflow et sim simplifiés

**Contexte** : le propriétaire a demandé moins d'itérations revue/test et
un jeu plus simple. Branche `cursor/simplifier-workflow-sim-5161`.

### Ce qui a été fait

1. ADR-0018 : Hermes (GPT Sol 5.6) prépare les grandes étapes ; Cursor
   prend un brief large, le découpe et exécute en parallèle. Le harnais
   trois rôles devient optionnel ; la porte mécanique dit encore la
   vérité si on l'appelle.
2. Checks PR allégés : `risk-gate` et `audit-check` hors chemin (succès
   explicite, sans mentir). `gitleaks` ignore les scripts de preuve des
   briefs (faux positif 026). Tests `sim/` corrigés pour R1
   `not_consumed` (régression #132).
3. Simulation : plus de porte |mesure − formule fermée|. On garde tick,
   commerce physique, déterminisme, ADR-0003. Unity reste en veille.

### Prochain pas

Revue humaine de cette PR. Ne pas fusionner tant que `sim-tests` et
`gitleaks` ne sont pas verts. Le brief 026 est livré (artefacts R1
présents, non consommés par `sim/`).

---

## Session précédente — 2026-08-23 : preuve Europe G6 verte après #130

**Contexte** : #130 est dans `origin/master`. Sur le VPS, le cache
Copernicus complet est vérifié `1110/1110` et la preuve Europe G6 est
verte. Le relief est calculé mais `sim/` ne le lit pas. #132 a ensuite
livré les gisements 026 (non consommés).

### Prochain pas (alors)

Exécuter le brief 026 — **fait dans #132**.

---

## Session encore avant — 2026-08-23 : #126 fusionné, base saine

G6 livré non consommé, V0 première tranche et viewer mince. Unity en
veille.
