# ADR-0016: sim/ sans Unity est le produit vivant ; Hermes pilote et propose

**Date**: 2026-08-20
**Status**: accepted
**Deciders**: le propriétaire (décision orale du 2026-08-20), Cursor
(rédaction, rôle exécutant)

Amende ADR-0013, ADR-0014 et ADR-0015. Ne remplace pas la séparation
« celui qui produit ne prononce pas la recevabilité ».

## Context

Le dépôt portait deux mondes : un moteur Python étroit sous `sim/`, et un
jeu Unity hérité de VictoriaProject qui contient encore sa propre
simulation. Les documents d’état se contredisaient (stubs, HANDOFF en
retard, Hermes présenté comme simple teneur de `ROADMAP.md`). Le
propriétaire a tranché : le visuel Unity est en veille ; la simulation
doit tourner sans Unity ; Hermes doit piloter et s’auto-améliorer, pas
seulement tenir une feuille de route.

ADR-0015 était resté `proposed` et conditionnait les crons à un bilan
écrit des trois lots ForgePilot. Les lots `021`–`023` sont livrés. Le
propriétaire autorise maintenant les crons quotidiens sans attendre ce
bilan comme préalable.

## Decision

1. **`sim/` est la seule simulation vivante.** Elle doit s’exécuter sans
   Unity (`python -m sim`). Les couches à venir (villes, États, armées,
   batailles) s’écrivent dans `sim/`, pas dans `unity/`.
2. **`unity/` est en veille.** Aucun lot visuel ou Unity tant que le
   propriétaire ne le rouvre pas. Le code Unity reste une référence gelée,
   jamais une seconde source de vérité.
3. **Hermes est le pilote.** Il propose des améliorations, tient la
   mémoire, fixe la cadence, lance ForgePilot. Il peut modifier sa propre
   skill et écrire sous `hermes/**` et `ROADMAP.md`. Il n’écrit toujours
   pas le code produit, la CI, un brief, une rubrique, un verdict, un
   audit. Il ne fusionne pas.
4. **Les crons quotidiens sont autorisés** : lecture, mesure, proposition,
   régénération de vue. Aucun cron ne fusionne, n’écrit du code produit,
   ni ne paraphrase un brief.
5. **ADR-0015 passe à `accepted`**, avec l’amendement : le déverrouillage
   des crons est cette décision propriétaire, pas le bilan des trois lots.

## Alternatives Considered

### Alternative 1 : garder Unity comme démonstrateur prioritaire
- **Pros** : un humain voit le monde tout de suite.
- **Cons** : deux simulations ; le moteur Python n’est pas le jeu.
- **Why not** : le propriétaire a mis le visuel en veille.

### Alternative 2 : attendre le bilan ADR-0013 avant tout cron
- **Pros** : respecte la lettre d’ADR-0015 tel que proposé.
- **Cons** : bride Hermes alors que les trois lots sont déjà livrés et que
  le propriétaire demande explicitement des crons quotidiens.
- **Why not** : le propriétaire tranche maintenant.

### Alternative 3 : laisser Hermes écrire aussi les briefs
- **Pros** : un seul interlocuteur de bout en bout.
- **Cons** : une issue ou une proposition deviendrait une instruction ;
  la source unique d’instruction (le brief) casserait.
- **Why not** : Hermes propose ; Claude écrit le brief quand le
  propriétaire le demande.

## Consequences

### Positive
- Une seule cible produit : le moteur Python.
- Hermes retrouve un rôle de chef de projet qui propose, pas un rôle de
  copiste de feuille de route.
- Les documents d’état peuvent cesser de se contredire.

### Negative
- Le jeu n’a plus de démonstrateur visuel actif.
- Les crons peuvent produire du bruit (propositions quotidiennes) s’ils
  ne restent pas bornés au contrat.

### Risks
- **Une proposition Hermes est lue comme un brief.** Atténuation : format
  `hermes/propositions/`, interdiction de porter des conditions de succès
  d’exécutant, contrôle existant `test_single_source_of_instruction.py`.
- **Un cron pousse sur `master`.** Atténuation : le script quotidien
  n’appelle ni `git push` ni `gh pr merge`.
- **Unity gelé diverge silencieusement de `sim/`.** Atténuation : c’est
  accepté tant que Unity reste en veille ; le réveil exigera un brief de
  branchement en lecture seule.

## Ce que cet ADR ne décide pas

1. Le contenu des prochaines couches de `sim/` (un brief à la fois).
2. Le plafond mensuel Claude.
3. L’auto-fusion.
