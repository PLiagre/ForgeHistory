# Brief 047 — Le bourg est une agrégation dérivée

**Authored**: 2026-08-30T13:40:00Z
**Author**: Claude
**Risque**: R1 — vue dérivée bornée dans `sim/aggregation.py`, sans changement du tick, sans migration de données, sans nouvelle clé spatiale.

## But unique

Donner au monde une **vue dérivée** qui répond à une seule question :
*combien d'habitants de cette cellule ne tirent pas leur nourriture de ses
champs ?* Ceux-là sont le **bourg** ; les autres sont la campagne qui les
nourrit.

C'est la première entité de la couche 2, et elle est construite exactement
comme la Province : elle se **recalcule**, elle ne s'estampille pas. Aucun
champ n'est ajouté à `Cell`, aucune seconde clé spatiale n'apparaît, et **le
tick ne la consulte pas**.

Ce lot ne crée **aucun mécanisme** : il ne change aucun nombre du monde, ne
déplace aucun kilogramme, ne fait naître ni mourir personne. C'est une lecture.
Si après ce lot `python -m sim` rend un résultat différent, le lot est faux.

Ce lot n'invente ni quartier, ni bâtiment, ni famille, ni personne, ni salaire,
ni marché, ni prix, ni route, ni métier nouveau.

## Dépendance

**Ce lot suppose le lot 044 (`un-metier-le-mineur`) fusionné.**

Le bourg se compte à partir de la **part non agricole** de la population. Le lot
044 est le premier — et aujourd'hui le seul — mécanisme du projet qui rend cette
part non nulle : il fait qu'une partie des habitants d'une cellule à gisement
cesse de cultiver pour extraire. Avant lui, tout le monde cultive, la part vaut
zéro partout, et **l'échantillon du bourg est vide**.

Un échantillon vide **échoue**, il ne passe pas en silence (règle 6, mode de
défaillance n° 6). Donc : **si le lot 044 n'est pas fusionné, ce lot est
bloqué.** Il n'est pas « à adapter », il ne se lance pas, et SC4 est le contrôle
mécanique qui le dit.

**Ce lot est indépendant du lot 046** (« la mer est un port commun ») et du lot
043. Le bourg de ce modèle est nourri par la campagne de sa **propre** cellule ;
il n'attend aucun transport, et aucun ordre n'est à respecter entre 046 et
celui-ci.

## Fondement dans le modèle

`sim/MODELE.md` :

- § « Ce qu'est une ville, à l'échelle d'une cellule » — la décision de modèle
  dont ce lot découle : pourquoi le bourg est une concentration *dans* la
  cellule et non une cellule entière, d'où vient la donnée, ce qui se refuse
  plutôt que se devine, et ce que cette lecture coûte ;
- § « La province dérivée et ses centres », sous-section « Ce que l'agrégation
  ne fait pas — et le motif que toute vue recopie » — le motif exact que cette
  vue reprend sans en changer une ligne ;
- § « L'extraction minière » — la part de population que le lot 044 détourne
  des champs, et qui est la seule source de ce que ce lot compte.

Si l'une de ces sections a changé depuis la rédaction de ce brief, la relire
avant de le lancer.

`sim/MODELE.md` est **hors périmètre** de ce lot. La mise à jour des sections
citées après fusion est une dette de l'architecte du modèle (Claude), pas de
l'exécutant.

## État de départ mesuré

Les commandes qui donnent l'état — à rejouer ; aucun de leurs résultats n'est
recopié ici comme cible :

```bash
grep -rn "bourg\|Bourg" sim/
grep -rn "part_miniere" sim/
.venv/bin/python -m sim --ticks 365 --seed 0 --json
.venv/bin/python -m pytest sim/tests/ -q
git log --oneline -1 --grep="044"
```

Le SHA de base du lot est le `master` du jour où il est lancé ; le mesureur
l'enregistre et compare contre lui.

**Le fait qualitatif qui rend ce lot caduc** : si `sim/` porte déjà une vue qui
distingue les habitants non agricoles d'une cellule, il n'y a rien à faire ici.

**Le fait qualitatif qui rend ce lot bloqué** : si aucune fonction de `sim/` ne
calcule une part non agricole de la population, le lot 044 n'est pas fusionné et
ce lot **ne se lance pas**. Ce n'est pas une condition à contourner en
fabriquant la part : c'est la dépendance, et SC4 la mesure.

**La suite est verte sur la base, et ce lot doit la laisser verte.** Elle ne
l'était plus depuis le lot 043 ; le micro-lot 043-bis l'a refermée le
2026-08-30 en donnant au monde d'épreuve de quoi exercer les deux chemins de
capacité. Voir `sim/MODELE.md`, § « Le monde d'épreuve, et pourquoi certaines
constantes se cachent ».

Ce lot n'introduit **aucune constante** et ne touche pas au moteur : il n'a
donc aucune raison de rouvrir ce contrôle. S'il le fait rougir, c'est qu'il a
débordé de son périmètre.

## Règle du monde

**Fidélité niveau 2.** La part de la population qu'un bourg représente est un
ordre de grandeur plausible, générée, jamais sourcée. Une répartition locale
surprenante n'est pas un défaut historique et n'ouvre ni correctif, ni brief.

Aucune donnée de carte nouvelle n'est lue : ce lot n'introduit **aucune**
constante et **aucun** paramètre. Il n'y a donc rien à calibrer, et c'est
voulu — une vue qui aurait son propre réglage serait une seconde vérité.

### Le mécanisme, en une ligne

```
habitants_du_bourg(cellule)   = int(population × part_non_agricole(cellule))
habitants_des_champs(cellule) = population − habitants_du_bourg(cellule)
```

La troncature est délibérée et **personne ne se perd** : la campagne est
définie comme *le reste*, donc la somme des deux vaut exactement la population,
pour toute cellule et sans arrondi à corriger.

**Il n'y a pas de report de fraction ici**, contrairement à la mortalité, à la
natalité et à la migration. Ces trois-là accumulent un état d'un tick sur
l'autre ; une vue ne s'accumule pas, elle se recalcule. Un reste conservé serait
un état stocké, c'est-à-dire exactement ce que ce lot est écrit pour ne pas
faire.

### La part non agricole se lit, elle ne se redérive pas

`part_non_agricole` **est** la fonction unique que le lot 044 installe dans
`sim/constants.py` — celle qui calcule la part de la population qu'un gisement
occupe, plafonnée. Ce lot l'**appelle**. Il n'en écrit pas une seconde version,
n'en recopie pas la formule, et ne duplique pas les facteurs de richesse.

Si le lot 044 a livré ce calcul sous un autre nom que celui que son brief
annonce, c'est **ce nom-là** qu'il faut lire : il n'y en a qu'un, et SC5 est le
contrôle qui vérifie qu'il n'y en a toujours qu'un après ce lot.

**Ce que le nom « bourg » promet, et ce qu'il ne promet pas.** Aujourd'hui la
seule façon de ne pas cultiver est de descendre à la mine : le bourg d'une
cellule est donc exactement ses mineurs. Le nom est délibérément plus large que
le mécanisme, pour que le jour où un second métier existera la vue le compte
sans être réécrite. **Cela n'autorise personne à inventer ce second métier
ici** : ce lot n'ajoute aucune source de population non agricole.

### Ce qui se refuse plutôt que se deviner

- une cellule dont la part non agricole vaut zéro a un bourg de **zéro
  habitant**. C'est une **mesure réelle** — la vue a regardé et n'a trouvé
  personne — et non une absence. La sentinelle « non calculé » du projet est
  `-1`, jamais `0` ;
- si **aucune** cellule du monde n'a de part non agricole, la vue ne rend pas
  « zéro bourg » en silence : c'est un échantillon vide, et il **échoue**
  (SC4) ;
- aucun seuil ne « fait » un bourg. Il n'y a pas de nombre d'habitants au-dessus
  duquel une cellule serait déclarée urbaine : le bourg n'est pas déclaré, il
  est **compté**.

## Source de vérité et raccord au moteur

La population vient de `world.cells`, la part non agricole de l'unique fonction
du lot 044. Rien d'autre n'est lu, et **rien n'est écrit** : ni sur les
cellules, ni sur disque, ni dans la carte.

La vue vit dans `sim/aggregation.py`, **hors de `sim.model`**, pour la raison
exacte qu'ADR-0003 donne à `Regroupement` : `sim.model` contient les entités
*persistées* que le moteur fait évoluer, et y déclarer le bourg inviterait à le
traiter comme un état stockable.

Elle recopie le motif de la Province, sans en changer une ligne :

- elle est **pure** — elle ne mute aucun objet reçu, et deux appels sur les
  mêmes entrées rendent le même résultat ;
- son enregistrement est **immuable** (`frozen`), et hérite de la garde
  `_NoBadSpatialField` déjà importée dans ce module ;
- elle est indexée par **`cell_id` et rien d'autre** ;
- son ordre de sortie est stable, par `cell_id` croissant ;
- **le tick ne la consulte pas.**

`sim/engine.py`, `sim/model.py`, `sim/constants.py` et `sim/world.py` ne sont
pas touchés. C'est ce périmètre étroit qui **prouve** que la vue ne décide rien :
si elle avait besoin de changer le moteur, elle ne serait pas une vue.

## Périmètre d'écriture

Fichiers produit autorisés :

- `sim/aggregation.py` ;
- `sim/tests/test_province.py`, uniquement pour **ajouter** les cas qui
  protègent cette règle visible — c'est le fichier qui porte déjà l'invariant
  « la vue est dérivée, jamais stockée » (ADR-0003). Aucun test déjà vert n'est
  modifié.

Livrables du lot autorisés :

- `harness/queue/briefs/047-le-bourg-est-une-agregation-derivee/deliverables/manifest.json` ;
- `harness/queue/briefs/047-le-bourg-est-une-agregation-derivee/deliverables/generator-log.md` ;
- `harness/queue/briefs/047-le-bourg-est-une-agregation-derivee/deliverables/measure_047.py` ;
- les sorties textuelles déterministes produites par ce mesureur dans le même
  dossier `deliverables/`.

Tout autre chemin est interdit. En particulier : ne modifier ni `sim/MODELE.md`,
ni `sim/engine.py`, ni `sim/model.py`, ni `sim/constants.py`, ni `sim/world.py`,
ni `sim/snapshot_export.py`, ni `sim/__main__.py`, ni `sim/tests/test_monde.py`,
ni `sim/tests/test_commerce.py`, ni `sim/tests/test_survie.py`, ni
`sim/tests/test_determinisme.py`, ni `sim/tests/test_write_coverage.py`, ni
`sim/tests/test_no_hardcoded.py`, ni la carte figée, ni le visualiseur, ni
l'outil de fabrication de la carte, ni le brief 044, ni le brief 046, ni le
harnais, ni ce brief, ni sa grille, ni un `verdict.md`.

## Conditions de succès

### SC1 — La vue se recalcule, et elle ne touche à rien

Deux appels successifs sur le même monde rendent un résultat **égal**, et le
monde en sort **inchangé** : aucun attribut n'est ajouté à une cellule, aucune
valeur de cellule n'est modifiée. L'empreinte de `world.to_dict()` avant et
après l'appel est identique.

Le nombre de cellules réellement comparées est dérivé du monde chargé ; un
échantillon vide échoue.

**Le rouge est prouvé avant la correction** : sur le SHA de base, la vue
n'existe pas et l'appel lève une erreur d'attribut.

### SC2 — Aucune seconde clé spatiale

Un contrôle parcourt l'arbre syntaxique des modules de `sim/` hors tests et
échoue si une entité déclare un champ dont le nom normalisé commence par
`bourg`, `ville` ou `city`. Le dénominateur est le nombre de classes de données
réellement découvertes, jamais une liste écrite à la main.

**Le rouge est prouvé** sur une entité d'épreuve délibérée, portant un tel
champ, exactement comme le contrôle existant le fait déjà pour le préfixe
`province` : le contrôle doit rougir sur elle, sinon il ne protège rien.

La garde d'exécution héritée de `sim/model.py` n'est **pas** étendue : elle
continue de ne connaître que `province`, et ce lot ne modifie pas ce fichier.
C'est le contrôle ci-dessus qui porte les nouveaux préfixes.

### SC3 — Le tick ne consulte pas la vue

Un contrôle parcourt l'arbre syntaxique de `sim/engine.py` et échoue si le
module importe `sim.aggregation` ou référence le nom de la vue. Le dénominateur
est le nombre de modules de `sim/` hors tests réellement parcourus.

C'est la même propriété que porte déjà la Province, et elle est ici la
définition même de ce lot : une vue lit, elle ne décide jamais.

### SC4 — L'échantillon n'est jamais vide, et c'est le signal de dépendance

Sur le monde réel, le compteur `cellules_avec_bourg` — le nombre de cellules
dont le bourg compte au moins un habitant — est **strictement positif**.

Un zéro ici **fait échouer le lot**. Il ne signifie pas « ce monde n'a pas de
ville » : il signifie que rien ne produit de part non agricole, c'est-à-dire que
le lot 044 n'est pas fusionné. C'est la déclaration mécanique de la dépendance,
et il est **interdit** de la contourner en fabriquant une part non agricole dans
ce lot.

Le dénominateur est le nombre de cellules du monde chargé. Le mesureur rapporte
aussi le nombre de cellules que la carte déclare porteuses d'au moins un
gisement, dérivé du fichier, pour que les deux comptes se confrontent.

### SC5 — Le bourg suit la part non agricole, et il n'y a qu'une définition

Deux propriétés, mesurées ensemble :

- **l'ordre.** À population égale, le bourg d'une cellule dont le gisement est
  de richesse majeure compte strictement plus d'habitants que celui d'une
  cellule de richesse notable, lui-même strictement plus que celui d'une
  cellule de richesse mineure. Les trois classes sont **dérivées de la carte** ;
  si l'une manque, le contrôle échoue au lieu de la sauter ;
- **l'unicité.** Un contrôle parcourt l'arbre syntaxique des modules de `sim/`
  hors tests et échoue si plus d'une fonction calcule la part non agricole, ou
  si un second jeu de facteurs de richesse apparaît. Le dénominateur est le
  nombre de modules réellement parcourus.

Ce second point **se compose** avec SC4 du lot 044, qui exige déjà une
définition unique de la part minière : ce lot ne revendique pas cette
grandeur, il la lit, et le contrôle vérifie qu'il ne l'a pas dupliquée en la
lisant.

### SC6 — La somme est exacte, pour chaque cellule

Pour **toute** cellule du monde chargé :
`habitants_du_bourg + habitants_des_champs == population`, à l'entier près et
sans tolérance. Le compteur d'écarts vaut **0**, et ce zéro est une mesure
réelle : chaque cellule a été sommée.

Le dénominateur est le nombre de cellules réellement sommées ; un échantillon
vide échoue.

### SC7 — Ce lot ne change aucun nombre du monde

`.venv/bin/python -m sim --ticks 365 --seed 0 --json` rend, après changement,
une sortie **identique octet pour octet** à celle rejouée sur le SHA de base.

Le mesureur archive la sortie de base **avant** l'édition, la relit, et compare
les empreintes SHA-256. Le compteur `ecart_octets_sortie_cli` vaut **0** — une
mesure réelle, obtenue en comparant deux fichiers réellement produits, jamais
une affirmation.

C'est le critère qui distingue une vue d'un mécanisme. S'il rougit, ce lot a
touché au monde, et il est faux quelles que soient ses autres mesures.

Aucune paire `must_differ_from_git` n'est déclarée par ce lot : rien de ce qu'il
produit ne doit différer de la base, et la porte mécanique n'a pas de clé
inverse. La preuve passe donc par ce compteur, pas par une clé de manifeste.

### SC8 — Les invariants existants restent intacts, et la suite reste verte

- `.venv/bin/python -m pytest sim/tests/ -q` est **vert**, comme sur le SHA de
  base. La liste des tests en échec après changement est **vide**, et elle est
  comparée à celle rejouée sur la base plutôt que supposée ;
- tous les contrôles de `sim/tests/test_province.py` déjà présents restent
  verts **sans modification**, y compris `test_province_couverture_totale_monde_reel`,
  `test_province_aucun_champ_province_sur_entites` et
  `test_province_garde_prefixe_variantes_rouges` ;
- `sim/tests/test_write_coverage.py` reste vert sans modification : ce lot
  n'ajoute aucun champ à une entité de `sim.model`, donc son dénominateur ne
  bouge pas ;
- `test_no_hardcoded_numeric_literals` reste vert : ce lot n'introduit aucun
  littéral numérique hors 0, 1 et −1 ;
- `test_aucune_constante_terminale` reste vert : ce lot n'ajoute aucune
  constante ;
- deux exécutions de `.venv/bin/python -m sim --ticks 365 --seed 0 --json` sont
  strictement identiques entre elles ;
- le nombre de tests collectés dans `sim/tests/` est au moins celui du SHA de
  base.

## Compteurs exigés

Le mesureur `deliverables/measure_047.py` reconstruit chaque compteur ; il ne
porte aucun résultat en dur.

| compteur | source d'échantillon | dénominateur dérivé |
|---|---|---|
| `cellules_du_monde` | chargement de la carte figée | nombre de cellules du fichier |
| `cellules_avec_gisement_carte` | agrégation des `gisements` rendus par la carte | `cellules_du_monde` |
| `cellules_avec_bourg` | vue calculée sur le monde chargé, bourg strictement positif | `cellules_du_monde` |
| `habitants_du_bourg_total` | somme de la vue sur toutes les cellules | population totale réellement sommée |
| `ecarts_somme_bourg_champs` | comparaison bourg + champs contre population, cellule par cellule | nombre de cellules réellement sommées |
| `appels_identiques_sur_meme_monde` | deux appels successifs comparés | nombre d'appels réellement effectués |
| `empreintes_monde_avant_apres_vue` | `world.to_dict()` avant et après l'appel de la vue | nombre de cellules réellement sérialisées |
| `champs_de_cle_spatiale_interdits` | parcours de l'arbre syntaxique des modules de `sim/` hors tests | nombre de classes de données réellement découvertes |
| `rouge_garde_prefixe_declenche` | entité d'épreuve portant un champ interdit | nombre de préfixes réellement essayés |
| `references_a_la_vue_dans_engine` | parcours de l'arbre syntaxique de `sim/engine.py` | nombre de modules de `sim/` hors tests parcourus |
| `definitions_de_part_non_agricole` | même parcours, sur tous les modules | nombre de modules réellement parcourus |
| `jeux_de_facteurs_de_richesse` | même parcours | nombre de modules réellement parcourus |
| `bourgs_ordonnes_par_richesse` | une cellule par classe de richesse, dérivée de la carte | nombre de classes réellement mesurées |
| `ecart_octets_sortie_cli` | empreintes SHA-256 des sorties CLI avant et après | nombre d'exécutions réellement lancées |
| `tests_en_echec_avant` | collecte pytest sur le SHA de base | nombre de tests collectés |
| `tests_en_echec_apres` | collecte pytest après changement | nombre de tests collectés |
| `tests_collectes_avant` | collecte pytest sur le SHA de base | nombre de fichiers de test collectés |
| `tests_collectes_apres` | collecte pytest après changement | nombre de fichiers de test collectés |

Contraintes sur ces compteurs, toutes vérifiables :

- `cellules_avec_bourg` est **strictement positif**. Un zéro fait échouer le
  lot et signifie que le lot 044 n'est pas fusionné (SC4) ;
- `habitants_du_bourg_total` est strictement positif et strictement inférieur à
  la population totale ;
- `ecarts_somme_bourg_champs`, `references_a_la_vue_dans_engine`,
  `champs_de_cle_spatiale_interdits` et `ecart_octets_sortie_cli` valent **0**.
  Ces zéros sont des **mesures réelles** ; la sentinelle « non calculé » du
  projet est `-1`, jamais `0` ;
- `definitions_de_part_non_agricole` et `jeux_de_facteurs_de_richesse` valent
  **1** ;
- `rouge_garde_prefixe_declenche` est égal au nombre de préfixes essayés, et
  strictement positif : un contrôle qui ne peut pas rougir ne prouve rien ;
- `appels_identiques_sur_meme_monde` est égal au nombre d'appels effectués ;
- `empreintes_monde_avant_apres_vue` sont identiques ;
- `tests_en_echec_avant` et `tests_en_echec_apres` valent **0**, et les deux
  listes de noms sont vides. Ces zéros sont des mesures réelles, obtenues en
  jouant la suite deux fois ;
- `tests_collectes_apres` est au moins `tests_collectes_avant`.

## Livrables et porte mécanique

Le dossier `deliverables/` contient au minimum :

- `manifest.json`, avec les commandes exactes et les compteurs ci-dessus ;
- `generator-log.md`, en français clair : le rouge prouvé de SC1 et de SC2 avant
  correction, la vérification que le lot 044 était bien fusionné au démarrage,
  les fichiers modifiés, les commandes jouées, les résultats et les limites —
  dont la limite de fidélité déclarée par le modèle : la campagne nourrit son
  bourg sans transport ;
- `measure_047.py`, rejouable depuis la racine avec `.venv/bin/python`.

Les chemins du manifeste sont relatifs au dossier du brief.

## Hors périmètre

- `sim/MODELE.md` (dette de l'architecte après fusion) ;
- **tout mécanisme** : ce lot ne change aucun nombre du monde, et SC7 le mesure ;
- tout métier autre que celui du lot 044, et toute nouvelle source de population
  non agricole ;
- les quartiers, les bâtiments, les familles, les personnes ;
- le salaire, le prix, le marché, la propriété, la classe sociale, la fiscalité ;
- l'attraction urbaine, une migration qui viserait le bourg, une natalité
  différente au bourg et aux champs ;
- la nourriture du bourg comme flux distinct : la campagne de la cellule le
  nourrit sans transport, c'est une limite déclarée du modèle et non un
  mécanisme à écrire ici ;
- les routes, la mer et le lot 046 ;
- le schéma du snapshot, sa version, le visualiseur et Unity ;
- `sim/tests/test_write_coverage.py` et `_MondeEpreuve`, que le micro-lot
  043-bis vient de reprendre ;
- calibration d'un test existant après observation ;
- architecture, sécurité, CI, ForgePilot et fusion.

## Interdictions pour l'exécutant

L'exécutant n'écrit pas de `verdict.md`, ne modifie ni ce brief ni
`eval-rubric.md`, ne juge pas son propre travail, ne fusionne rien et ne pousse
pas directement sur `master`.
