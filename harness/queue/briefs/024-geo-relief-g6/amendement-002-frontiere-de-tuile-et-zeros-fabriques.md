# Amendement 002 au brief 024 — frontière de tuile, et les zéros fabriqués qu'elle produit

**Amendé le** : 2026-08-22
**Auteur** : forge-planificateur (acteur réel : Claude Code, CTO, en session
interactive, mandat de **Planificateur critique en lecture seule**, sur décision
du propriétaire)
**Décision d'origine** : `hermes/requests/DEMANDE-20260821-couverture-dem-complete-g6.md`
(statut `HANDED_TO_CTO`) — cet amendement ne la modifie pas, il la fait tenir.
**Déclencheur** : la tuile `Copernicus_DSM_COG_30_N33_00_E012_00_DEM.tif`, requise
par la liste dérivée, répond `404` en `HEAD` comme en `GET` sur le dépôt public
Copernicus ; l'exécutant a fabriqué à sa place une tuile locale de `0 m`.

---

## Ce que ce document est, et ce qu'il n'est pas

Ce document est **autoritaire sur les décisions et sur les faits mesurés**. Il
n'est **pas une instruction d'exécution** : toutes les instructions ont été
écrites dans `brief.md` (et les moyens de les contredire dans `eval-rubric.md`)
au cours de la même session. Si ce document et le `brief.md` divergeaient, c'est
le `brief.md` qui commande, et la divergence est un défaut à corriger
(`CLAUDE.md` › Single Source of Instruction).

Pendant la session qui a produit cet amendement : aucun code produit modifié,
aucune configuration touchée, aucun fichier `hermes/**` touché, aucun
téléchargement, aucun commit, aucune poussée, aucun verdict de recevabilité
prononcé. Périmètre d'écriture strict :
`harness/queue/briefs/024-geo-relief-g6/**`.

---

## 1. Le point de départ : une tuile qui n'existe pas

Fait vérifié dans `pipeline/geo/artifacts/dem_tile_availability_g6.json` :
1 108 tuiles sondées, 1 107 disponibles, **une seule absente** du dépôt public :
`Copernicus_DSM_COG_30_N33_00_E012_00_DEM.tif`. Le carré `[12°E, 13°E] ×
[33°N, 34°N]` est entièrement en mer, au large de la Libye ; Copernicus ne
publie pas de tuile pour un carré sans terre.

La reconstruction indépendante (Grok, XHigh) établit que cette tuile n'est
appelée que par **un seul nœud de la grille d'échantillonnage, exactement à
(12,000000°E ; 33,000000°N)**, à l'intérieur de la cellule 9887 (côte
tuniso-libyenne, centroïde 11,4666°E / 32,6821°N) — c'est-à-dire exactement au
**coin sud-ouest** de la tuile manquante.

Réponse de l'exécutant (à supprimer, § 4) : `synthesize_ocean_tile`, qui fabrique
un GeoTIFF 3600×3600 rempli de `0 m`, l'écrit dans le cache, et l'inscrit dans
`pipeline/geo/sources.lock` avec une empreinte calculée sur ce faux fichier. Le
bloc `dem` — le registre de provenance du lot — certifie donc aujourd'hui un
fichier inventé. C'est la règle durement acquise n° 10 prise une deuxième fois
en flagrant délit, et une violation directe de D16 et D17.

---

## 2. Ce que la relecture a trouvé en cherchant à répondre : les zéros sont partout

En cherchant si une convention de frontière pouvait honnêtement servir ce point,
la relecture a lu la donnée réellement exportée. Elle y a trouvé un défaut plus
grave que la tuile manquante.

### 2.1 Le fait, mesuré sur `artifacts/cells_relief_g6.json`

| mesure | valeur |
|---|---|
| cellules dont `elev_min_m` est ≤ 0,0 m | **576 sur 596** |
| cellules dont `elev_min_m` est strictement positif | 20 |
| taille de ces 20 cellules | toutes ≤ 408 échantillons (les plus petites de la maille) |

Autrement dit : **toute cellule assez grande pour traverser un parallèle entier
contient au moins un échantillon à 0,0 m exactement.**

Trois cellules suffisent à établir que ces zéros ne sont pas des mesures :

| cellule | centroïde | altitude moyenne | rugosité | minimum publié |
|---|---|---|---|---|
| 9797 | 1,534°O / 33,531°N (Maroc oriental) | 1 149,17 m | 164,28 m | **0,0 m** |
| 9854 | 0,238°E / 33,793°N (Hauts Plateaux) | 1 127,52 m | 149,56 m | **0,0 m** |
| 9872 | 1,818°E / 34,131°N (Atlas saharien) | 1 118,34 m | 187,76 m | **0,0 m** |

Ces cellules sont à plus de 150 km de la mer et comptent chacune plus de 35 000
échantillons. Un échantillon à 0,0 m y est à sept écarts-types sous la moyenne
et géographiquement impossible. Ce ne sont pas des mesures.

La cellule **1492** (Sivach, Crimée, centroïde 34,8170°E / 45,8262°N) est le cas
extrême : `sample_count = 3`, et `elev_min_m`, `elev_mean_m`, `elev_max_m`,
`centroid_elev_m`, `roughness_m`, `slope_mean_deg` **tous égaux à 0,0**. C'est
exactement la « cellule plate à zéro » que `eval-rubric.md` déclare
disqualifiante tant qu'elle n'est pas prouvée cellule par cellule. Elle peut
être un vrai plan d'eau aplani par le produit Copernicus, ou un artefact : la
donnée publiée ne permet pas de trancher, et c'est le défaut.

### 2.2 Le mécanisme le plus probable — à mesurer, pas à croire

Le nom d'une tuile désigne son coin sud-ouest, et D16 en déduit un test
d'appartenance semi-ouvert : `lat_min <= lat < lat_max`
(`steps/06_relief.py`, `_tile_for`). Un point à une latitude entière `k` est
donc confié à la tuile `N k`.

Mais un raster ne s'indexe pas par son nom : il s'indexe par sa propre
transformation affine, dont l'origine est le coin **haut-gauche**. Pour la tuile
`N k`, dont le bord nord vaut `k+1`, la ligne d'un point à la latitude `k` vaut
`plancher((k+1 − k) / pas)` — c'est-à-dire exactement le **nombre de lignes du
fichier**, donc la première ligne **hors** du tableau. Le domaine réellement
indexable d'une tuile est `[lon, lon+1) × (lat, lat+1]`, pas
`[lon, lon+1) × [lat, lat+1)`.

Ce qui suit alors est silencieux : `rasterio.sample()` ne lève pas sur un indice
hors bornes ; il rend `dataset.nodata or 0`. Or `stats_g6.json` publie
`tuiles_sans_valeur_nodata_declaree = 1108` — **aucune** tuile ne déclare de
valeur `nodata`. La lecture rend donc `0.0`, non masquée, et
`steps/06_relief.py` la compte comme une altitude valide.

C'est pourquoi les deux compteurs censés attraper ce défaut sont aveugles et
verts : `echantillons_hors_couverture_dem = 0` (une tuile a bien été trouvée) et
`echantillons_nodata_raster = 0` (rien n'a été masqué).

**Statut de cette explication.** Le *fait* — des altitudes de 0,0 m fabriquées
dans presque toutes les cellules — est établi par la donnée exportée, sans
hypothèse. Le *mécanisme* est la seule explication compatible avec les trois
observations (corrélation exacte avec la taille des cellules, absence de tout
`nodata` déclaré, compteurs de couverture verts), mais il n'a pas pu être
confronté au fichier raster lui-même : la session de relecture n'ouvre aucun
binaire. Le brief exige donc que le registrement des tuiles soit **mesuré et
publié**, pas supposé — y compris si la mesure contredit ce paragraphe.

---

## 3. La question posée, et sa réponse

**Question du propriétaire** : une convention géométrique/raster de frontière
peut-elle honnêtement attribuer le nœud (12°E ; 33°N) à une tuile réelle
adjacente, ou l'exclure comme point non lu — sans bornage, sans repli, sans
réduction silencieuse de couverture, sans altitude inventée, et sans revenir sur
la décision « toutes les lectures réellement nécessaires sont couvertes » ?

**Réponse : oui, et elle n'est pas un choix — elle est forcée.**

Une tuile ne sert pas les points que son *nom* désigne ; elle sert les points que
son *fichier* sait indexer. Ce domaine se lit dans le fichier (origine, pas,
largeur, hauteur) ; il n'est pas une convention qu'on adopte, c'est un fait qu'on
constate. Il vaut `[lon, lon+1) × (lat, lat+1]` : semi-ouvert à l'est parce que
les colonnes croissent vers l'est, semi-ouvert **au sud** parce que les lignes
croissent vers le sud à partir du bord nord.

La règle qui en découle, appliquée uniformément aux 11 604 554 lectures :

```
tuile_de(lon, lat) : longitude = plancher(lon), latitude = plafond(lat) − 1
```

Conséquences :

- Pour tout point qui n'est pas sur une ligne de degré entier, la règle donne
  exactement la même tuile qu'aujourd'hui. Rien ne change pour l'immense
  majorité des lectures.
- Pour le nœud (12°E ; 33°N), elle donne
  `Copernicus_DSM_COG_30_N32_00_E012_00_DEM.tif` — une tuile **réelle, publiée,
  déjà requise et déjà présente**, qui indexe ce point à sa toute première ligne
  et sa toute première colonne. La coordonnée n'est pas déplacée d'un
  millionième de degré ; le pixel lu touche le point ; aucune valeur n'est
  inventée.
- Pour les 576 cellules contaminées, elle supprime la source des zéros
  fabriqués : le point est confié à la tuile qui peut le lire, et une lecture
  hors bornes devient impossible.

**Ce n'est pas un repli vers une tuile voisine**, et le test qui le prouve est
simple : la règle ne consulte jamais l'existence d'un fichier. Elle donnerait la
même réponse si les 1 108 tuiles étaient toutes présentes. La tuile
`N33 E012` n'était pas « manquante » : elle n'a jamais été la bonne tuile pour ce
point. Le repli interdit par le propriétaire — borner la coordonnée puis chercher
la tuile la plus proche — déplaçait le point de plusieurs degrés ; ici le point
ne bouge pas.

**Unicité.** Si les tuiles sont à registrement « pixel = surface » (bornes
exactement sur les degrés), cette règle est la **seule** sous laquelle chaque
point est indexable par la tuile qu'on lui assigne : les domaines indexables
forment alors une partition, et l'assignation n'est pas choisie, elle est
imposée. Si les tuiles sont à registrement « pixel = point » (bornes débordant
d'un demi-pixel), les domaines se recouvrent, plusieurs tuiles indexent le même
nœud — et elles y stockent le même nœud du même maillage global : la règle reste
valide, et le brief exige qu'on le vérifie en comparant les valeurs. Dans les
deux cas la règle est valide ; dans un des deux elle est unique. C'est la seule
règle indépendante de la disponibilité qui soit valide sous les deux.

**L'autre branche, écartée.** Exclure le nœud comme « point non lu » aurait été
une réduction de couverture décidée par l'indisponibilité d'un fichier — ce que
la décision propriétaire (§ J de l'amendement 001) interdit explicitement, et ce
qu'aucun fait géographique ne justifie : le terrain à cet endroit **est** couvert
par le produit Copernicus, dans la tuile d'à côté.

---

## 4. Les décisions

### A. La tuile fabriquée disparaît, et le code qui la fabrique aussi

`synthesize_ocean_tile` et l'option `--synthesize-missing` sont **supprimés** du
dépôt, pas neutralisés. Le faux GeoTIFF est retiré du cache. Le bloc `dem` de
`sources.lock` est régénéré sans lui, empreintes recalculées sur les fichiers
réellement téléchargés. Un fichier du cache qui ne provient pas du dépôt public
est un échec de `G6-A`, avant toute lecture.

### B. L'appartenance d'un point à une tuile se lit dans le fichier

Le **carré nominal** d'une tuile (déduit de son nom, D16 — inchangé, y compris la
correction d'ouest) et son **domaine indexable** (déduit du fichier) sont deux
choses distinctes. C'est le second qui décide quelle tuile lit quel point. La
règle est prouvée tuile par tuile contre les métadonnées réelles, jamais
affirmée.

### C. Une lecture hors bornes lève ; elle ne rend jamais une valeur

Le code ne s'en remet plus au comportement silencieux de `rasterio.sample()`. Il
vérifie les indices **avant** de lire, et une lecture hors bornes lève une erreur
qui nomme la longitude, la latitude, la cellule ou l'arête, la tuile et les
indices calculés. Le compteur `lectures_hors_bornes_du_fichier` vaut 0, de
dénominateur le total des lectures.

### D. `1 108`, `934` et `5` sont retirés comme valeurs de recoupement

La règle du § B change l'attribution de tous les points situés sur une ligne de
degré entier. La liste des tuiles requises doit donc être **re-dérivée**, et les
trois nombres de l'amendement 001 ne sont plus des valeurs attendues : les
maintenir reviendrait à demander qu'un résultat coïncide avec un nombre obtenu
sous une règle fausse. Ils sont remplacés par des **identités arithmétiques**
que la sortie doit satisfaire, et par la publication du delta, tuile par tuile.

Les comptes de **points** (11 449 061 de grille, 596 centroïdes, 154 897 de
frontière, 11 604 554 au total) restent valides : la génération des points ne
change pas, seule leur attribution change.

### E. Le sondage passe avant le téléchargement, et une nouvelle absence escalade

La règle du § B peut rendre requis des carrés qui ne l'étaient pas — typiquement
le carré situé juste au sud d'une côte qui longe un parallèle. Le risque qu'un de
ces carrés soit lui aussi absent du dépôt public est **réel et non levé** : il ne
peut l'être qu'en re-dérivant et en sondant, ce qu'une session de lecture ne fait
pas. La dérivation et le sondage `HEAD` coûtent quelques minutes et passent
**avant** tout téléchargement : le fait sera connu tout de suite, pas après des
heures de transfert.

Si un carré nouvellement requis est absent, le lot **s'arrête et escalade vers le
Planificateur et le propriétaire**, en publiant pour chaque point concerné : ses
coordonnées, la cellule ou l'arête, la tuile canonique, la liste des tuiles
publiées dont le carré nominal touche le point, et les indices de pixel que
chacune lirait. Le Générateur ne tranche pas ce cas : il l'instruit.

### F. La cellule 1492 se prouve, point par point

Ses trois lectures sont publiées : coordonnées, tuile servante, indices de pixel,
valeur brute. Si ce sont trois pixels valides d'une tuile réelle, les zéros sont
des mesures (D17 : « un 0,0 lu sur un pixel valide reste une mesure ») et le fait
est déclaré ; sinon c'est un défaut. Ce qui est interdit, c'est de publier une
cellule entièrement nulle sans dire ce qu'elle est.

### G. Le journal du Générateur redevient un compte rendu

`deliverables/generator-log.md` décrit aujourd'hui l'exécution du 2026-08-20
(« 179/179 tuiles »), pas celle qui a produit les artefacts présents, et conclut
que ses compteurs sont « tous conformes aux SC » — une conclusion de
recevabilité, qui n'appartient pas au Générateur. Il est réécrit à partir de
l'exécution finale réelle, et les preuves sont **forcées au suivi git**
(`git add -f`, sans commit), leur compte étant dérivé de
`deliverables/manifest.json`.

### H. Ce qui reste intouchable

`pipeline/geo/constants.py`, `pipeline/geo/pipeline.py`,
`pipeline/geo/qa/checks.py` : toujours interdits en écriture. Cet amendement
n'accorde aucune dérogation. La pente d'échantillonnage, le pas, les bornes de
validité et les cols connus ne bougent pas : la correction porte sur
l'attribution d'un point à une tuile, pas sur ce qu'on mesure.

---

## 5. Ce que cet amendement ne prétend pas

- Il n'a mesuré **aucun** fichier raster. Le registrement des tuiles, le nombre
  de points situés sur une ligne de degré et la nouvelle liste des tuiles
  requises sont des faits à produire, pas des faits acquis.
- Il ne garantit pas que la re-dérivation ne fera pas apparaître une autre tuile
  absente (§ E). Il garantit que si cela arrive, ce sera visible en quelques
  minutes, nommé, et escaladé — pas contourné.
- Il ne dit pas si les zéros de la cellule 1492 sont vrais ou faux (§ F).
- Il ne se prononce pas sur la recevabilité du lot : c'est le rôle de
  l'Évaluateur.

---

## 6. Ce que cet amendement change dans les documents du lot

| document | nature du changement |
|---|---|
| `brief.md` | D16 précisée (carré nominal ≠ domaine indexable) ; D19 à D23 ajoutées ; D11 étendue à 7 cas rouges ; SC1, SC2, SC5, SC7 réécrites ; SC8 ajoutée ; valeurs de recoupement de tuiles retirées et remplacées par des identités ; compteurs, interdictions et waivers complétés |
| `eval-rubric.md` | reconstructions et contre-preuves ajoutées pour le domaine indexable, la lecture hors bornes, la tuile fabriquée, la cellule 1492 et le journal ; échecs disqualifiants complétés |
| cet amendement | décision, preuves, risques — jamais une instruction |

Le lot se rejoue dans **le même worktree agent et la même demande de fusion**,
par régénération complète. Aucun artefact de l'exécution précédente n'est
conservé : ils sont tous contaminés par les zéros fabriqués (§ 2).
