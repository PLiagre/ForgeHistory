# ADR-0019: geler G6, reculer le scope, produit = sim mince

**Date**: 2026-08-24
**Status**: accepted
**Deciders**: le propriétaire (demande du 2026-08-24), Cursor Cloud (rédaction)

Amende ADR-0018 (le moteur reste simple) et le récit F1 / E1 de
`ROADMAP.md`. Ne remplace pas ADR-0016 (`sim/` est le produit vivant).
N'édite pas `VISION.md`.

## Context

Les dernières itérations du relief G6 ont coûté cher et se sont
terminées en échec : tuiles manquantes, zéros fabriqués, re-preuves
SHA, lots qui ne nourrissent pas `sim/`. Le projet cherchait trop loin
(cadastre 1400, climat observé, gisements à consommer, vérité
historique prédictive) alors que le jeu réel tourne déjà :
`python -m sim`, couche 1 mince, snapshot `v0a-1`.

« Livré mais non consommé » est devenu un objectif sans fin. Ce n'est
plus un objectif.

## Decision

1. **G6 est gelé.** Plus de lot de sauvetage, plus de preuve Europe,
   plus de consommation par `sim/`. Les artefacts restent une archive
   sous `pipeline/geo/`. L'échec est accepté : le relief n'est pas un
   terrain jouable, et on arrête d'essayer.

2. **`pipeline/geo/` sort du chemin critique.** Ce n'est plus un
   produit parallèle. `sim/` continue de lire les cellules G3 déjà
   là (carte et commerce). On ne relance pas G6, climat observé, ni
   consommation R1 comme travail courant.

3. **Le scope recule jusqu'au jeu qui tourne.** Grandes étapes Hermes :
   le moteur mince (tick, économie physique de base, déterminisme,
   snapshot). Pas Unity. Pas de file geo. Les briefs trop loin
   (relief G6, consommation des gisements, viewer gisements, contrôles
   geo partagés) sont **abandonnés** : on ne les exécute plus. Le brief
   reste la source d'instruction s'il était relancé ; la file dit de
   ne pas les relancer.

4. **`sim/` n'annonce plus G6 ni R1.** Le snapshot ne porte plus ces
   couches « pour plus tard ». C1 déjà joint au snapshot peut rester :
   c'est du présent, pas une poursuite. Le tick ne lit toujours ni
   relief, ni gisements, ni climat.

## Alternatives Considered

### Alternative 1 : une dernière passe G6 « pour de vrai »
- **Pros** : le relief calculé ne serait pas perdu.
- **Cons** : les passes précédentes ont déjà échoué au même endroit
  (données DEM, zéros, SHA).
- **Why not** : le propriétaire coupe, il ne relance pas.

### Alternative 2 : supprimer `pipeline/geo/`
- **Pros** : plus rien à relancer.
- **Cons** : `sim/` lit encore G3 ; ce serait casser le moteur.
- **Why not** : archive, pas destruction.

### Alternative 3 : consommer R1 (brief 030) à la place de G6
- **Pros** : un autre artefact déjà livré entrerait dans le tick.
- **Cons** : même fuite en avant (le projet cherche trop loin).
- **Why not** : le jeu mince n'en a pas besoin.

## Consequences

### Positive
- Le quotidien rejoint ce qui marche déjà.
- Plus de boucle G6 qui finit en fail.

### Negative
- Pas de relief jouable, pas de gisements dans le tick.
- L'archive geo peut vieillir sans être rejouée.

### Risks
- **On relance G6 « juste une sentinelle ».** Atténuation : le routeur
  de tests ne planifie plus la preuve Europe ; la file marque les
  briefs concernés abandonnés.
- **On vide `sim/`.** Atténuation : tick, commerce physique,
  déterminisme et `cell_id` restent.

## Ce que cet ADR ne décide pas

1. Un réveil futur de `pipeline/geo/` (il faudrait une décision
   écrite nouvelle).
2. Le contenu d'un brief encore exécutable (la source unique reste
   ce brief).
3. Le réveil d'Unity.
