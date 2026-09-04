# ROADMAP — où on en est

Ce fichier dit **où on en est** et **dans quel ordre on avance**. Il ne dit
jamais quoi faire pour un lot donné : ça, c'est le brief.

La vision produit (ce que le jeu **est**) vit dans [VISION.md](VISION.md) et
prime en cas de conflit. Le fonctionnement du monde vit dans
[`sim/MODELE.md`](sim/MODELE.md). Les règles vivent dans
[AGENTS.md](AGENTS.md).

**L'état d'un lot ne s'écrit qu'à un seul endroit : sa fiche, dans le
[registre des lots](#le-registre-des-lots).** La prose de ce fichier raconte
le monde ; elle ne dit jamais qu'un lot est prêt ou livré. Si une phrase et
une fiche se contredisent, la fiche a raison et la phrase est à corriger.

---

## Les cinq couches

| # | couche | où on en est |
|---|---|---|
| 1 | **Monde vivant** — carte, terrain, climat, ressources, population, économie locale, commerce | **faite, et elle tourne** ; la mer reste à finir (lot 046) |
| 2 | **Villes** — urbanisation, entreprises, métiers, routes, infrastructures | **ouverte** — le premier métier existe, le bourg se compte ensuite (lot 047) |
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
py -m sim --ticks 0 --json
```

Le tick joue, dans cet ordre : extraction minière, production agricole,
commerce, consommation, faim, mortalité, natalité, migration. Concrètement :

- le **relief** module le rendement d'une cellule et le débit d'une arête ;
- le **climat** joue par la durée du jour, donc par la saison ;
- les **gisements** produisent des kg de leur ressource dans le panier, et
  une part des habitants d'une cellule à gisement **cesse de cultiver** pour
  extraire : c'est le premier métier ;
- le **commerce** transporte n'importe quelle marchandise, pas seulement la
  nourriture, avec une capacité dérivée de la longueur de frontière partagée ;
- la population **naît**, **meurt de faim** et **migre** vers les voisines en
  surplus ;
- la **province** est une agrégation dérivée, recalculée, jamais stockée.

`viewer/` est un regard mince sur une photographie du monde. Il lit, il ne
décide jamais.

### Ce que le monde ne sait pas encore faire

- **Fabriquer.** Le minerai extrait reste du minerai : rien ne le
  transforme, et une marchandise que personne ne mange n'a jamais de
  demandeur — le fer s'accumule là où il sort. C'est le lot 049.
- **Utiliser la mer.** Les arêtes maritimes de la carte sont écartées en
  silence. Plus d'une cellule sur trois n'a aucun voisin terrestre : elle ne
  peut ni recevoir ni donner un kilogramme. Le lot 046 ouvre le bassin aux
  marchandises ; le lot 050 l'ouvre ensuite aux migrants.
- **Compter un bourg.** La part non agricole d'une cellule existe depuis le
  lot 044, mais aucune vue ne la lit encore. Le lot 047 la compte, le lot 051
  la photographie et le lot 052 la montre.
- **Concentrer les gens.** Aucun endroit ne peut abriter plus de monde qu'il
  n'en nourrit lui-même — voir ci-dessous.
- **Dater le monde.** Le rang du jour se dérive du numéro du tick, mais le
  monde lui-même ne porte aucune date. C'est le lot 053.

---

## Couche 2 — les villes

Une ville est un endroit qui **ne produit pas ce qu'il mange**. Trois choses
manquaient pour qu'elle puisse exister : un métier (lot 044), la mer (lot
046), et une vue qui compte le bourg (lot 047). Le registre dit où chacun en
est.

Les lots 046 et 047 ont des périmètres disjoints (voir leurs sections
« Périmètre ») : aucun des deux n'attend l'autre. Le 047 attend le 044, et le
dit lui-même : sans part non agricole, son échantillon est vide et il échoue.

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
lever**. Ce qui reste : la mer (046). Le bourg (047) s'écrit sur une mesure,
pas sur une intention — c'est pourquoi il vient après le métier.

---

## Cohérence globale — la preuve transversale manque

Les tests actuels protègent des invariants et des règles visibles, mécanisme
par mécanisme. L'historique conserve les travaux du premier jour et le tag
`v0-avant-degraissage` leurs anciennes preuves. Mais aucun inventaire courant
ne relie encore chaque promesse de `VISION.md` à ce qui fonctionne réellement,
à ce qui ne fonctionne que partiellement, à ce qui manque, et à la commande
qui permet de le constater. Un test vert ne dit donc pas quelle part de la
vision est effectivement couverte. C'est le manque recensé par le lot 054.

---

## Le registre des lots

**C'est ici, et seulement ici, que l'état d'un lot s'écrit.** Une machine le
lit : `python3 -m atelier feuille valider --projet .` refuse toute fiche mal
formée, tout numéro dupliqué, tout brief attendu qui manque, toute
dépendance qui n'existe pas. Un humain le lit aussi : une fiche par lot, deux
lignes, et **l'ordre des fiches est la priorité** — parmi les lots prêts, le
premier de la liste part le premier.

Une fiche : `### [NNN — Titre](briefs/NNN-slug.md)` puis
`état : … · couche : … · dépend de : … · PR : …`, et une ligne vide après.
La ligne vide n'est pas décorative : c'est elle qui permet à deux PR de lots
voisins de fusionner sans conflit. Un `## titre` entre les fiches n'est
qu'un intertitre pour l'œil. Le cycle — les états, qui les fait changer, et
quoi faire quand ça casse — est décrit après le registre.

<!-- lots:debut -->

## Présentation

### [048 — Tableau de bord : stats mêlées à la carte](briefs/048-dashboard-stats-carte.md)
état : livre · couche : — · dépend de : — · PR : 201

## Couche 2 — ouverte

### [046 — La mer est un port commun](briefs/046-la-mer-est-un-port-commun.md)
état : livre · couche : 1 · dépend de : — · PR : 206

### [047 — Le bourg est une agrégation dérivée](briefs/047-le-bourg-est-une-agregation-derivee.md)
état : livre · couche : 2 · dépend de : 044 · PR : 214

### [049 — Fabriquer : le minerai devient un objet](briefs/049-fabriquer-le-minerai-devient-un-objet.md)
état : a-briefer · couche : 2 · dépend de : 044 · PR : —

### [054 — Cohérence globale : inventaire du produit face à la vision](briefs/054-coherence-globale-inventaire-produit-vision.md)
état : a-briefer · couche : — · dépend de : — · PR : —

### [050 — On migre aussi par la mer](briefs/050-on-migre-aussi-par-la-mer.md)
état : a-briefer · couche : 1 · dépend de : 046 · PR : —

### [051 — Le snapshot photographie le bourg](briefs/051-le-snapshot-photographie-le-bourg.md)
état : a-briefer · couche : 2 · dépend de : 047 · PR : —

### [052 — Le regard mince montre le bourg](briefs/052-le-regard-mince-montre-le-bourg.md)
état : a-briefer · couche : 2 · dépend de : 051 · PR : —

### [053 — Le monde porte sa date](briefs/053-le-monde-porte-sa-date.md)
état : a-briefer · couche : 1 · dépend de : — · PR : —

## Livrés depuis le dégraissage V1

### [044 — Un métier : le mineur](briefs/044-un-metier-le-mineur.md)
état : livre · couche : 2 · dépend de : — · PR : 184, 188
note : la PR 188 est la réparation de la formule agricole que le brief rouvre ; la règle du monde est livrée par la 184.

## Archivés — avant le dégraissage V1 (briefs et preuves au tag `v0-avant-degraissage`)

### [045 — La migration lit le reste du tick](https://github.com/PLiagre/ForgeHistory/tree/v0-avant-degraissage/harness/queue/briefs/045-la-migration-lit-le-reste-du-tick)
état : archive · couche : 1 · dépend de : — · PR : 172

### [043-ter — ForgePilot transmet le délai de preuve effectif](https://github.com/PLiagre/ForgeHistory/tree/v0-avant-degraissage/harness/queue/briefs/043-ter-forgepilot-timeout-preuves)
état : archive · couche : — · dépend de : — · PR : 176

### [043-bis — Le monde d'épreuve exerce longueur et repli](https://github.com/PLiagre/ForgeHistory/tree/v0-avant-degraissage/harness/queue/briefs/043-bis-monde-epreuve-longueurs)
état : archive · couche : 1 · dépend de : — · PR : 175

### [043 — Le convoi est à l'échelle de la cellule](https://github.com/PLiagre/ForgeHistory/tree/v0-avant-degraissage/harness/queue/briefs/043-le-convoi-a-l-echelle-de-la-cellule)
état : archive · couche : 1 · dépend de : — · PR : 169

### [042 — Le regard mince montre ce que le moteur joue vraiment](https://github.com/PLiagre/ForgeHistory/tree/v0-avant-degraissage/harness/queue/briefs/042-le-viewer-montre-ce-qui-joue)
état : archive · couche : 1 · dépend de : — · PR : 173

### [041 — On s'en va quand on a faim](https://github.com/PLiagre/ForgeHistory/tree/v0-avant-degraissage/harness/queue/briefs/041-on-s-en-va-quand-on-a-faim)
état : archive · couche : 1 · dépend de : — · PR : 167

### [040 — Franchir une montagne coûte plus cher qu'une plaine](https://github.com/PLiagre/ForgeHistory/tree/v0-avant-degraissage/harness/queue/briefs/040-franchir-une-montagne-coute)
état : archive · couche : 1 · dépend de : — · PR : 164

### [039 — Le commerce cesse de ne connaître que la nourriture](https://github.com/PLiagre/ForgeHistory/tree/v0-avant-degraissage/harness/queue/briefs/039-le-commerce-porte-tout)
état : archive · couche : 1 · dépend de : — · PR : 163

### [038 — Les gisements sortent enfin quelque chose](https://github.com/PLiagre/ForgeHistory/tree/v0-avant-degraissage/harness/queue/briefs/038-les-gisements-sortent-du-minerai)
état : archive · couche : 1 · dépend de : — · PR : 157

### [037-bis — L'assertion de couche non consommée vise une couche inexistante](https://github.com/PLiagre/ForgeHistory/tree/v0-avant-degraissage/harness/queue/briefs/037-bis-assertion-couche-inexistante)
état : archive · couche : — · dépend de : — · PR : 156

### [037 — Le stock d'une cellule devient un panier de marchandises](https://github.com/PLiagre/ForgeHistory/tree/v0-avant-degraissage/harness/queue/briefs/037-le-stock-devient-un-panier)
état : archive · couche : 1 · dépend de : — · PR : 154

### [036 — On naît aussi : la population cesse de ne faire que mourir](https://github.com/PLiagre/ForgeHistory/tree/v0-avant-degraissage/harness/queue/briefs/036-on-nait-aussi)
état : archive · couche : 1 · dépend de : — · PR : 152

### [035 — La saison joue dans le rendement](https://github.com/PLiagre/ForgeHistory/tree/v0-avant-degraissage/harness/queue/briefs/035-la-saison-joue-le-rendement)
état : archive · couche : 1 · dépend de : — · PR : 151

### [034-bis — L'assertion de couche non consommée vise les gisements](https://github.com/PLiagre/ForgeHistory/tree/v0-avant-degraissage/harness/queue/briefs/034-bis-assertion-gisements)
état : archive · couche : — · dépend de : — · PR : 150

### [034 — La carte arrive au tick par les arguments, plus par une variable de module](https://github.com/PLiagre/ForgeHistory/tree/v0-avant-degraissage/harness/queue/briefs/034-moteur-sans-etat-cache)
état : archive · couche : 1 · dépend de : — · PR : 142

### [033 — Le relief joue dans le rendement alimentaire](https://github.com/PLiagre/ForgeHistory/tree/v0-avant-degraissage/harness/queue/briefs/033-relief-dans-le-rendement)
état : archive · couche : 1 · dépend de : — · PR : 137

<!-- lots:fin -->

Les lots antérieurs au 033 n'ont plus de brief dans l'historique : ils ne
sont pas recensés, et personne ne les invente.

---

## Le cycle d'un lot

### Les six états qu'une fiche peut porter

| état | ce que ça veut dire | qui l'écrit |
|---|---|---|
| `idee` | recensé et placé dans l'ordre, sans brief. Le fichier nommé par la fiche **n'existe pas encore**. | le propriétaire, dans une PR de feuille |
| `a-briefer` | le propriétaire veut le brief maintenant. Le pilote dépose la carte, le briefer écrit le brief et ouvre sa PR. | le propriétaire |
| `pret` | le brief est sur `master` et passe la porte (cinq sections, un périmètre nommé, des conditions qui peuvent échouer). Le pilote déposera la carte du coder dès que les dépendances sont livrées et qu'aucun fichier du périmètre n'est tenu. | la PR du brief, que le propriétaire fusionne |
| `livre` | la PR du lot est fusionnée sur `master`. La fiche porte son numéro. | la PR du lot elle-même, que le propriétaire fusionne |
| `abandonne` | on n'y va pas, ou on n'y va plus. La fiche reste, avec une `note :`. | le propriétaire |
| `archive` | livré avant le dégraissage V1 ; le brief vit au tag. | personne, plus jamais |

Tout ce qui est *entre* ces états — en file, en planification, en relecture,
à fusionner, bloqué, en échec — ne s'écrit pas : ça se **dérive** des cartes
de l'atelier, des verrous et des briefs. `python3 -m atelier feuille etat
--projet .` le montre lot par lot.

### Les transitions permises, et qui tient le geste

```
idee       → a-briefer   le propriétaire (PR de feuille)
idee       → pret        le propriétaire, quand il écrit le brief lui-même (PR qui apporte le brief)
a-briefer  → pret        la PR du brief, écrite par le briefer, fusionnée par le propriétaire
pret       → livre       la PR du lot, écrite par le coder, fusionnée par le propriétaire
pret       → a-briefer   le propriétaire, quand le brief est à réécrire
*          → abandonne   le propriétaire (sauf livre et archive, qui ne bougent plus)
abandonne  → idee        le propriétaire
```

Toute autre transition est refusée par `atelier feuille valider --base
origin/master`, que la CI joue sur chaque PR. Un lot n'entre jamais dans le
registre déjà livré, et une fiche ne s'efface pas : elle passe à `abandonne`.

**La fiche d'un lot fait partie du périmètre implicite de sa PR** — et rien
d'autre de ce fichier. La PR du brief passe la fiche à `pret` ; la PR du lot
la passe à `livre` avec son numéro (`python3 -m atelier feuille marquer
--projet . --lot NNN --etat livre --pr N`). C'est ce qui fait que `master`
ne dit « livré » qu'à l'instant exact où le changement y est, et jamais
avant : il n'y a aucune correction à faire après la fusion, donc aucune à
oublier. La CI refuse une PR de lot (`agent/NNN-slug`) dont la fiche ne dit
pas `livre` avec le bon numéro, ou qui touche une autre fiche.

### Ajouter un lot, le prioriser

Une PR de feuille : une fiche `idee` (ou `a-briefer`), à la place qu'elle
mérite dans l'ordre. Le numéro est le premier libre au-dessus du plus grand
recensé ; le slug est décidé là, une fois, parce que c'est le nom du brief
que le briefer écrira. Le périmètre et les fichiers ne sont **pas** dans la
fiche : ils sont dans la section « Périmètre » du brief, la seule source
d'instruction du lot — l'atelier les y lit pour poser le verrou.

### Quand ça casse

- **Un agent a échoué** (code de retour, délai, brief introuvable) : sa
  carte est dans `echec/` avec la raison, et `feuille etat` le montre. Le
  propriétaire lit la raison, corrige ce qu'il faut, puis
  `python3 -m atelier reprendre --projet . --lot NNN-slug` : le pilote
  redépose la carte le lendemain. Rien ne se relance tout seul.
- **Un lot est bloqué** par une dépendance non livrée : il ne bouge pas, et
  `feuille etat` dit par qui. Par un fichier tenu : idem, et le verrou se
  rend par `atelier lever` après la fusion — le pilote le fait lui-même quand
  la fiche dit `livre`.
- **Une PR est abandonnée** (fermée sans fusion) : le propriétaire range la
  carte (`python3 -m atelier echouer --projet . --role relire --lot NNN-slug
  --raison "PR fermée"`), rend le verrou (`atelier lever`), et décide dans
  une PR de feuille : le lot reste `pret` (il repartira) ou passe à
  `abandonne` (il ne repartira pas).
- **La feuille est incohérente** (brief orphelin, fiche sans brief, carte
  d'un lot inconnu, dépendance fantôme) : `atelier feuille valider` le dit,
  la CI rougit, et le pilote ne dépose rien tant que ce n'est pas réparé.

---

## Le dégraissage V1

Le dépôt est passé de 363 fichiers à une cinquantaine. Sont sortis de l'arbre
de travail : le pilote ForgePilot, le harnais et sa porte mécanique, le
pilotage Hermes, les vingt et un ADR, l'outil qui a fabriqué la carte, et les
lots déjà faits avec leurs preuves.

Rien n'est perdu : tout vit dans l'historique git, au tag
`v0-avant-degraissage` (voir [AGENTS.md](AGENTS.md) § « Les archives »).

Ce qui reste : la vision, le modèle, le moteur, ses tests, le regard mince,
la carte, et les briefs. Le workflow automatique ne revient pas ici : il
vit dans **ForgeAtelier** (branche `cursor/forgeatelier-ced6`, à détacher
dès que le dépôt GitHub existe — voir `docs/PUBLIER.md` là-bas). Ce dépôt
se branche avec [`atelier.toml`](atelier.toml). Le propriétaire fusionne
toujours.
