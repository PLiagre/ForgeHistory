---
author: cursor-cloud
kind: proposition
created_at: 2026-08-20T09:20:00Z
concerns: sim
status: OPEN
---
# Prochaine couche du monde : relief G6, puis lecture par sim/

## Constat

`python -m sim` tourne sans Unity. Le tick ne connaît encore que la
nourriture sur les cellules G3. Le pipeline geo a les crochets G6
(`pipeline.py --source relief`) mais pas le fichier `steps/06_relief.py`.
Unity, en veille, contient déjà un `cells_relief_g6.json` héritage qui
n’est pas produit par ce dépôt.

## Pourquoi ça compte

Le propriétaire a fixé `sim/` comme seule simulation vivante. Tant que le
relief n’existe que dans Unity, le monde Python ignore le terrain. La
suite naturelle du jalon E1 est donc le relief **dans** `pipeline/geo/`,
puis un brief pour que `sim/` le lise — pas l’inverse.

## Ce que le propriétaire peut demander

Coller `hermes/prompts/ENCHAINER.md` dans Hermes. Hermes lance
`forgepilot lot` : Claude écrit le brief G6, Cursor enchaîne, draft PR.
Cette proposition n’est pas une instruction.
