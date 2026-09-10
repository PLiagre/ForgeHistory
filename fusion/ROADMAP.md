# ROADMAP de fusion — des trois dépôts à la V1

> **Ce que dit ce fichier** : dans quel ordre on réunit les projets, et ce qui
> reste à faire une fois réunis. Il ne dit jamais *comment* faire un lot :
> ça, c'est le brief.
>
> La destination vit dans [`OBJECTIF.md`](OBJECTIF.md) et prime en cas de
> conflit. Le fonctionnement de la chaîne vit dans
> [`WORKFLOW.md`](WORKFLOW.md). Les commandes vivent dans
> [`NOTICE.md`](NOTICE.md).
>
> **L'état d'un lot ne s'écrit qu'à un seul endroit : sa fiche, au registre.**
> La prose raconte ; elle ne déclare jamais qu'un lot est prêt ou livré.

---

## Où on en est — mesuré le 10 septembre 2026

### Ce qui tourne

| | |
|---|---|
| moteur `sim/` | **596 cellules**, 66 649 511 habitants, une année en ~36 s |
| tests | **560 verts**, 7 rouges faute de ForgeAtelier sur le `PYTHONPATH` |
| couche 1 — monde vivant | faite, et elle tourne |
| couche 2 — villes | ouverte : un métier existe, le bourg se compte |
| CityLab | M0, M1, M2 livrés ; **M3 bloqué** |
| harnais CityLab | 51 tests verts, registre de 40 lots cohérent |
| contrat ville v1 | écrit, schématisé, testé — mais sans producteur |

### Ce qui est cassé, et qui bloque tout

**1. `master` de ForgeHistory est rouge.**

```
FAIL  briefs/055-le-monde-nourrit-ceux-qu-il-amorce.md — brief orphelin :
      aucune fiche ne le nomme
```

Toute PR rejouée sur `master` échoue. **Six PR sont bloquées** et empilées sur
une branche de contournement : #248 (répare le registre), #241 (lot 051),
#242 (049), #245 (053), #246 (054), #249 (052).

**2. Le monde ne nourrit pas ceux qu'il amorce.**

```
plafond de survie dérivé = 0,691          (il en faudrait 1)

départ    : 66 649 511 habitants
30 ticks  : 37 069 407      (238 cellules affamées)
60 ticks  : 11 495 086
365 ticks :  9 555 814      →  86 % du monde meurt la première année
```

Ce n'est pas une famine émergente : c'est une erreur d'amorçage. Tant qu'elle
tient, « lancer une simulation et l'afficher » montre un effondrement.

**3. ForgeAtelier n'existe comme dépôt nulle part.** ForgeHistory pointe une
branche détachée (`cursor/forgeatelier-ced6`) ; CityLab en garde une copie
vendorisée sous `Tools/ForgeAtelier`, épinglée à `dc52bb5`. Deux chaînes qui
divergent en silence, et 7 tests rouges du seul fait de cette absence.

**4. Le moteur et le contrat Unity se contredisent.** `sim/MODELE.md` interdit
explicitement tout `city_id` — « le bourg n'est pas déclaré, il est compté » —
pendant que `CityLaunchContext` en exige un, stable, avec sa cellule parente.
Le contrat réclame une chose que le modèle a déclaré ne jamais produire.

**5. La carte 3D ne montre aucune donnée.** `visualisateur/` pose une altitude
plausible par classe de relief et rend le terrain. Population, faim, stock,
bourg : rien n'est colorié. La « carte de statistique » n'existe pas.

---

## Les quatre conditions de la V1

La V1 est atteinte quand ces quatre choses sont vraies **ensemble**. Aucune ne
compte seule.

| # | condition | ce qui la prouve |
|---|---|---|
| **V1-1** | un dépôt, une chaîne verte | un lot traverse le cycle entier — brief, code, contrôles, relecture, intégration — sans main humaine |
| **V1-2** | une simulation qui ne s'effondre pas | le plafond de survie repasse au-dessus de 1 ; une année simulée ne tue plus 86 % du monde |
| **V1-3** | la carte de statistique en 3D | une commande simule, photographie et rend une carte colorée par une donnée du monde |
| **V1-4** | le tableau de bord et la chronique | les trois vues lisent le même snapshot, et le montrent |

**Ne sont pas dans la V1** : la vue ville jouable, la subdivision en lieux, les
États, les armées, les batailles. Ils viennent après, et l'ordre est plus bas.

---

## Les sept phases

### Phase 0 — Débloquer avant de fusionner

*On ne copie pas un dépôt rouge.*

| lot | ce qu'il fait |
|---|---|
| **100** | inscrire la fiche 055 au registre, activer les tests de chronique en CI — `master` redevient vert |
| **101** | faire entrer la pile : 051, 052, 049, 053, 054, rebasées sur `master` |

À la sortie : un `master` vert qui porte cinq lots de plus. **Aucune ligne
écrite n'est jetée.**

### Phase 1 — Extraire ForgeAtelier

| lot | ce qu'il fait |
|---|---|
| **102** | créer `PLiagre/ForgeAtelier` depuis `cursor/forgeatelier-ced6@04b701d` |
| **103** | y réconcilier les adaptations CityLab — profils, crons, `atelier-boucle` |
| **104** | l'épingler comme dépendance ; les 7 tests rouges deviennent verts |

À la sortie : **une** chaîne, plus deux copies qui divergent.

### Phase 2 — Fonder le dépôt

Un commit de fondation. Les deux dépôts d'origine passent en lecture seule avec
un tag d'archive, et le nouveau dit où les chercher.

```
sim/          le moteur, inchangé          unity/       la ville (LFS)
data/         la carte figée               fabrique/    l'Asset Factory
vues/                                      ville/       le contrat v1
  tableau/    ex-viewer                    outils/      l'intégration qui décide
  chronique/  ex-chronique                 .github/     workflows + scripts
  relief/     ex-visualisateur             briefs/      un seul registre
AGENTS.md  VISION.md  ROADMAP.md  atelier.toml
```

| lot | ce qu'il fait |
|---|---|
| **105** | fonder le dépôt, importer le moteur, les données et les vues |
| **106** | importer Unity, la fabrique et le contrat ; reprendre LFS tel quel |
| **107** | fusionner les deux jeux de règles en un seul `AGENTS.md` |
| **108** | fusionner les deux registres ; renuméroter (table plus bas) |
| **109** | épingler `forge3d==1.35.0` — **aucune ligne de son source n'entre** |
| **110** | poser les tags d'archive et passer les anciens dépôts en lecture seule |

### Phase 3 — Une seule chaîne automatique

| lot | ce qu'il fait |
|---|---|
| **111** | un `atelier.toml`, un registre, un préfixe `agent/` |
| **112** | les contrôles unifiés : `sim`, `vues`, `outils`, `feuille`, `gitleaks` |
| **113** | brancher le worker Unity Windows en contrôle **conditionnel** — il ne s'arme que si le lot touche `unity/` |
| **114** | retirer Hermes : ses demandes deviennent des fiches du registre |
| **115** | poser les deux gestes qui ne sont pas du code — protection de `master`, Pages → GitHub Actions |
| **116** | **preuve V1-1** : un lot traverse le cycle entier sans main humaine |

**Pourquoi Hermes disparaît.** Il existait pour porter une demande d'un dépôt
à l'autre, à recopier à la main. Quand `sim/` et Unity sont dans le même
dépôt, la demande n'a plus d'objet : c'est une fiche.

### Phase 4 — La V1 : lancer une simulation et l'afficher

| lot | ce qu'il fait | condition |
|---|---|---|
| **055** | dériver la population amorcée de ce que la cellule produit | **V1-2** |
| **117** | un palier de couche 1 : le monde tient 365 ticks sans s'effondrer | V1-2 |
| **118** | la carte 3D colorie une donnée du monde, pas seulement le relief | **V1-3** |
| **119** | une commande unique : simuler → photographier → rendre carte et planche | V1-3 |
| **120** | les trois vues lisent le même snapshot, et le prouvent | **V1-4** |
| **121** | le palier de V1 : les quatre conditions vertes ensemble | toutes |

Le lot **118** est celui qui n'existe nulle part aujourd'hui. Il demande de
choisir une donnée (population, faim, stock, part du bourg), une échelle de
couleur qui reste lisible en relief, et une preuve qui puisse rougir — un
rendu sans GPU doit refuser proprement, pas rendre du gris.

### Phase 5 — Après la V1 : subdiviser la cellule

*Le lot pivot. Il ouvre la couche 2 pour de bon.*

Une cellule fait 11 186 km² : c'est une région, pas un lieu. Le jeu doit
pouvoir en ouvrir **un**.

| lot | ce qu'il fait |
|---|---|
| **122** | la cellule se peuple de **lieux**, dérivés de façon déterministe de `cell_id` et d'une graine |
| **123** | un lieu porte une identité stable, matérialisée seulement quand on le regarde |
| **124** | la distribution intérieure cesse d'être gratuite : le transport entre lieux coûte |
| **125** | réconcilier `MODELE.md` et le contrat v1 sur cette identité |
| **126** | le snapshot porte les lieux ; les trois vues les montrent |

**Ce que ça coûte, dit franchement** : ce lot touche la carte, le tick, les
trois vues et le contrat. Il rouvre la gratuité que `MODELE.md` nomme
aujourd'hui — « la campagne nourrit son bourg sans transport, sans perte et
sans délai ». C'est là qu'il faut revenir, et le moment est venu.

### Phase 6 — Le pont ville

Le contrat existe. Il lui manquait un producteur — et une identité, que la
phase 5 vient de lui donner.

| lot | ce qu'il fait |
|---|---|
| **210** | l'adaptateur factice : éprouver transport, ordre et refus contre un backend en mémoire |
| **211** | l'adaptateur réel : `sim/` devient l'autorité que consomme Unity |
| **212** | la première ville intégrée : entrer depuis la carte, jouer, sortir, voir les conséquences |
| **216** | l'agrégation LOD conservative — rien ne se crée, rien ne se perd en changeant d'échelle |
| **217** | la synchronisation carte ↔ ville |

### Phase 7 — Le jeu

Hors de ce document. La couche 3 (États) est celle qui rend l'ascension
jouable : sans État, un seigneur qui réussit n'a nulle part où monter.

| couche | ce qu'elle apporte |
|---|---|
| 3 — **États** | fiscalité, lois, diplomatie, technologies, culture, religion |
| 4 — **Armées** | recrutement, logistique, ravitaillement, stratégie |
| 5 — **Batailles** | une couche de plus sur les **mêmes** données |

---

## La renumérotation

Les deux registres se chevauchent : ForgeHistory va de 033 à 055, CityLab de
001 à 040. Un seul registre exige des numéros uniques.

| tranche | pour quoi | règle |
|---|---|---|
| **001–099** | moteur, vues, chaîne | les lots ForgeHistory **gardent leur numéro** |
| **100–149** | la fusion elle-même | numéros neufs |
| **200–249** | ville, assets, Unity | les lots CityLab **prennent +200** |

Ainsi CityLab 004 → **204**, 010 → **210**, 040 → **240** : la correspondance
est immédiate, et rien ne se perd en route.

---

## Le registre — tout ce qui reste à faire

> Format : `### [NNN — Titre](briefs/NNN-slug.md)` puis
> `état · couche · dépend de · PR`, et une ligne vide après. **L'ordre est la
> priorité** : parmi les lots prêts, le premier de la liste part le premier.

<!-- lots:debut -->

## Phase 0 — débloquer

### [100 — Le registre accepte le brief orphelin](briefs/100-registre-brief-orphelin.md)
état : a-briefer · couche : — · dépend de : — · PR : 248
note : `master` est rouge tant que ce lot n'est pas entré. Il bloque les cinq suivants.

### [101 — La pile en vol entre dans master](briefs/101-pile-en-vol.md)
état : a-briefer · couche : — · dépend de : 100 · PR : 241, 242, 245, 246, 249

## Lots ForgeHistory en vol — repris tels quels

### [049 — Fabriquer : le minerai devient un objet](briefs/049-fabriquer-le-minerai-devient-un-objet.md)
état : pret · couche : 2 · dépend de : 044 · PR : 242
note : code écrit et testé, 9 nouveaux cas verts. Attend seulement la porte.

### [051 — Le snapshot photographie le bourg](briefs/051-le-snapshot-photographie-le-bourg.md)
état : pret · couche : 2 · dépend de : 047 · PR : 241

### [052 — Le regard mince montre le bourg](briefs/052-le-regard-mince-montre-le-bourg.md)
état : pret · couche : 2 · dépend de : 051 · PR : 249

### [053 — Le monde porte sa date](briefs/053-le-monde-porte-sa-date.md)
état : pret · couche : 1 · dépend de : — · PR : 245
note : 30 tests neufs verts, parité exacte des trois régimes de production.

### [054 — Cohérence globale : inventaire du produit face à la vision](briefs/054-coherence-globale-inventaire-produit-vision.md)
état : pret · couche : — · dépend de : — · PR : 246

### [055 — Le monde nourrit ceux qu'il amorce](briefs/055-le-monde-nourrit-ceux-qu-il-amorce.md)
état : pret · couche : 1 · dépend de : — · PR : —
note : **condition V1-2**. Plafond mesuré 0,691 ; 86 % du monde meurt la première année.

## Phase 1 — extraire ForgeAtelier

### [102 — ForgeAtelier devient un dépôt](briefs/102-forgeatelier-depot.md)
état : idee · couche : — · dépend de : — · PR : —

### [103 — Les adaptations CityLab rentrent dans l'atelier](briefs/103-atelier-adaptations-citylab.md)
état : idee · couche : — · dépend de : 102 · PR : —

### [104 — L'atelier est une dépendance épinglée](briefs/104-atelier-dependance.md)
état : idee · couche : — · dépend de : 103 · PR : —

## Phase 2 — fonder le dépôt

### [105 — Le dépôt naît : moteur, données, vues](briefs/105-fonder-le-depot.md)
état : idee · couche : — · dépend de : 101, 104 · PR : —

### [106 — Unity, la fabrique et le contrat entrent](briefs/106-importer-unity.md)
état : idee · couche : — · dépend de : 105 · PR : —

### [107 — Un seul jeu de règles](briefs/107-un-seul-agents.md)
état : idee · couche : — · dépend de : 106 · PR : —

### [108 — Un seul registre, renuméroté](briefs/108-un-seul-registre.md)
état : idee · couche : — · dépend de : 107 · PR : —

### [109 — forge3d est une dépendance, pas du source](briefs/109-forge3d-dependance.md)
état : idee · couche : — · dépend de : 105 · PR : —

### [110 — Les anciens dépôts passent en archive](briefs/110-archiver-les-anciens.md)
état : idee · couche : — · dépend de : 108 · PR : —

## Phase 3 — une seule chaîne

### [111 — Un atelier.toml, un préfixe](briefs/111-une-seule-chaine.md)
état : idee · couche : — · dépend de : 108 · PR : —

### [112 — Les contrôles unifiés](briefs/112-controles-unifies.md)
état : idee · couche : — · dépend de : 111 · PR : —

### [113 — Le worker Unity est un contrôle conditionnel](briefs/113-worker-unity-conditionnel.md)
état : idee · couche : — · dépend de : 112 · PR : —

### [114 — Hermes se retire : une demande est une fiche](briefs/114-retirer-hermes.md)
état : idee · couche : — · dépend de : 111 · PR : —

### [115 — Les deux gestes qui ne sont pas du code](briefs/115-protection-et-pages.md)
état : idee · couche : — · dépend de : 112 · PR : —

### [116 — Un lot traverse le cycle entier](briefs/116-preuve-du-cycle.md)
état : idee · couche : — · dépend de : 113, 115 · PR : —
note : **condition V1-1**. La fusion, le rejeu d'une PR en retard et le dépôt d'un palier n'ont jamais été joués en ligne.

## Phase 4 — la V1

### [117 — Palier de couche 1 : le monde tient une année](briefs/117-stabilisation-couche-1.md)
état : idee · couche : 1 · dépend de : 055 · PR : —

### [118 — La carte 3D colorie une donnée du monde](briefs/118-carte-de-statistique.md)
état : idee · couche : — · dépend de : 051 · PR : —
note : **condition V1-3**. N'existe nulle part aujourd'hui.

### [119 — Une commande : simuler, photographier, rendre](briefs/119-une-commande.md)
état : idee · couche : — · dépend de : 118 · PR : —

### [120 — Les trois vues sur le même snapshot](briefs/120-trois-vues-un-snapshot.md)
état : idee · couche : — · dépend de : 052, 119 · PR : —
note : **condition V1-4**.

### [121 — Le palier de V1](briefs/121-stabilisation-v1.md)
état : idee · couche : — · dépend de : 116, 117, 119, 120 · PR : —

## Phase 5 — subdiviser la cellule

### [122 — La cellule se peuple de lieux](briefs/122-la-cellule-se-subdivise.md)
état : idee · couche : 2 · dépend de : 121 · PR : —
note : le lot pivot. Il touche la carte, le tick, les trois vues et le contrat.

### [123 — Un lieu porte une identité stable](briefs/123-identite-du-lieu.md)
état : idee · couche : 2 · dépend de : 122 · PR : —

### [124 — La distribution intérieure cesse d'être gratuite](briefs/124-transport-interieur.md)
état : idee · couche : 2 · dépend de : 122 · PR : —

### [125 — Le modèle et le contrat se réconcilient](briefs/125-reconcilier-modele-contrat.md)
état : idee · couche : 2 · dépend de : 123 · PR : —

### [126 — Le snapshot porte les lieux](briefs/126-snapshot-des-lieux.md)
état : idee · couche : 2 · dépend de : 123 · PR : —

## Phase 6 et au-delà — la ville (ex-CityLab, +200)

### [204 — Vider la dette matière des orphelins](briefs/204-dette-matiere-orphelins.md)
état : pret · couche : — · dépend de : 202, 203 · PR : —

### [205 — Planche de contact C-10](briefs/205-planche-contact-c10.md)
état : pret · couche : — · dépend de : 203 · PR : —
note : jugement artistique humain ; aucune mesure technique ne le remplace.

### [206 — Carte MetallicGloss empilée](briefs/206-metallic-gloss-pack.md)
état : pret · couche : — · dépend de : 204, 208 · PR : —

### [207 — Ornement sous budget](briefs/207-ornement-sous-budget.md)
état : pret · couche : — · dépend de : 203 · PR : —

### [208 — Preuve Unity Windows](briefs/208-preuve-unity-windows.md)
état : pret · couche : — · dépend de : 203 · PR : —

### [209 — Approbation artistique Factory](briefs/209-approbation-artistique-factory.md)
état : pret · couche : — · dépend de : 205, 208 · PR : —

### [210 — Adaptateur snapshot/intention factice](briefs/210-adaptateur-sim-factice.md)
état : pret · couche : 1 · dépend de : — · PR : —

### [211 — Adaptateur de simulation réel](briefs/211-adaptateur-sim-reel.md)
état : idee · couche : 2 · dépend de : 210, 125 · PR : —

### [212 — Première ville intégrée](briefs/212-premiere-ville-integree.md)
état : idee · couche : 3 · dépend de : 211 · PR : —

### [213 — Usure, réparation et démolition](briefs/213-usure-reparation-demolition.md)
état : idee · couche : 3 · dépend de : 212 · PR : —

### [214 — Bible artistique](briefs/214-bible-artistique.md)
état : idee · couche : 3 · dépend de : 212 · PR : —

### [215 — Environnement et population](briefs/215-environnement-population.md)
état : idee · couche : 3 · dépend de : 214 · PR : —

### [216 — Agrégation LOD](briefs/216-aggregation-lod.md)
état : idee · couche : 2 · dépend de : 212 · PR : —

### [217 — Synchronisation carte-ville](briefs/217-synchronisation-carte-ville.md)
état : idee · couche : 2 · dépend de : 216 · PR : —

### [218 — Multi-ville](briefs/218-multi-ville.md)
état : idee · couche : 3 · dépend de : 217 · PR : —

### [219 — Streaming et cache](briefs/219-streaming-cache.md)
état : idee · couche : 3 · dépend de : 218 · PR : —

### [220 — Sauvegarde monde](briefs/220-sauvegarde-monde.md)
état : idee · couche : 2 · dépend de : 217 · PR : —

### [221 — Foyers et cycle de vie](briefs/221-foyers-cycle-vie.md)
état : idee · couche : 2 · dépend de : 212 · PR : —

### [222 — Santé et maladies](briefs/222-sante-maladies.md)
état : idee · couche : 2 · dépend de : 221 · PR : —

### [223 — Foi et sépulture](briefs/223-foi-sepulture.md)
état : idee · couche : 2 · dépend de : 221 · PR : —

### [224 — Ordre et criminalité](briefs/224-ordre-criminalite.md)
état : idee · couche : 2 · dépend de : 221 · PR : —

### [225 — Fiscalité et trésor](briefs/225-fiscalite-tresor.md)
état : idee · couche : 3 · dépend de : 221 · PR : —
note : premier lot de la couche « États » — c'est par lui que l'ascension devient jouable.

### [226 — Incendies et catastrophes](briefs/226-incendies-catastrophes.md)
état : idee · couche : 2 · dépend de : 221 · PR : —

### [227 — Parcours de huit heures](briefs/227-parcours-huit-heures.md)
état : idee · couche : 3 · dépend de : 212, 217, 220 · PR : —

### [228 — Tutoriel carte-ville](briefs/228-tutoriel-carte-ville.md)
état : idee · couche : 3 · dépend de : 227 · PR : —

### [229 — Sauvegarde robuste](briefs/229-sauvegarde-robuste.md)
état : idee · couche : 2 · dépend de : 220 · PR : —

### [230 — Contenu urbain](briefs/230-contenu-urbain.md)
état : idee · couche : 3 · dépend de : 212, 214 · PR : —

### [231 — Art final](briefs/231-art-final.md)
état : idee · couche : 3 · dépend de : 214, 215, 230 · PR : —

### [232 — Animations de production](briefs/232-animations-production.md)
état : idee · couche : 3 · dépend de : 215 · PR : —

### [233 — Audio de ville](briefs/233-audio-ville.md)
état : idee · couche : 3 · dépend de : 214 · PR : —

### [234 — UX et lisibilité](briefs/234-ux-lisibilite.md)
état : idee · couche : 3 · dépend de : 227 · PR : —

### [235 — Performance à 500 habitants](briefs/235-performance-500.md)
état : idee · couche : 3 · dépend de : 219, 227 · PR : —

### [236 — Équilibrage backend](briefs/236-equilibrage-backend.md)
état : idee · couche : 2 · dépend de : 212 · PR : —

### [237 — Régression intégrée](briefs/237-regression-integree.md)
état : idee · couche : 3 · dépend de : 227, 229, 235 · PR : —

### [238 — Accessibilité](briefs/238-accessibilite.md)
état : idee · couche : 3 · dépend de : 234 · PR : —

### [239 — Localisation français-anglais](briefs/239-localisation-fr-en.md)
état : idee · couche : 3 · dépend de : 228, 234 · PR : —

### [240 — Packaging City Mode](briefs/240-packaging-city-mode.md)
état : idee · couche : 3 · dépend de : 231, 232, 233, 236, 237, 238, 239 · PR : —

<!-- lots:fin -->

**Compte** : 6 lots ForgeHistory en vol ou prêts, 27 lots de fusion neufs,
37 lots ex-CityLab. **70 fiches**, dont 13 prêtes à partir dès que la chaîne
tourne.

---

## Les risques, nommés

| risque | ce qui arrive si on l'ignore | la contre-mesure |
|---|---|---|
| **fusionner du rouge** | on copie un registre incohérent, et la chaîne neuve naît bloquée | phase 0 avant tout le reste ; rien ne se copie tant que `feuille valider` refuse |
| **055 repoussé** | la V1 affiche un effondrement démographique et on croit que c'est le jeu | 055 est une condition de V1, pas un lot parmi d'autres |
| **le lot pivot avancé trop tôt** | subdiviser la cellule avant que la chaîne tourne, c'est refaire à la main ce qu'on venait d'automatiser | 122 dépend de 121 : le palier de V1 d'abord |
| **forge3d vendorisé** | 227 Mo de source que personne du projet n'a écrit, et plus de `git pull` amont | 109 : dépendance épinglée, aucune ligne de son source n'entre |
| **deux registres qui cohabitent** | les numéros se chevauchent et l'atelier refuse tout | 108 renumérote en une fois, table de correspondance à l'appui |
| **la protection de branche oubliée** | une main fusionne du rouge — c'est déjà arrivé | 115, et la chaîne n'est pas déclarée complète sans elle |
| **la ville de 110 000 habitants** | on ouvre une « ville » à l'échelle d'une région, et rien n'est crédible | phase 5 : subdiviser avant d'ouvrir |
| **cinq heures pour une partie** | 36 s l'année × 500 ans ; supportable pour un serveur, pas pour un joueur | hors V1, mais à trancher avant la couche 3 |

---

## Ce que ce document ne fait pas

Il ne décrit **aucun** lot dans le détail : chaque fiche renvoie à un brief qui
reste à écrire, et le brief est la seule source d'instruction.

Il ne déclare **aucun** lot livré : seule sa fiche le dit, et seulement au
moment exact où la PR entre.

Il n'invente **aucune** date. La chaîne avance au rythme des lots qui passent
la porte, et l'ordre des fiches est la seule priorité qui existe.
