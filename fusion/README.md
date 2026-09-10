# Forge — moteur de simulation historique et jeu de grande stratégie

> **Ce fichier est le README du dépôt fusionné**, écrit avant que le dépôt
> existe. Il décrit ce que sera `PLiagre/Forge` quand les trois projets
> actuels y auront été réunis. Tant que la fusion n'est pas faite, il se lit
> comme une carte des lieux.
>
> Pour la destination : [`OBJECTIF.md`](OBJECTIF.md).
> Pour le chemin : [`ROADMAP.md`](ROADMAP.md).
> Pour s'en servir : [`NOTICE.md`](NOTICE.md).
> Pour la chaîne : [`WORKFLOW.md`](WORKFLOW.md).

---

## En une page

Un moteur simule l'Europe de 1400 à 1900, jour par jour, sans interface. Des
vues le regardent. Un jeu de grande stratégie se construit dessus : on commence
seigneur d'un domaine, on finit — peut-être — à la tête d'un État.

Le moteur ne connaît pas le jeu. Il connaît des cellules, des habitants, des
kilos et des routes. Le gameplay **émerge** de leur physique ; il n'est écrit
nulle part.

```
                       data/world-1400.json
                       (la carte, figée)
                                │
                                ▼
                         ┌─────────────┐
                         │    sim/     │  le moteur — un tick = un jour
                         │  (Python)   │  extraction, production, commerce,
                         └──────┬──────┘  consommation, faim, mortalité,
                                │         natalité, migration
                                ▼
                          snapshot JSON        ← la photographie du monde
                                │
              ┌─────────────────┼─────────────────┬──────────────┐
              ▼                 ▼                 ▼              ▼
        vues/tableau/    vues/chronique/    vues/relief/      unity/
        tableau de bord  planche + bobine   carte 3D          la ville
        2D web           (suite d'instants) (forge3d)         jouable
```

**Aucune vue ne décide rien.** Elles lisent une photographie et l'affichent.
Le moteur tourne sans elles : `python3 -m sim` n'a besoin d'aucune dépendance
extérieure.

## Le monde, en chiffres mesurés

| | |
|---|---|
| étendue | Irlande → Anatolie, Norvège → Alexandrie |
| projection | EPSG:3035 (lon/lat WGS84 conservés en référence) |
| surface | 6 667 147 km² |
| cellules | 596, dont 11 186 km² en moyenne |
| arêtes d'adjacence | 1 364 |
| provinces nommées | 50 |
| relief | 322 plaines · 162 collines · 77 montagnes · 20 marais · 15 hautes montagnes |
| gisements | 27 |
| population à t0 | 66 649 511 |
| coût d'une année simulée | ~36 s |

La carte est **figée** et n'est fabriquée par personne : un seul fichier lu par
le moteur. Sa fidélité est déclarée — niveau 1 pour le trait de côte, le relief
et les gisements ; niveau 2 (plausible, jamais sourcé) pour ce qu'on en déduit.

## D'où vient ce dépôt

Trois dépôts et un outil orphelin ont été réunis. Chacun apportait autre chose.

### ForgeHistory — le moteur, les vues, la chaîne

Le cœur. Python, bibliothèque standard seule pour tout ce qui compte.

| dossier | ce qu'il fait |
|---|---|
| `sim/` | le moteur. Un tick = un jour. Ne dépend de rien. |
| `data/` | la carte figée et les centres de provinces |
| `vues/tableau/` | tableau de bord 2D web, sur `GET /dashboard.json` |
| `vues/chronique/` | la suite des instants : planche HTML et bobine `.webm` |
| `vues/relief/` | la carte en relief, rendue par forge3d |
| `outils/` | l'intégration qui **décide** — et n'écrit jamais sur GitHub |
| `.github/scripts/` | l'intégration qui **fait** le geste |
| `briefs/` | les briefs, seule source d'instruction d'un lot |

### VictoriaCityLab — la ville, les assets

Unity `6000.0.43f1`, pipeline URP `17.0.4`.

| dossier | ce qu'il fait |
|---|---|
| `unity/Assets/` | la scène, le terrain, la caméra RTS, le HUD |
| `unity/Packages/com.victoria.citymode*` | contrats, présentation, assets — quatre paquets isolés |
| `fabrique/` | l'Asset Factory : Blender **hors Unity**, recettes déterministes |
| `ville/` | le contrat d'intégration v1 : schéma JSON, exemples, matrice d'autorité |

Ce qu'elle a déjà prouvé : 56 FBX, 24 prefabs de bâtiments, 8 personnages, un
trim sheet PBR 2048², une boucle de construction jouable, une économie de
village à sept chaînes de production, 60 FPS à 100 habitants.

### forge3d — le moteur de rendu

**Ce n'est pas du code de ce projet.** C'est un fork intact de
[`milos-agathon/forge3d`](https://github.com/milos-agathon/forge3d) — 399
commits, aucun de nous, MIT/Apache-2.0. Rust + WebGPU sous un paquet Python.
Il est consommé comme **dépendance épinglée**, jamais recopié : c'est ce qui
permet de continuer à tirer les correctifs amont.

### ForgeAtelier — l'invocation des agents

Le quatrième, et il n'était un dépôt nulle part : une branche détachée d'un
côté, une copie vendorisée de l'autre. Il distribue les cartes de travail,
isole chaque rôle dans son worktree, journalise ses réveils — et ne fusionne
jamais. La fusion l'extrait en dépôt à part entière.

## Comment le travail avance

Le propriétaire donne une direction. Le reste avance sans lui.

Une fiche entre au registre. Un **briefer** écrit le brief. Un **coder**
l'exécute. La **CI** joue les tests. Un **relecteur** — jamais l'auteur —
approuve sur la révision courante. L'**intégration** fusionne, une PR à la
fois, rejouée sur le dernier `master`.

Trois choses seulement restent au propriétaire : donner des directions,
reprendre ce qui est tombé, et fusionner ce qui n'est pas un lot.

Le détail, avec le schéma : [`WORKFLOW.md`](WORKFLOW.md).

## Où en est le produit

| couche | état |
|---|---|
| 1 — **Monde vivant** | faite, et elle tourne |
| 2 — **Villes** | ouverte : un métier existe, le bourg se compte |
| 3 — **États** | non commencée |
| 4 — **Armées** | non commencée |
| 5 — **Batailles** | non commencée |

**Ce que le monde sait faire.** Le relief module le rendement d'une cellule et
le débit d'une arête. Le climat joue par la durée du jour, donc par la saison.
Les gisements produisent des kilos, et une part des habitants cesse de cultiver
pour extraire — c'est le premier métier. Le commerce transporte n'importe
quelle marchandise, par terre et par mer. La population naît, meurt de faim,
et migre vers les voisines en surplus.

**Ce qu'il ne sait pas encore faire.** Fabriquer : le minerai extrait reste du
minerai. Se subdiviser : une cellule fait 11 186 km², c'est une région, pas un
lieu. Se dater : le rang du jour se dérive du numéro du tick, mais le monde ne
porte aucune date. Et surtout —

**Se nourrir.** Le monde amorce plus de bouches que sa terre n'en nourrit. Le
plafond de survie dérivé vaut **0,691** : il en faudrait 1. Mesuré :

```
départ    : 66 649 511 habitants
30 ticks  : 37 069 407        (238 cellules affamées)
60 ticks  : 11 495 086
365 ticks :  9 555 814        →  86 % du monde meurt la première année
```

Ce n'est pas une famine émergente, c'est une erreur d'amorçage. Tant qu'elle
tient, « lancer une simulation et l'afficher » montre un effondrement. C'est le
premier lot de la V1.

## Ce que la V1 exige

1. un dépôt, une chaîne automatique verte de bout en bout ;
2. une simulation qui ne s'effondre pas ;
3. la carte de statistique en 3D ;
4. le tableau de bord et la chronique sur le même snapshot.

Ni vue ville jouable, ni États, ni armées, ni batailles : ceux-là viennent
après, et [`ROADMAP.md`](ROADMAP.md) dit dans quel ordre.

## Les règles, en trois phrases

**Une capacité n'existe que si sa preuve peut échouer.** Un test qui ne peut
pas rougir ne prouve rien, et un échantillon vide échoue au lieu de passer en
silence.

**L'absence de données ne s'invente pas.** Elle se déclare, à l'endroit où elle
manque.

**Celui qui a écrit le code ne dit pas s'il est recevable.** C'est une règle
mécanique, pas une politesse : l'approbation doit venir d'une connexion qui n'a
écrit aucun des commits, sur la révision courante.

Le reste vit dans `AGENTS.md`, et rien ne le paraphrase.
