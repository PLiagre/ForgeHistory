---
author: hermes
kind: rapport
created_at: 2026-08-19T16:59:59Z
concerns: briefs 021, 022, 023, pilote ADR-0013
status: REFLECTED_IN_ROADMAP
---
# Bilan des trois lots pilotes ForgePilot — 021, 022 et 023

## Pourquoi ce bilan arrive après la décision qu'il devait précéder

ADR-0013 exigeait un bilan écrit après trois lots réels et **avant** toute
décision d'hébergement. Les trois lots sont achevés depuis la fusion du lot
`023`, le `2026-08-16`. Le VPS a pourtant été mis en service le `2026-08-19`,
avant le présent bilan.

Cet ordre n'a pas été tenu. L'amendement `001` d'ADR-0013 le consigne déjà ; ce
rapport ne transforme pas l'écart en précédent. Il examine le pilote comme si
le choix d'hébergement restait ouvert et peut donc encore proposer de le
retirer.

## Périmètre et sources

Le pilote observé comprend :

| rang | lot | résultat final | fusion |
|---|---|---|---|
| 1 | `021` — fleuves G5 | `PASS` | PR `#107`, 2026-08-15 |
| 2 | `022` — relecture par stdin et `iterate` | `ACCEPT` | PR `#108`, 2026-08-15 |
| 3 | `023` — modèle et effort par rôle | `ACCEPT` | PR `#109`, 2026-08-16 |

Les chiffres ci-dessous viennent des verdicts, des rapports de lot, du registre
de coûts et de l'historique Git. Le `2026-08-19`, les trois portes mécaniques
`verdict_audit.py` rendent `ACCEPT`, et les `26` tests de `control-plane/tests`
passent. Les durées sont des **durées calendaires bornées par les commits** :
elles incluent les attentes et ne sont pas des temps d'exécution machine.

## Qualité des plans et capacité des contrôles à contredire

### Lot 021

Le plan a permis de livrer `157` tronçons, `276` arêtes portant un fleuve, `57`
embouchures, `9/9` fleuves nommés et `6/6` contrôles capables de rougir. Il a
cependant nécessité un amendement et une deuxième passe : la première relecture
a produit `11` constats, notamment un contrôle qualité creux, des compteurs
incapables de détecter une modification et des captures colorant les lacs comme
de la terre.

Le même acteur a participé à la planification et à l'évaluation. Cette entorse à
la séparation des rôles est déclarée dans le verdict ; le contrôle mécanique ne
savait pas la voir.

### Lot 022

Le brief annonçait `4` tests préexistants alors qu'il y en avait `6`. Un
amendement a corrigé ce fait avant la génération. Le lot a ensuite corrigé le
défaut rencontré par le lot `021` : sur un diff réel de `1 239 157` octets, le
plus grand argument est passé de `1 253 092` à `24` octets et
`OSError: [Errno 7]` a disparu.

La relecture a demandé une itération et produit `8` constats, dont un faux parce
qu'elle avait lu l'environnement du dépôt principal au lieu de celui du
worktree. Les `6` points transmis au producteur ont été corrigés ; la
reconstruction ultérieure confirme `6/6` tests rouges avant correction et
`12/12` tests verts après.

### Lot 023

Le plan a donné un lot mesurable : modèle distinct par rôle, `5/5` niveaux
d'effort Claude acceptés, trois chemins de refus vérifiés côté Cursor, ordre de
priorité CLI → rôle → réglage général et préservation des garanties des lots
précédents. La reconstruction confirme `12` tests rouges pertinents sur `14`
ajoutés et `26/26` tests verts à l'état livré.

Ce lot n'a reçu **aucune relecture avant sa fusion** et son verdict n'a été écrit
que le `2026-08-19`. Le réglage livré pour le relecteur est `effort = "low"` ;
il n'est pas issu d'une comparaison qualité/coût. Les verdicts `022` et `023`
existent désormais et sont tous deux `ACCEPT`, mais ils ont jugé du code déjà
fusionné.

## Retouches humaines et itérations

| lot | amendements connus | retours / constats avant état final | itérations de production |
|---|---:|---:|---:|
| `021` | `1` | `11` | `2` |
| `022` | `1` | `8` constats de relecture, puis `6` points transmis | `2` |
| `023` | `0` | `0` avant fusion | `1` |

Total observable : `2` amendements, `3` itérations supplémentaires au-delà
d'une première production pour les deux premiers lots, et aucune relecture
pré-fusion pour le troisième. Le dépôt ne contient pas un compteur fiable du
temps humain effectivement passé ; ce chiffre manque et n'est pas déduit.

## Durée

Bornes issues des commits de début et de fin du travail de chaque lot :

| lot | première trace | dernière trace du lot | durée calendaire bornée |
|---|---|---|---:|
| `021` | 2026-08-15 10:36:29 +02:00 | 2026-08-15 17:47:51 +02:00 | `7 h 11 min 22 s` |
| `022` | 2026-08-15 17:55:54 +02:00 | 2026-08-15 20:59:47 +02:00 | `3 h 03 min 53 s` |
| `023` | 2026-08-15 22:48:51 +02:00 | 2026-08-16 18:45:56 +02:00 | `19 h 57 min 05 s` |

La durée du lot `023` traverse une nuit et inclut des interruptions ; elle ne
mesure donc pas vingt heures de calcul. Les durées exactes par commande
ForgePilot ne sont pas conservées dans une source commune exploitable.

## Coût, plafond et authentification

La seule mesure monétaire complète disponible concerne la session du lot
`022` :

| poste | équivalent tarif API |
|---|---:|
| total | `68.66` USD |
| orchestration | `59.70` USD, soit `87 %` |
| plan | `1.08` USD |
| relecture | `1.96` USD |

Cette mesure porte sur `434` appels et `213 801` jetons de contexte moyens. Le
coût de Cursor n'est pas observable et n'est pas supposé nul. Aucun total
comparable et vérifiable n'est disponible pour les lots `021` et `023` ; le
coût cumulé des trois lots est donc **inconnu**.

Le plafond mensuel Claude a été atteint trois fois entre le `2026-08-13` et le
`2026-08-15`. Pendant le lot `022`, il a interrompu l'Évaluateur et une relecture
de `1,2` Mo estimée à `6.24` USD. Aucun plafond mensuel de pilotage n'est encore
fixé par le propriétaire.

Aucune erreur d'authentification propre aux trois lots n'est consignée dans leurs
preuves. Le contrôle courant `forgepilot doctor --check-auth`, joué le
`2026-08-19`, confirme Claude Code, Cursor et GitHub authentifiés. Cela ne prouve
pas rétroactivement une absence totale d'incident ; cela borne seulement ce qui
est tracé.

## Incidents de sécurité et de gouvernance

Aucune fuite de secret, exécution de code de fork, auto-fusion ou modification
attribuée à un secret compromis n'est consignée pour ces trois lots.

Les incidents de gouvernance, eux, sont réels :

1. le lot `021` a mélangé Planificateur et Évaluateur ;
2. les lots `022` et `023` ont été fusionnés avant leur verdict ;
3. le lot `023` n'a reçu aucune relecture pré-fusion ;
4. le VPS a précédé le bilan qui devait conditionner sa décision ;
5. les tests ajoutés par `022` utilisent `os.sysconf` et rendent la suite
   complète du pilote Linux seulement ; le VPS Linux convient, Windows non ;
6. le plafond Claude a interrompu des contrôles et aucune enveloppe mensuelle
   n'est encore décidée ;
7. la régénération du tableau de bord affiche `REJECT` pour `022` et `023` : son
   parseur retient la dernière chaîne `VERDICT: REJECT` citée dans le récit des
   contrôles antérieurs, au lieu du verdict final `ACCEPT`. Les fichiers de
   verdict restent l'autorité et concluent bien `ACCEPT`. Corriger
   `hermes/dashboard.py` est du code : cela exige un brief, qu'Hermes ne peut pas
   écrire ni faire écrire.

## Proposition

**Conserver ForgePilot, mais ajuster le pilote avant d'étendre son autonomie.**

Les trois lots ont livré des résultats finalement acceptés et les contrôles ont
réellement trouvé des défauts. Le lot `022` a réparé une panne rencontrée en
production, et le lot `023` a retiré le verrou qui empêchait d'ajuster modèle et
effort par rôle. Retirer ForgePilot maintenant ferait perdre ces acquis.

La conservation proposée est conditionnée par quatre ajustements :

1. aucun lot lourd tant que le propriétaire n'a pas fixé une enveloppe mensuelle
   Claude ;
2. verdict Claude avant toute proposition de fusion, sans exception silencieuse ;
3. prochain lot possible : comparer `reviewer low` et `reviewer high` sur une
   mesure qualité/coût ; il exige un brief écrit par Claude, qu'Hermes ne peut
   ni écrire ni faire écrire ;
4. corriger ultérieurement la portabilité Windows des tests `os.sysconf` si le
   pilote doit être rejoué hors du VPS Linux ; cela exige également un brief.

## Clôture et ce qu'elle débloque

Le présent document remplit l'obligation de bilan après les lots `021`, `022` et
`023` et **clôt le pilote de trois lots défini par ADR-0013**. Il ne ratifie pas
rétroactivement l'ordre VPS → bilan et n'autorise ni auto-fusion ni retour au
`full_auto`.

ADR-0015 propose que cette clôture lève la condition temporelle « aucun cron ».
Mais ADR-0015 est encore `proposed` : ses règles ne sont pas en vigueur. En
conséquence, **aucun cron n'est créé** tant que le propriétaire ne l'a pas
tranché. Le veto du propriétaire sur toute fusion reste inchangé.

## Décisions qui attendent le propriétaire

1. fixer l'enveloppe mensuelle Claude et la cadence maximale des lots ;
2. accepter, amender ou rejeter ADR-0015 ;
3. décider si le prochain brief porte sur la comparaison `reviewer low/high`,
   la portabilité Windows, ou une priorité produit F1 ;
4. confirmer ou refuser la proposition de conserver ForgePilot avec les
   ajustements ci-dessus.
