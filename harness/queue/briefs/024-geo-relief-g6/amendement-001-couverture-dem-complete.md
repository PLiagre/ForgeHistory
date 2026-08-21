# Amendement 001 au brief 024 — compléter la couverture DEM du relief G6

**Amendé le** : 2026-08-21T17:10:00Z
**Auteur** : forge-planificateur (acteur réel : Claude Code, CTO, en session
interactive, mandat de Planificateur critique en lecture seule)
**Décision d'origine** : `hermes/requests/DEMANDE-20260821-couverture-dem-complete-g6.md`
(statut `HANDED_TO_CTO`, décision du propriétaire du 2026-08-21)
**Déclencheur** : verdict `FAIL` de la relecture de la PR #122, consigné dans
`.forgepilot/runs/20260820T220349Z-reviewer/result.json` (findings F1 à F5)
**Mesures autoritaires** : `/home/hermes/g6-required-tiles-report.json`, produit
en rejouant le code exact de génération des points, sans ouvrir une seule tuile
DEM

---

## Ce que ce document est, et ce qu'il n'est pas

Ce document est **autoritaire sur les décisions et sur les faits mesurés** : il
dit ce que le propriétaire a tranché, pourquoi, et sur quels nombres.

Il **n'est pas une instruction d'exécution**. Le dépôt n'admet qu'un seul
document qui dit à un agent quoi faire pour un lot : le `brief.md` du lot
(`CLAUDE.md` › Single Source of Instruction, vérifié mécaniquement par
`harness/tests/test_single_source_of_instruction.py`). Toutes les décisions
ci-dessous ont donc été **écrites dans `brief.md` et dans `eval-rubric.md`** au
cours de la même session. Si ce document et le `brief.md` divergeaient un jour,
c'est le `brief.md` qui commande, et la divergence est un défaut à corriger.

Rien n'a été téléchargé, exécuté en écriture, committé ni poussé pendant la
session qui a produit cet amendement. Aucun code n'a été modifié : le périmètre
d'écriture était strictement `harness/queue/briefs/024-geo-relief-g6/**`.

---

## 1. Le défaut, en une phrase

Le lot livré publiait des altitudes de `0,0 m` pour toutes les coordonnées
situées hors de l'emprise des 179 tuiles déclarées, parce que le code bornait la
coordonnée sur l'emprise disponible puis retombait sur la tuile la plus proche.
Ce ne sont pas des mesures : ce sont des valeurs fabriquées par un repli
silencieux, et elles ont produit de fausses barrières en plaine et l'absence de
toute barrière pyrénéenne.

C'est exactement la règle durement acquise n° 10 (« quand la donnée manque,
l'agent l'invente silencieusement par défaut — l'absence doit être déclarable et
le code doit refuser de deviner ») prise en flagrant délit.

---

## 2. Les mesures qui font foi

Reconstruites par exécution du code exact de génération des points de G6, sans
aucune lecture de raster. Elles sont **la référence** de cet amendement.

### 2.1 Tuiles

| grandeur | valeur |
|---|---|
| tuiles 1°×1° réellement requises par les lectures G6 | 1 108 |
| tuiles présentes dans le bloc `dem` actuel de `sources.lock` | 179 |
| parmi elles, réellement utiles | 174 |
| tuiles manquantes à ajouter | 934 |
| tuiles excédentaires à retirer | 5 |

Contrôle d'addition : 174 + 934 = 1 108, et 174 + 5 = 179. Les deux tiennent.

### 2.2 Points lus

| famille de points | nombre |
|---|---|
| grille régulière dans les polygones de cellules | 11 449 061 |
| centroïdes de cellules | 596 |
| échantillons de frontière partagée | 154 897 |
| **total des lectures d'altitude** | **11 604 554** |

### 2.3 Emprise réelle des points

| borne | valeur |
|---|---|
| longitude minimale | −10,475 |
| longitude maximale | 34,819 304 810 285 41 |
| latitude minimale | 29,704 867 323 841 14 |
| latitude maximale | 61,558 333 333 333 |

L'emprise du bloc `dem` actuel s'arrête à W007→E008 et N42→N55. L'écart entre
les deux est la totalité du défaut.

### 2.4 Volume et disque

Le bloc `dem` actuel pèse 644 127 181 octets pour 179 tuiles. En extrapolant à
partir de la distribution de taille de ces 179 tuiles :

| estimation du volume **supplémentaire** (934 tuiles) | octets |
|---|---|
| médiane des tuiles connues × 934 | 3 594 423 346 (≈ 3,6 Go) |
| plus grande tuile connue × 934 | 5 337 717 534 (≈ 5,3 Go) |

Disque libre mesuré sur cette machine : **85 Go**. Le disque n'est pas la
contrainte.

**Honnêteté sur cette estimation** : les 179 tuiles connues sont toutes
européennes et de moyenne latitude. Les 934 nouvelles couvrent de N29 (Sahara,
Égypte) à N61 (Scandinavie) et une large part d'océan et de désert, dont la
compression n'est pas celle d'un relief tempéré. Ces deux nombres sont un ordre
de grandeur, pas une prévision. Le volume réel est un fait à mesurer et à
publier, jamais à supposer.

---

## 3. Les décisions du propriétaire, tranchées ici

### A. `sources.lock` est ouvert à l'écriture, pour le seul bloc `dem`

Le brief interdisait toute écriture dans `pipeline/geo/sources.lock`. Cette
interdiction est **levée pour le seul objet de premier niveau `dem`**. Tous les
autres objets de premier niveau (`files`, `geonames_cities500`,
`layer_coverage`, `licence`, `source_set`) restent octet pour octet identiques,
et le sous-objet `dem.licence` est conservé mot pour mot : l'attribution
Copernicus est une obligation légale, pas un détail de format.

La nouvelle liste de tuiles et leurs empreintes sont produites par un script
committé et rejouable. **Aucune valeur hexadécimale n'est recopiée à la main**,
nulle part : chaque `sha256` du bloc est recalculé à partir du fichier
réellement présent dans le cache local (règle durement acquise n° 12).

### B. La liste des tuiles requises se dérive, elle ne se déclare pas

Un outil committé dérive la liste des tuiles requises en appelant **les mêmes
fonctions de génération de points** que le module de relief — pas une seconde
implémentation qui pourrait diverger. Il ne lit aucun raster.

Les nombres du § 2 sont la valeur attendue. Une reconstruction mécanique qui
donnerait un autre nombre est une **escalade**, avec la sortie du script qui le
prouve. Aucun nombre de cet amendement ne s'ajuste en silence pour faire
coïncider un résultat.

### C. Plus aucun dénominateur `179`

Partout où le brief comptait « sur 179 », il compte désormais sur la longueur
réellement lue du bloc de tuiles de `sources.lock`. Le nombre 179 n'apparaît
plus comme dénominateur nulle part.

### D. La convention des bornes de tuile est corrigée

Le nom d'une tuile Copernicus désigne son **coin sud-ouest signé**, et la tuile
couvre un degré vers le nord et vers l'est à partir de ce coin. Donc `W001`
couvre les longitudes `[−1, 0)`, `E000` couvre `[0, 1)`, `N42` couvre les
latitudes `[42, 43)`.

Le code livré appliquait cette règle correctement à l'est et au nord, et
faussement à l'ouest : il faisait couvrir `[−2, −1)` à `W001`, soit un degré de
décalage sur tout l'ouest de la carte.

Cette convention n'est pas seulement écrite : elle est **prouvée tuile par
tuile** en comparant les bornes déduites du nom aux bornes réelles lues dans les
métadonnées du fichier COG. Une convention qu'on affirme est une convention
qu'on peut se tromper à relire ; une convention qu'on confronte au raster ne
ment pas.

### E. Le bornage et le repli vers la tuile voisine disparaissent

Le bornage de coordonnée et la recherche de « la tuile la plus proche » sont
supprimés du code, pas désactivés par un drapeau. Une coordonnée sans tuile qui
la contienne fait **échouer la lecture**, en nommant la longitude, la latitude,
l'identifiant de cellule (ou d'arête) concerné et le nom de la tuile qui aurait
été nécessaire.

Jamais un `0,0`. Jamais une valeur de secours. Jamais un silence.

### F. Le « pas de donnée » du raster se distingue d'un zéro mesuré

Un pixel sans donnée (la valeur `nodata` déclarée par le fichier, ou un pixel
masqué) n'est **pas** une altitude de zéro. Il est exclu de tout calcul et
compté à part. Un `0,0` lu sur un pixel valide, lui, est une mesure réelle — le
niveau de la mer selon le géoïde de référence du produit — et il est conservé.

La valeur `nodata` est lue dans le fichier, jamais écrite en dur dans le code.
Si un fichier ne déclare pas de valeur `nodata`, c'est le masque du raster qui
fait autorité, et le fait est compté.

Une cellule qui n'aurait plus aucun échantillon valide après exclusion du
`nodata` prend la sentinelle `−1`, jamais `0` (règle n° 8), et fait rougir le
contrôle en nommant la cellule.

### G. Une garde de couverture, avant toute lecture

Une garde compare la liste des tuiles requises dérivée en B à la liste déclarée
dans `sources.lock`, **avant la première lecture d'altitude**. Une tuile
manquante arrête le lot en la nommant.

Une garde posée après l'effet qu'elle doit empêcher ne protège rien (règle
n° 5) : c'est précisément pour cela que celle-ci passe avant, et non après.

### H. La recette de l'empreinte collective est figée

Le code livré essayait quatre recettes successives et retenait celle qui
retombait sur la valeur attendue. C'est une méthode de découverte acceptable une
fois ; ce n'est pas une méthode de production.

La recette déjà démontrée devient **la** recette canonique : empreinte SHA256 de
la concaténation, triée par nom de tuile, de `nom_de_tuile` suivi de
`sha256_de_la_tuile`. Elle est nommée dans `sources.lock` **par son nom**, jamais
par sa valeur. La fonction d'essai de plusieurs recettes est supprimée. Une
empreinte collective qui ne correspond pas est un échec, pas le début d'une
recherche.

### I. Ce qui reste intouchable

`pipeline/geo/constants.py`, `pipeline/geo/pipeline.py` et
`pipeline/geo/qa/checks.py` restent interdits en écriture. Cet amendement ne
contient aucune justification pour les modifier, et n'en accorde aucune.

C'est réalisable : le contrôle `G6-A` reçoit déjà un booléen calculé par le
module de relief. Les nouvelles gardes alimentent ce booléen, sans qu'une seule
ligne de la barre qualité ne bouge.

### J. Aucune maille partielle

Le lot ne livre pas une carte à moitié mesurée. Il n'a le droit ni de restreindre
G6 à l'emprise couverte, ni de marquer durablement le reste comme non mesuré, ni
de réintroduire un repli. Soit toutes les tuiles requises sont là et vérifiées,
soit le lot escalade.

---

## 4. Le risque, dit honnêtement

**Réseau et temps.** 934 tuiles à récupérer, ordre de grandeur 3,6 à 5,3 Go.
Le débit réel n'a pas été mesuré dans cette session — aucun téléchargement n'a
été fait. Selon le débit obtenu, la récupération peut prendre de quelques
dizaines de minutes à plusieurs heures, et peut être ralentie par le fournisseur
sur un volume de cette taille. Le temps réellement passé est un fait à mesurer
et à publier, pas à estimer après coup.

**Tuiles éventuellement absentes du dépôt public.** Le produit Copernicus ne
publie pas nécessairement une tuile pour chaque carré de 1° du globe. Une partie
des 1 108 tuiles requises tombe sur de l'océan ouvert ou du désert. Le risque
qu'une ou plusieurs tuiles requises n'existent pas côté fournisseur est réel et
n'a pas été levé dans cette session.

C'est pourquoi le brief impose un **sondage préalable** de disponibilité sur les
1 108 tuiles, par requête d'en-tête, **avant** de télécharger le moindre
gigaoctet. Une tuile requise introuvable arrête le lot immédiatement, en la
nommant, plutôt qu'après plusieurs heures de transfert. Le contournement est
interdit : une tuile absente n'autorise ni un `0,0`, ni un repli, ni une maille
réduite. Elle autorise une escalade vers le propriétaire, et rien d'autre.

**Budget d'exécution.** La récupération est **une commande longue**, pas 934
appels d'outil. Le lot qui ferait un appel par tuile épuiserait son budget
d'exécution avant d'avoir mesuré quoi que ce soit.

---

## 5. Un point d'honnêteté sur le harnais lui-même

Le contrôle mécanique `rubric_predates_deliverables` compare la date déclarée en
tête de `eval-rubric.md` à la date des livrables. Il existe pour prouver qu'une
grille d'évaluation n'a pas été écrite après avoir vu les résultats.

**Pour cette itération, il ne prouve pas cela.** La rubrique a été amendée le
2026-08-21, après le verdict `FAIL` du 2026-08-20, en connaissance des
résultats — sur décision du propriétaire, ce qui est légitime, mais ce n'est pas
ce que le contrôle affirme. Les dates d'origine des deux documents ont été
conservées telles quelles, parce que ce sont des faits ; la date d'amendement
est portée séparément, en tête de chacun d'eux.

Pour que ce fait ne repose pas sur la seule prose, le brief exige un compteur
dédié qui le porte jusque dans le verdict. Un contrôle vert dont on sait qu'il ne
mesure pas ce qu'il prétend doit être déclaré, pas encaissé.

---

## 6. Ce que cet amendement change dans les documents du lot

| document | nature du changement |
|---|---|
| `brief.md` | décisions D2, D6, D9, D12, D13 réécrites ; D14 à D18 ajoutées ; SC1 et SC3 réécrites ; SC7 ajoutée ; tableau des compteurs refondu ; interdictions et waivers complétés |
| `eval-rubric.md` | SC1 et SC3 réécrites ; SC7 ajoutée ; contre-preuves ajoutées pour la convention d'ouest, le repli supprimé, le `nodata` et la garde de couverture ; échecs disqualifiants complétés |
| cet amendement | décision, preuves, risques — jamais une instruction |

Le lot se rejoue dans **le même worktree agent et la même PR #122**, par
régénération complète suivie d'une nouvelle relecture. Aucune nouvelle branche,
aucune nouvelle PR, aucune fusion.
