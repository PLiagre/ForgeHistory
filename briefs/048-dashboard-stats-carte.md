# Brief 048 — Tableau de bord : stats mêlées à la carte

## But

Le regard mince devient un **tableau de bord** : la carte reste au centre, un
bandeau de KPI et un panneau de distribution lisent la photographie. On voit
d’un coup le tick **et** le jour de l’année, les totaux du monde déjà
photographiés, et la couche active (min, max, histogramme, totaux par
province). Rien n’est recalculé par le moteur.

Ce qui rend ce lot caduc : si le viewer affiche déjà, sur un snapshot réel,
le tick distinct du jour de l’année, un bandeau de totaux dérivés des
cellules, et une distribution de la couche active. La commande qui le
mesure :

```bash
python3 -m sim --ticks 0 --seed 0 --snapshot-json /tmp/monde.json
python3 -m viewer --snapshot /tmp/monde.json
grep -n "kpi-\|jour_de_tick\|agregats_monde" viewer/static/index.html viewer/static/app.js viewer/snapshot_loader.py
```

## Règle du monde

Aucun fondement dans [`sim/MODELE.md`](../sim/MODELE.md) : **ce lot ne
change aucun nombre du monde.** C’est de la présentation. Elle LIT la
photographie, elle ne décide jamais (principe 1, mode de défaillance n° 4).

Elle s’appuie sur ce que le snapshot porte déjà, décrit dans
`sim/MODELE.md` § « La base de temps » (le rang du jour se dérive du
numéro de tick **dans le moteur**, et s’exporte sous `tick` et
`jour_de_tick` — le regard ne refait pas le modulo) ; § « Le panier de
marchandises » et « Absence contre zéro » (sentinelle `-1`, zéro mesuré,
clé absente) ; § « Ce que veut dire affamée » (`hunger_ticks`) ; et § « La
province dérivée et ses centres » (vue déjà jointe à chaque cellule du
snapshot). Si l’une de ces sections a changé depuis, la relire avant de
lancer.

**Pas de niveau de fidélité** : ce lot ne touche pas au monde.

### Ce que le regard lit, et ce qu’il refuse

Chaque chiffre du bandeau est un **agrégat des cellules déjà présentes**
dans le JSON, ou un champ déjà écrit en tête du document (`tick`,
`jour_de_tick`, `seed`). Un champ manquant s’affiche **absent** ; il n’est
pas remplacé par zéro, ni recalculé (règle 10). En particulier
`kg_transportes` n’est **pas** dans le snapshot aujourd’hui : le bandeau
le dit, il n’invente pas un flux en resimulant le commerce.

Le défaut connu à corriger : l’UI actuelle n’affiche que `jour_de_tick`
(le rang dans l’année, un modulo). Au tick 365 ça ressemble au jour 0.
Le bandeau montre les **deux** champs, chacun pour soi, et ne dérive pas
l’un de l’autre.

Un échantillon sans cellule **échoue**. Un zéro de population ou de stock
est une mesure réelle.

## Périmètre

En écriture :

- `briefs/048-dashboard-stats-carte.md` — ce brief
- `ROADMAP.md` — la fiche 048 du registre, et rien d’autre de ce fichier
- `viewer/snapshot_loader.py` — agrégats lus du snapshot
- `viewer/server.py` — servir `/dashboard.json`
- `viewer/static/index.html`
- `viewer/static/style.css`
- `viewer/static/app.js`
- `viewer/README.md`
- `viewer/tests/test_viewer_v0b.py` — **ajouter** des cas, sans modifier
  les contrôles déjà verts

Tout autre chemin est interdit, nommément : `sim/`, `data/world-1400.json`,
`data/province-centres-1400.json`, `viewer/svg_proof.py`,
`viewer/classify.py`, les autres briefs, `VISION.md`, `AGENTS.md`,
`sim/MODELE.md`.

## Conditions de succès

### SC1 — Un échantillon vide échoue

`agregats_monde` sur un document sans cellule lève une erreur. Il ne rend
pas des zéros.

```bash
python3 -m pytest viewer/tests/test_viewer_v0b.py::test_agregats_echantillon_vide_echoue -q
```

**Rouge d’abord** : la fonction n’existe pas sur `master`.

### SC2 — Les totaux dérivent du snapshot, pas d’une cible

Sur un snapshot réel (`export_snapshot` du monde chargé), la population
du bandeau **égale** la somme des `population` des cellules lues ; le
nombre de cellules **égale** `len(cells)` ; les cellules affamées
**égalent** le nombre de cellules dont `hunger_ticks > 0` ; le stock
nourriture **égale** la somme des entrées `nourriture` du panier qui sont
une mesure (pas `-1`, pas une clé absente). Le dénominateur de chaque
somme est le nombre de cellules réellement lues ; un échantillon vide
échoue déjà en SC1.

```bash
python3 -m pytest viewer/tests/test_viewer_v0b.py::test_agregats_monde_derivent_du_snapshot -q
```

### SC3 — L’absence ne se déguise pas en zéro

Un snapshot sans clé `kg_transportes` rend `{etat: "absent"}` pour ce
KPI, **pas** `0`. Une sentinelle `-1` sur une entrée de panier n’entre
pas dans la somme du stock. Une cellule sans `province` n’invente pas de
nom de province ; si **aucune** cellule n’en porte, les totaux par
province sont `absent`.

```bash
python3 -m pytest viewer/tests/test_viewer_v0b.py::test_absence_declaree_pas_inventee -q
```

### SC4 — Tick et jour de l’année sont deux champs

Le dashboard expose `tick` et `jour_de_tick` **séparément**. Sur un
snapshot au tick 365, le tick lu est 365 et le jour lu est celui que le
document porte (`jour_de_tick`), sans que le regard recalcule un modulo.
Si `jour_de_tick` manque, le jour s’affiche absent ; le tick reste le
tick.

```bash
python3 -m pytest viewer/tests/test_viewer_v0b.py::test_tick_et_jour_sont_distincts -q
```

Le HTML du regard porte un bandeau KPI (`#kpis`) et les identifiants
`kpi-tick`, `kpi-jour`, `kpi-population`, `kpi-cellules`,
`kpi-affamees`, `kpi-stock`, `kpi-transport`, plus la distribution de
couche (`#layer-min`, `#layer-max`, `#histogram`, `#provinces`).

```bash
python3 -m pytest viewer/tests/test_viewer_v0b.py::test_dashboard_html_porte_les_kpis -q
```

### SC5 — La couche active a min, max et un histogramme dérivés

Pour la couche `population` d’un snapshot réel, min et max **égalent**
le min et le max des populations mesurées. L’histogramme a des effectifs
dont la somme **égale** le nombre de valeurs mesurées. Un échantillon
sans aucune valeur mesurée pour la couche rend l’histogramme `absent`.

```bash
python3 -m pytest viewer/tests/test_viewer_v0b.py::test_agregats_couche_derivent_du_snapshot -q
```

### SC6 — Le regard sert les agrégats, et la suite reste verte

`/dashboard.json` rend exactement `construire_dashboard(document)` du
snapshot servi. Aucun test déjà présent de `viewer/tests/test_viewer_v0b.py`
n’est modifié. Les suites restent vertes :

```bash
python3 -m pytest viewer/tests/ -q
python3 -m pytest sim/tests/ -q
```

## Hors périmètre

- ajouter `kg_transportes` au schéma du snapshot (ça serait un lot `sim/`)
- la comparaison temporelle avancée (`--compare`, export PDF, courbes
  d’un tick sur l’autre)
- un second moteur graphique : la carte reste le canvas existant
- recalculer commerce, production, saison ou province
- `data/world-1400.json`, le moteur, `sim/MODELE.md`
- modifier un test existant pour le faire passer
