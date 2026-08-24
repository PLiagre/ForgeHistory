# ADR-0018: Hermes prépare les grandes étapes ; Cursor découpe et exécute

**Date**: 2026-08-24
**Status**: accepted
**Deciders**: le propriétaire (demande du 2026-08-24), Cursor Cloud (rédaction)

Amende ADR-0010, ADR-0013, ADR-0016 et ADR-0017. Ne remplace pas
la source unique d'instruction (le brief) ni le produit vivant `sim/`
(ADR-0016). N'édite pas `VISION.md`.

## Context

Le chemin actuel empile trop d'allers-retours : harnais trois rôles,
ForgePilot plan → code → juge, checks PR nombreux, audits d'architecture
sur chaque lot. Une partie de ces portes **marche** (tests `sim/`, suite
harnais, gitleaks une fois les faux positifs écartés). Une autre **ne
mesure rien d'utile** ou bloque pour de mauvaises raisons (déclaration
de risque sur le corps de PR, garde d'audit historique, gitleaks qui
prend un script de preuve pour une clé, revue trois rôles qui rejoue
le même lot).

En parallèle, la simulation exige trop : données historiques
« absolument valides » et prédictions fermées calées au dixième près.
Le moteur vivant (`python -m sim`) tourne déjà. On n'a pas besoin d'un
modèle prédictif ultra-précis pour avancer.

Le propriétaire veut : moins d'itérations, un Hermes clair sur les
grandes étapes, un Cursor qui prend un brief large et l'exécute en
parallèle, un jeu plus simple.

## Decision

1. **Hermes (GPT Sol 5.6)** suit le projet, tient `ROADMAP.md` et la
   vision opérationnelle, et **prépare les grandes étapes**. Modèle
   principal : `openai/gpt-5.6-sol-high` via Nous Portal ; repli
   `openai/gpt-5.6-sol-xhigh` pour un point difficile. Hermes n'écrit
   pas le code produit, ni un verdict, ni la CI. Il n'écrit pas non plus
   le brief d'exécutant : il rédige la demande / la proposition / le
   contour de l'étape. Le brief, quand il existe, reste la seule
   instruction d'un lot.

2. **Cursor exécute.** Il prend un **brief large** (ou, à défaut, la
   grande étape Hermes plus le brief existant), le **découpe en
   sous-tâches indépendantes**, et les **exécute en parallèle**
   (sous-agents internes, un seul worktree, une seule PR). Il ne relance
   pas une boucle plan / test / revue par sous-tâche.

3. **Le harnais trois rôles devient optionnel.** Il reste disponible
   quand on veut une archive de preuves avec porte mécanique
   (`harness/verdict_audit.py`). Il n'est plus le chemin par défaut d'un
   lot produit. Quand on l'invoque, la porte dit encore la vérité : on
   ne la contourne pas, on ne la fait pas mentir. Le producteur ne
   fusionne toujours pas son propre travail.

4. **ForgePilot reste le chemin durable** (VPS, reprise, `merge`
   mécanique). Ce n'est plus le goulot de chaque changement. Un agent
   Cursor Cloud peut livrer une PR directement depuis un brief large.

5. **Les checks PR se réduisent au vital.** Gardés : tests `sim/`,
   tests harnais, tests ForgePilot, `gitleaks` (vrais secrets),
   `actionlint` si un workflow change, démo F0 (la porte refuse un faux
   brief). Retirés du chemin PR : la porte de risque (`risk-gate`) et
   la garde d'audits historiques (`audit-check`). Ces jobs existent
   encore pour ne pas casser une protection de branche déjà déclarée ;
   ils se contentent d'un succès explicite « hors chemin », sans
   prétendre protéger le lot.

6. **La simulation reste simple et honnête.** On garde ce qui tourne :
   amorçage, tick, commerce physique, conservation de la masse,
   déterminisme, `cell_id` unique (ADR-0003), snapshot `v0a-1`. On ne
   bloque plus le moteur vivant sur une reconstruction historique
   exhaustive ni sur l'écart serré entre une formule fermée et la
   mesure. Les constantes de `sim/SEEDING.md` sont des **proxys d'ordre
   de grandeur**, déjà déclarés comme tels. Unity reste en veille.

## Alternatives Considered

### Alternative 1 : garder le harnais trois rôles obligatoire
- **Pros** : « celui qui produit ne prononce pas la recevabilité »
  reste une porte à chaque lot.
- **Cons** : chaque lot rejoue Planificateur / Générateur / Évaluateur
  et s'arrête dès qu'une itération ne progresse pas.
- **Why not** : le propriétaire demande moins d'itérations ; la revue
  humaine de la PR et les tests vitaux suffisent pour un lot ordinaire.

### Alternative 2 : supprimer `sim/` des exigences physiques
- **Pros** : encore plus simple.
- **Cons** : casserait le produit vivant (économie qui se téléporte,
  double clé spatiale).
- **Why not** : on allège la vérité historique et la prédiction, pas le
  moteur.

### Alternative 3 : remettre Sol comme juge de chaque PR (rejet d'ADR-0017)
- **Pros** : une autre famille que Grok relit le code.
- **Cons** : recréerait la boucle de revue que cet ADR raccourcit.
- **Why not** : Sol pilote Hermes (grandes étapes), pas la revue de
  chaque SHA.

## Consequences

### Positive
- Un seul cerveau de suivi (Hermes / Sol 5.6) et un seul exécutant
  (Cursor) pour le quotidien.
- Moins de checks qui rougissent sans protéger une régression.
- Le moteur peut avancer sans attendre un cadastre de 1400 ni une
  prédiction calée au millième.

### Negative
- Un lot Cursor n'a plus d'Évaluateur automatique par défaut. La PR
  humaine et la CI vitale portent ce rôle.
- Un brief trop large mal découpé peut faire travailler deux
  sous-agents sur le même fichier.

### Risks
- **Un sous-agent Cursor est pris pour un juge.** Atténuation : le
  producteur ne fusionne pas ; `forgepilot merge` et le propriétaire
  restent les seules fusions.
- **La porte mécanique est invoquée puis ignorée.** Atténuation : si
  `verdict_audit.py` tourne, son REJECT arrête le lot. On ne l'appelle
  plus « pour la forme ».
- **On vide `sim/` en croyant simplifier.** Atténuation : décision 6
  liste ce qui reste intouchable.

## Ce que cet ADR ne décide pas

1. Le contenu du brief 026 ni d'un brief futur (la source unique reste
   le brief lui-même).
2. Le réveil d'Unity.
3. La réactivation de `mode: full_auto`.
