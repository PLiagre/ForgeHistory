# ROADMAP — où on en est

Ce fichier dit **où on en est** et **dans quel ordre on avance**. Il ne dit
jamais quoi faire pour un lot donné : ça, c'est le brief.

La vision produit (ce que le jeu **est**) vit dans [VISION.md](VISION.md) et
prime en cas de conflit. Le fonctionnement du monde vit dans
[`sim/MODELE.md`](sim/MODELE.md). Les règles vivent dans
[AGENTS.md](AGENTS.md).

---

## Les cinq couches

| # | couche | statut |
|---|---|---|
| 1 | **Monde vivant** — carte, terrain, climat, ressources, population, économie locale, commerce | **faite, et elle tourne** |
| 2 | **Villes** — urbanisation, entreprises, métiers, routes, infrastructures | **ouverte** — trois briefs écrits |
| 3 | **États** — fiscalité, lois, diplomatie, technologies, culture, religion | non commencée |
| 4 | **Armées** — recrutement, logistique, ravitaillement, stratégie | non commencée |
| 5 | **Batailles tactiques** — sur les mêmes données que tout le reste | non commencée |

---

## Couche 1 — ce que le monde sait faire

La carte est **figée** : `data/world-1400.json`, un seul fichier lu par
`sim/`. Elle porte les cellules, leurs arêtes d'adjacence, le relief en cinq
classes, les déterminants du climat et les gisements nommés de 1400. Les
compter se fait par une commande, jamais en recopiant un nombre ici :

```bash
python -m sim --ticks 0 --json
```

Le tick joue, dans cet ordre : extraction minière, production agricole,
commerce, consommation, faim, mortalité, natalité, migration. Concrètement :

- le **relief** module le rendement d'une cellule et le débit d'une arête ;
- le **climat** joue par la durée du jour, donc par la saison ;
- les **gisements** produisent des kg de leur ressource dans le panier ;
- le **commerce** transporte n'importe quelle marchandise, pas seulement la
  nourriture, avec une capacité dérivée de la longueur de frontière partagée ;
- la population **naît**, **meurt de faim** et **migre** vers les voisines en
  surplus ;
- la **province** est une agrégation dérivée, recalculée, jamais stockée.

`viewer/` est un regard mince sur une photographie du monde. Il lit, il ne
décide jamais.

### Ce que le monde ne sait pas encore faire

- **Fabriquer.** Le minerai extrait reste du minerai : rien ne le transforme.
- **Utiliser la mer.** Les arêtes maritimes de la carte sont écartées en
  silence. Plus d'une cellule sur trois n'a aucun voisin terrestre : elle ne
  peut ni recevoir ni donner un kilogramme.
- **Diviser le travail.** Tout le monde laboure. Il n'y a pas de métier.
- **Concentrer les gens.** Aucun endroit ne peut abriter plus de monde qu'il
  n'en nourrit lui-même — voir ci-dessous.

---

## Couche 2 — les villes

Une ville est un endroit qui **ne produit pas ce qu'il mange**. Trois choses
manquent pour qu'elle puisse exister, et chacune a son brief écrit :

| brief | ce qu'il ouvre |
|---|---|
| [044 — un métier : le mineur](briefs/044-un-metier-le-mineur.md) | la première division du travail : les mineurs ne labourent pas |
| [046 — la mer est un port commun](briefs/046-la-mer-est-un-port-commun.md) | les cellules côtières cessent d'être hermétiques |
| [047 — le bourg est une agrégation dérivée](briefs/047-le-bourg-est-une-agregation-derivee.md) | compter la part non agricole d'une cellule, sans la déclarer |

Aucun ordre n'est imposé entre eux, **sauf** 044 avant 047 : le bourg compte
une part non agricole que le métier doit d'abord créer.

> **Ces trois briefs sont à l'ancien format** (dossier, grille d'évaluation,
> tableau de compteurs, manifeste). Ils sont à réécrire au format court
> d'[AGENTS.md](AGENTS.md) avant d'être lancés. Leur règle du monde et leurs
> conditions de succès sont bonnes : c'est la cérémonie autour qui saute.

### Le mur, et pourquoi il est en train de tomber

Mesuré avant d'écrire ces briefs : **aucun mécanisme du moteur ne concentrait
la population.** Ni la natalité, ni la migration de famine ne faisaient monter
la densité de la cellule la plus dense au-dessus de la médiane, à 365 comme à
1 000 ticks.

La cause était chiffrable : une cellule médiane consomme des centaines de
milliers de kg par tick, et une arête d'adjacence en transportait deux cents.
Le commerce était **près de mille fois trop petit** pour l'échelle des
cellules. Aucun endroit ne pouvait être nourri par ses voisins.

La capacité dérivée de la longueur de frontière a **baissé ce mur, sans le
lever**. Ce qui reste : la mer (046) et le métier (044). Le bourg (047)
s'écrit sur une mesure, pas sur une intention — c'est pourquoi il vient
après.

---

## Le dégraissage V1

Le dépôt est passé de 363 fichiers à une cinquantaine. Sont sortis de l'arbre
de travail : le pilote ForgePilot, le harnais et sa porte mécanique, le
pilotage Hermes, les vingt et un ADR, l'outil qui a fabriqué la carte, et les
quarante lots déjà faits avec leurs preuves.

Rien n'est perdu : tout vit dans l'historique git, au tag
`v0-avant-degraissage` (voir [AGENTS.md](AGENTS.md) § « Les archives »).

Ce qui reste : la vision, le modèle, le moteur, ses tests, le regard mince,
la carte, et les briefs. Le workflow tient en cinq lignes dans
[AGENTS.md](AGENTS.md), et c'est le propriétaire qui le pilote.
