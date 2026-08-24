# HANDOFF.md

> **Borné à trois sessions** par ADR-0014 amendement 001. Les sessions plus
> anciennes sont dans git ; le récit du projet va dans `hermes/reports/`.

## Session la plus récente — 2026-08-24 : G6 gelé, scope reculé (ADR-0019)

**Contexte** : les dernières itérations G6 ont coûté cher et ont
échoué. Le projet cherchait trop loin. Suite de la PR #134 sur
`cursor/simplifier-workflow-sim-5161`.

### Ce qui a été fait

1. ADR-0019 : G6 est gelé (échec accepté). `pipeline/geo/` sort du
   chemin quotidien. « Livré mais non consommé » n'est plus un objectif.
2. Snapshot `sim/` mince : plus de couches G6/R1 « pour plus tard ».
   C1 déjà joint reste du présent. Le tick ne lit ni relief, ni
   gisements, ni climat.
3. Routeur de preuves : plus de preuve Europe / sentinelle G6 / preuve
   R1 sur le chemin quotidien. Briefs trop loin listés dans
   `harness/queue/ABANDONED.md` (on ne les relance pas).
4. ROADMAP reculée jusqu'à `python -m sim`. Hermes écrit des grandes
   étapes courtes collées au jeu réel.

### Prochain pas

Revue humaine de la PR #134. Produit quotidien : `python -m sim`.
Ne pas relancer G6, 026, 030, 031, 032.

---

## Session précédente — 2026-08-24 : ADR-0018, workflow et sim simplifiés

**Contexte** : moins d'itérations revue/test, jeu plus simple. Même
branche.

### Ce qui a été fait

Hermes Sol 5.6 prépare les grandes étapes ; Cursor découpe et exécute.
Harnais trois rôles optionnel. Checks PR allégés sans mentir. Plus de
porte |mesure − formule fermée|.

### Prochain pas (alors)

Aller plus loin : geler G6. Fait dans la session ci-dessus.

---

## Session encore avant — 2026-08-23 : #126 / #132, puis l'échec G6

G6 et R1 ont été livrés comme artefacts. Les re-preuves ont ensuite
échoué ou n'ont pas nourri `sim/`. Unity en veille. Cette piste est
close par ADR-0019.
