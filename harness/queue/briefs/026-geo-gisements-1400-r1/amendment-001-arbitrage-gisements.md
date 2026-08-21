# Amendement 001 — arbitrage du propriétaire sur les gisements du lot 026

**Authored**: 2026-08-21T07:13:06Z
**Author**: forge-planificateur
**Amende**: `harness/queue/briefs/026-geo-gisements-1400-r1/brief.md` et
`harness/queue/briefs/026-geo-gisements-1400-r1/eval-rubric.md`

> **Note de transparence (contrat du Planificateur) :** le rôle signataire est
> le rôle natif du harnais `forge-planificateur`. L'acteur réel est Claude
> Code (CTO), en session interactive, saisi par Hermes après la décision du
> propriétaire. Cette session n'a écrit que sous
> `harness/queue/briefs/026-geo-gisements-1400-r1/` : ni `hermes/**`, ni
> `docs/**`, ni `pipeline/**`, ni `ROADMAP.md`, ni `VISION.md`. Elle n'a rien
> committé, rien poussé, rien fusionné, et n'a lancé ni Cursor ni ForgePilot.
> Le Générateur n'a pas écrit une ligne de ce fichier : c'est la condition
> même de sa valeur (le producteur ne s'accorde pas son autorisation).

---

## 1. La décision du propriétaire, citée par son chemin

**Chemin de la décision écrite :**
`hermes/requests/DEMANDE-20260821-arbitrage-gisements-026.md`
(auteur `hermes`, `kind: demande`, créée le 2026-08-21, statut
`HANDED_TO_CTO`, objet « brief 026 »).

Ce fichier existe dans le dépôt et il tranche bien la question des
ressources : il porte trois décisions numérotées du propriétaire et les
transmet explicitement à Claude Code pour amendement du lot 026. Une citation
vers un fichier absent, ou vers un document qui ne parlerait pas des
ressources, ne vaudrait pas décision — c'est exactement ce que `SC0` de
`brief.md` existe pour attraper.

---

## 2. Réponses aux trois questions laissées ouvertes

### `A1` — la provenance « connaissance historique générale » est-elle acceptable ?

**Oui**, avec la condition que le propriétaire a posée lui-même : le degré de
certitude doit être **déclaré honnêtement gisement par gisement**.

Ce que cela change au mécanisme : rien. Il exigeait déjà une `certainty` par
entrée et refusait un niveau uniforme appliqué à tout le fichier. Ce que cela
ferme : la réserve que le Planificateur avait signalée — s'appuyer sur
`P1_PROVENANCE` / `P2_PROVENANCE` comme précédent était un raisonnement, pas
une autorisation ; c'en est une désormais. Aucune citation primaire n'est
exigée pour cette couche, et aucune source minière n'entre dans
`pipeline/geo/sources.lock`.

### `A2` — la liste d'amorce de vingt-sept gisements est-elle celle que le monde doit contenir ?

**Oui, comme amorce provisoire — et cet amendement retient la liste de `D4`
sans en retirer ni en ajouter aucune entrée.**

Le propriétaire l'accepte explicitement comme « amorce provisoire », « non
exhaustive » et « remplaçable ». Trois conséquences :

1. **Aucune table concurrente.** Cet amendement ne recopie pas la liste : la
   table autoritaire reste `D4` de `brief.md`, désormais amendée d'une colonne
   de classe (voir §3). Deux tables dans le même répertoire dériveraient l'une
   de l'autre au premier ajout ; l'unique instruction de l'exécutant reste
   `brief.md` (`CLAUDE.md` › Single Source of Instruction).
2. **Les identifiants, noms, natures, coordonnées et certitudes de `D4` ne
   bougent pas.** Cet amendement n'en corrige aucun, n'en déplace aucun,
   n'ajoute aucun « site évident qui manque ». Toute évolution ultérieure
   passera par un nouvel amendement ou par un remplacement du fichier de
   données.
3. **La liste reste remplaçable sans toucher au code.** C'est pourquoi elle
   vit dans `pipeline/geo/data/resources_1400.json` et non dans le module
   d'étape, et pourquoi un contrôle refuse qu'un identifiant de gisement
   apparaisse en dur dans le module.

### `A3` — « ressource » signifie-t-elle présence d'un gisement travaillé, sans aucune quantité ?

**Reformulation explicite du propriétaire, plus large que la question posée :**
un gisement géographique porte **trois** choses — sa **présence**, son **type
de ressource**, et une **classe qualitative de richesse**. Il ne porte
**aucune** quantité numérique, aucune réserve, aucun tonnage, aucun rythme
d'extraction : ces grandeurs ne sont pas décidées ici et restent du ressort de
`sim/`.

Donc : **oui** sur l'interdiction des quantités, qui reste entière et
mécanique ; **et** une exigence nouvelle — la classe qualitative — qui
n'était pas dans le brief d'origine et que le §3 tranche.

---

## 3. La classe qualitative de richesse : le vocabulaire retenu

Le propriétaire a décidé **qu'**une classe qualitative de richesse existe. Il
a laissé au Planificateur le soin de proposer « une représentation qualitative
bornée et vérifiable, cohérente avec les principes du dépôt ». C'est l'objet de
cette section.

### Le champ garde le mot de la décision

Le champ s'appelle **`richness_class`** — la classe de richesse, telle que le
propriétaire l'a nommée. Le renommer en autre chose (« envergure »,
« rayonnement », « notoriété ») aurait été détourner la décision en douce :
livrer une notion voisine sous le couvert d'un mot différent, et laisser au
propriétaire le soin de découvrir plus tard qu'il n'a pas reçu ce qu'il avait
demandé. Le champ porte donc le mot décidé ; ce sont ses **valeurs** et son
**critère d'attribution** qui font le travail d'honnêteté.

### Le vocabulaire fermé : trois valeurs, pas une de plus

| valeur | ce qu'elle affirme du gisement autour de 1400 |
|---|---|
| `mineure` | ce qu'on en tirait se travaillait et se consommait sur place — la vallée, le pays immédiat. Au-delà, le site n'était pas connu |
| `notable` | son produit alimentait sa région et les marchés voisins ; on le connaissait à l'échelle d'un pays ou d'un bassin |
| `majeure` | son produit s'échangeait loin de son lieu d'extraction, et le site était connu bien au-delà de son pays |

Trois valeurs, pas quatre : au-delà, la frontière entre deux classes cesse
d'être défendable à partir de connaissance historique générale, et une classe
qu'on ne sait pas discriminer honnêtement est une classe inventée.

### Le critère d'attribution est observable, et c'est dit

Une classe de richesse doit bien être attribuée par **quelque chose**. Deux
critères étaient possibles :

- **la richesse géologique du gisement** — teneur du minerai, étendue du
  filon. C'est une mesure, nous n'en avons aucune pour 1400, et la déclarer à
  partir de connaissance générale serait la donnée fabriquée en silence que la
  règle n° 10 interdit ;
- **ce que le monde en constatait** — jusqu'où le produit du gisement
  s'échangeait et jusqu'où le site était connu. C'est un fait historique
  observable, du même ordre que le reste de la liste, et c'est le premier
  maillon du principe n° 3 (origine → transport → stockage → destination).

**Le critère retenu est le second**, et le brief l'écrit noir sur blanc dans
son vocabulaire. La classe reste donc une classe de richesse — un gisement
dont le produit portait loin était, dans les termes du monde, un gisement
riche — mais elle est attribuée sur ce que l'on sait, pas sur ce que l'on
suppose. Le sel de Lüneburg atteignait la Baltique entière ; l'argent de
Schwaz, autour de 1400, ne sortait guère de sa vallée. C'est cela que les trois
valeurs affirment, et rien de plus.

### Ce que la classe n'a pas le droit de devenir

La classe est un **nom**, jamais un nombre. Elle n'est ni un rang, ni un
indice, ni un coefficient, ni un multiplicateur, ni une taille de point sur
une carte. Elle n'est pas non plus une propriété de la cellule : une cellule
liste des identifiants de gisements, jamais un niveau — lui en attacher un
ferait d'elle une case notée, c'est-à-dire exactement la forme de
`terrain_endowment.json`, la table de barème du jeu hérité que ce lot lit
comme contre-exemple.

Ces interdits ne sont pas laissés à la bonne volonté de l'exécutant. Ils sont
mécaniques : le contrôle `R1-G`, le schéma fermé de `D3`, le vocabulaire
déclaré en `D5` et la condition `SC6` de `brief.md` les portent. Cet
amendement ne les recopie pas.

**Conséquence assumée sur le garde-fou des quantités.** Les mots `richness` et
`richesse` sortent de la liste des clés interdites du lot, parce qu'une liste
qui bannit le mot même de la décision forcerait à renommer la décision. Ce qui
protège contre une richesse numérique n'est plus un mot banni, mais trois
mécanismes : le schéma fermé des gisements (aucune clé hors schéma), `R1-G`
(la classe est une chaîne du vocabulaire, jamais un nombre), et le maintien
dans les clés interdites des mots qui nomment vraiment une grandeur de
minerai — `grade`, `teneur`, `intensite`, `tonnage`, `reserve`, `rendement`.

### Le classement des vingt-sept a le même statut que la liste

La colonne ajoutée à `D4` est **de la donnée déclarée**, de même provenance et
de même statut que le reste de la liste : une amorce, honnête, provisoire et
remplaçable sans toucher au code. Sa véracité historique n'est pas une
condition de succès du lot, et l'Évaluateur ne rejette pas le lot parce
qu'une classe lui paraît discutable — au même titre qu'une date. Ce qu'il
juge, c'est que le mécanisme est honnête : vocabulaire fermé, classe
obligatoire, jamais numérisée, jamais portée par une cellule, dénombrement
prouvé, réversibilité, déterminisme.

---

## 4. Ce que cet amendement ne fait pas

- **Il n'autorise pas l'exécution du lot.** La dépendance dure reste entière :
  le lot 026 ne s'exécute qu'**après la fusion du lot 025**, qui pose
  `WORLD_TERMS_FORBIDDEN_KEYS` dans `pipeline/geo/constants.py`. Vérifié à
  l'écriture de cet amendement : cette constante **n'existe pas encore** dans
  le fichier. Le préalable est donc, aujourd'hui, non satisfait.
- **Il n'autorise pas deux lots à la fois**, ni une fusion automatique, ni un
  raccourci de relecture.
- **Il ne déplace pas les quantités.** Réserves, tonnages, rendements et
  rythmes d'extraction restent hors de la géographie et du ressort de `sim/`.
  Cet amendement ne décide rien de leur forme là-bas.
- **Il ne rend pas la liste exhaustive.** Vingt-sept gisements ne sont pas un
  inventaire de l'Europe de 1400, et le fichier de données le dit lui-même.
- **Il n'ajoute aucune source externe** et ne touche pas à
  `pipeline/geo/sources.lock`.

---

## 5. Ce qui reste ouvert, et qui n'est pas tranché ici

Signalé plutôt que décidé en silence :

1. **Comment `sim/` lira la classe.** Une classe qualitative devra bien, un
   jour, peser sur quelque chose. Ce sera une décision de `sim/`, dans un lot
   à elle, et elle devra se faire en termes de monde — pas en rebranchant un
   coefficient sur les trois noms.
2. **Le remplacement de l'amorce.** Le jour où une liste plus complète ou
   mieux sourcée arrivera, elle remplacera le fichier de données. Le mécanisme
   posé ici ne changera pas ; les vingt-sept ne sont pas un socle sur lequel
   bâtir, mais un contenu à remplacer.
3. **Les ressources agricoles, forestières et pastorales.** Toujours pas
   livrables : elles dépendent d'un climat et d'un sol dont le dépôt ne dispose
   pas. Rien dans cet amendement ne les débloque.
