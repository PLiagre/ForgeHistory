---
author: hermes
kind: rapport
created_at: 2026-08-16T09:00:00Z
concerns: brief 021
status: REFLECTED_IN_ROADMAP
---
# Lot 021 — les fleuves (G5) : livré, accepté, fusionné

> **Rapport de rattrapage.** Il est écrit le `2026-08-16`, alors que le lot a
> été mené le `2026-08-15`. Il n'a pas été écrit sur le moment : entre le
> `2026-08-13` et le `2026-08-15`, le propriétaire s'est adressé directement à
> Claude Code et ce dossier n'a rien reçu — cinq lots (`019` à `023`) sans un
> seul rapport. Le rattrapage porte sur les lots `021` à `023` ; les lots `019`
> et `020` restent sans rapport et leur trace vit dans l'historique git et dans
> `HANDOFF.md`. La date du fichier est celle de l'écriture, pas celle des faits,
> pour ne pas fabriquer un passé plus propre qu'il ne fut.

## Ce que le lot a livré

Le brief `021` demandait les fleuves du pipeline géographique — l'étape `G5`,
après le littoral, les cellules `G3` et l'adjacence maritime `G4`. Il a livré
`pipeline/geo/steps/05_rivers.py` et ses artefacts.

Les chiffres, reconstruits par l'Évaluateur et non repris du manifeste du
producteur :

| fait mesuré | valeur |
|---|---|
| tronçons de fleuve | `157` (`36` + `92` + `29`) |
| arêtes du graphe portant un fleuve | `276` (`72` + `195` + `9`) |
| embouchures | `57` |
| fleuves nommés reconnus | `9` sur `9` attendus |
| contrôles qualité verts | `6` sur `6` |
| contrôles capables de rougir, prouvé | `6` sur `6` |
| déterminisme (deux passes) | `8` paires d'empreintes égales sur `8` |

Le lot ne touche ni `adjacency_g4.json`, ni `constants.py`, ni les neuf fichiers
partagés du pipeline.

## Comment ça s'est passé

Deux passes, pas une.

1. **Passe 1 : REJECT.** La relecture a produit `onze` constats. Les plus
   sérieux n'étaient pas des détails de forme : un contrôle qualité qui ne
   pouvait pas rougir (il déclarait vérifier quelque chose sans le calculer),
   des compteurs « fichier intact » incapables de détecter une modification, et
   des captures d'écran qui peignaient les lacs en couleur de terre.
2. **Passe 2 : PASS.** Les onze points corrigés. Sur le point n° `5`, le
   producteur est allé au-delà de ce qui était demandé : il a ajouté de
   lui-même une preuve montrant que le compteur voit désormais une
   modification.

**PR `#107` fusionnée le `2026-08-15` à `15:49` UTC.**

## L'entorse, déclarée et non masquée

Le verdict du lot porte en tête une note de transparence qu'il faut lire :
**le même acteur a tenu le rôle de Planificateur et celui d'Évaluateur** sur ce
lot. Il a orchestré la rédaction du brief et écrit l'amendement `001`, puis il a
écrit le verdict et le feedback. La règle du harnais — « jamais le même agent
dans la même passe » — n'a pas été tenue.

Le contrôle mécanique n'a rien vu : il ne compare que Producteur ↔ Évaluateur,
jamais Planificateur ↔ Évaluateur. L'entorse a donc été écrite en clair dans le
verdict plutôt que laissée à un contrôle aveugle. Le propriétaire en a été
averti et a choisi cette voie le `2026-08-15` pour clore le lot dans la journée.

Ce qui atténue sans annuler : les onze constats venaient d'une relecture
indépendante du diff de la PR, menée dans une invocation séparée.

## Ce que ce lot a cassé, et pourquoi ça compte

Le lot `021` est le **premier lot réel** passé par le pilote ForgePilot
(ADR-0013). Il l'a cassé.

Le diff produit faisait `1 239 157` octets. Le pilote le passait à la relecture
en **argument de ligne de commande** — ce que le système d'exploitation refuse
au-delà d'une certaine taille : `OSError: [Errno 7]`. La relecture automatique
n'a donc pas pu tourner sur son premier vrai lot.

C'est ce défaut qui a fait naître le brief `022` le jour même. Voir le rapport
[RAPPORT-20260816-lots-022-023-forgepilot.md](RAPPORT-20260816-lots-022-023-forgepilot.md).

## Ce qui reste ouvert côté carte

`F1` n'est pas close. Après les fleuves `G5` : le relief `G6`, le climat, les
ressources. Le recalibrage des bornes de semis des zones de mer, signalé par le
lot `019`, est toujours un constat ouvert.
