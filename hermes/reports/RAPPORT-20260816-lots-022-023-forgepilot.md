---
author: hermes
kind: rapport
created_at: 2026-08-16T09:15:00Z
concerns: brief 022, brief 023, ADR-0014
status: OPEN
---
# Lots 022 et 023 — ForgePilot se répare, et découvre ce qu'il coûte

> **Rapport de rattrapage**, écrit le `2026-08-16` pour des faits du
> `2026-08-15`. Même remarque que dans
> [RAPPORT-20260816-lot-021-fleuves-g5.md](RAPPORT-20260816-lot-021-fleuves-g5.md) :
> la date est celle de l'écriture, pas celle des faits.

Ce rapport reste **OPEN** : trois choses attendent une décision du
propriétaire, listées en fin de document.

## Lot 022 : le pilote répare ce que son premier lot avait cassé

Le lot `021` avait montré que ForgePilot passait le diff à relire en argument de
ligne de commande, ce qui casse au-delà de ~`1` Mo. Le brief `022` a corrigé ça
et ajouté une commande d'itération.

**Le défaut est prouvé corrigé en production**, sur le vrai diff de
`1 239 157` octets qui avait tué le lot `021` :

| fait mesuré | avant | après |
|---|---|---|
| plus grand élément de la ligne de commande | `1 253 092` octets | `24` octets |
| jetons de prompt réellement parvenus à l'API | — | `611 570` |
| l'erreur système `OSError: [Errno 7]` | reproduite | disparue |

Le lot est passé de bout en bout dans le pilote : plan → exécution par Cursor →
publication → relecture → itération → fusion. **PR `#108` fusionnée le
`2026-08-15` à `20:48` UTC.**

Deux premières réelles au passage :

- **La relecture automatique a servi pour de bon** depuis sa réparation. Verdict
  `FAIL`, huit constats — dont **un faux** : il avait lu l'environnement Python
  du dépôt principal au lieu de celui du worktree isolé. Une machine qui relit
  peut se tromper d'endroit ; c'est mesuré, pas supposé.
- **La commande d'itération a servi pour de bon**, celle-là même que le lot
  ajoutait. Les six points du retour ont été corrigés ; le compteur de tests
  rouges avant correction est passé de `2` à `6` sur `6`.

## La dette que ce lot laisse derrière lui

**La PR `#108` a été fusionnée sans verdict d'Évaluateur.**

Le sous-agent chargé de juger est mort sur le plafond mensuel de l'abonnement
Claude avant d'écrire son verdict. C'est une entorse réelle au harnais. Elle est
consignée, pas effacée.

Le travail est intégralement sur `master` (`82a356a`, `1eade7a`), donc un
Évaluateur peut encore juger a posteriori — et il le faut, car le brief `023`
s'appuie explicitement sur « le verdict de référence du lot `022` », qui
n'existe pas.

L'orchestrateur ne peut pas l'écrire lui-même : il a rédigé l'amendement `001`
**et** le retour `001` du même lot.

## Ce que la session a coûté — la mesure qui a tout orienté

`harness/backends/ledger.py tokens`, pour ce seul lot :

| poste | équivalent tarif API |
|---|---|
| **total du lot** | **`68.66` USD** |
| dont orchestration seule | `59.70` USD — **`87` %** |
| dont le plan | `1.08` USD |
| dont la relecture | `1.96` USD |

`434` appels, à `213 801` jetons de contexte en moyenne. Le coût de Cursor n'est
pas observable par ce registre, et n'est **pas** supposé nul.

**Le plafond mensuel de l'abonnement Claude a été atteint pendant la session**,
pour la troisième fois depuis le `2026-08-13`. Il a tué le sous-agent Évaluateur
et une relecture de `1,2` Mo (`6.24` USD à elle seule).

La leçon est simple : ce n'est pas le travail qui coûte, c'est le fait de le
faire faire. Huit dixièmes de la dépense partent dans l'orchestration.

## Trois défauts que seule une reconstruction indépendante a attrapés

Ils valent d'être retenus, parce qu'aucun n'était visible sans refaire la mesure
soi-même :

1. Le brief annonçait `quatre` tests préexistants là où il y en avait `six`
   (corrigé par l'amendement `001` avant toute génération).
2. La relecture automatique a conclu faux sur un point, en lisant le mauvais
   environnement.
3. Le producteur déclarait `2` tests rouges là où il y en avait `5`.

## Lot 023 : écrit, jamais lancé

`harness/queue/briefs/023-forgepilot-modele-et-effort-par-role/` — le réglage du
modèle et de l'effort **par rôle**, avec priorité au drapeau d'appel.

C'est la conséquence directe de la mesure de coût ci-dessus : aujourd'hui le
pilote ne peut pas choisir un modèle moins cher pour un rôle qui n'a pas besoin
du plus fort. Les contrôles préalables sont passés (taille du lot, source unique
d'instruction). **Le lot n'a pas été lancé.**

## ADR-0014 : proposé, pas tranché

`docs/adr/0014-*` — **Hermes déclenche et rend compte, Claude juge, Cursor
exécute**. Il est adossé à la demande
[DEMANDE-20260815-hermes-cerveau-du-pipeline.md](../requests/DEMANDE-20260815-hermes-cerveau-du-pipeline.md).
Un amendement `001` y est joint, qui tranche la question du tableau de bord et
celle des deux mémoires.

**Statut : `proposed`.** Et il est **inapplicable tant que le lot `023` n'est pas
livré** : un pilote qui ne peut pas choisir modèle et effort par rôle ne pilote
rien.

## Ce qui attend le propriétaire

1. **Le plafond mensuel Claude** — trois fois atteint en trois jours. Rien ne
   repart sans une décision là-dessus (attendre le renouvellement, ou changer la
   façon de dépenser).
2. **ADR-0014**, à accepter ou rejeter. Il reste `proposed`.
3. **La dette du lot `022`** : faire juger a posteriori le lot déjà fusionné,
   ou l'assumer comme définitivement non jugé — mais alors le brief `023` perd
   sa référence.

## Ce que ce dossier doit à lui-même

Six demandes d'évolution sont entrées dans `hermes/requests/` ; **un seul
rapport en était sorti**, celui du `2026-08-12`. Ce rapport et celui du lot
`021` comblent une partie du trou. Ils ne le comblent pas tout entier : les lots
`019` et `020` n'ont toujours pas de rapport.
