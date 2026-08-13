# Verdict — Brief `017` : Le seuil de survie honnête

**Authored**: 2026-08-13T21:40:00Z
**Author**: forge-evaluateur

---

## Note de transparence

Le rôle déclaré en en-tête (`forge-evaluateur`) est le rôle natif du harnais.
L'acteur réel est un sous-agent Cursor Cloud (Claude Opus `5`) qui remplace le
CTO Claude. Cette session est **distincte** de celle du Générateur (également
Opus `5`, autre sous-agent, autre passe) et de celle du Planificateur : aucun
état, aucun raisonnement, aucun fichier de travail n'est partagé.

Je n'ai suggéré aucun correctif pendant la production. Je n'évalue donc pas
mon propre travail.

Je n'ai modifié **aucune ligne** du dépôt hors le présent fichier de jugement.
Toutes mes contre-preuves ont été montées dans des copies hors dépôt, sous
`/tmp/eval-017/`. Vérifiable : `git status --porcelain` ne montre, à l'issue de
mon travail, que `verdict.md`.

**Périmètre jugé** : le commit du Générateur `fece293` sur la branche
`forge/017-seuil-survie-honnete-ba01`. Le commit du Planificateur (`cc549e9`,
qui a écrit `brief.md`, `eval-rubric.md` et l'encadré de pointage des graines
`015` et `016`) n'est pas jugé ici : il n'est pas le travail du Générateur.

---

## 1. Résultat de la porte mécanique

Commande rejouée :
`.venv/bin/python harness/verdict_audit.py harness/queue/briefs/017-sim-seuil-survie-honnete`

Au moment où je l'ai exécutée, `verdict.md` n'existait pas encore. Le rapport
sort en `VERDICT: REJECT` (code de sortie `1`) sur exactement deux contrôles,
et ces deux-là seulement :

| contrôle | état avant ce fichier | motif |
|---|---|---|
| `files_declared_exist` | PASS | — |
| `mtime_after_brief` | PASS | — |
| `captures_differ_when_should` | PASS | les deux paires rouge/vert diffèrent |
| `waivers_have_command_and_error` | PASS | — |
| `no_empty_sample_pass` | PASS | aucun `sample_size` nul |
| `verdict_numbers_traceable` | **FAIL** | `verdict.md` absent — c'est le présent fichier |
| `no_bare_python_alias` | PASS | — |
| `verdict_is_not_self_authored` | **FAIL** | en-tête `Author` absente faute de `verdict.md` |
| `rubric_predates_deliverables` | PASS | la rubrique précède le plus ancien livrable |
| `declared_files_are_tracked` | PASS | les livrables du dossier sont suivis par git |

Les deux échecs sont **définitionnels** : ils constatent l'absence du fichier
que je suis en train d'écrire. Aucun contrôle de forme portant sur le travail
du Générateur n'est rouge. Ce n'est donc pas un REJECT mécanique de fond, et je
poursuis l'examen de substance — mais **le présent verdict n'est recevable
qu'après un rejeu de la porte renvoyant `VERDICT: ACCEPT`**. Ce rejeu appartient
à l'orchestrateur, pas à moi.

---

## 2. Ce que j'ai reconstruit moi-même

Je n'ai repris aucune valeur du manifeste ni du journal. Pour chaque compteur
j'ai écrit mon propre script hors dépôt (`/tmp/eval-017/recon_a.py` à
`recon_e.py`) ou rejoué la commande depuis la racine. Résumé :

| compteur du manifeste | reconstruit par moi | concordance |
|---|---|---|
| `N_STAT_SURVIE` | recalcul à la main de `max(plancher, ceil(période / plafond))` | identique |
| `SURVIE_FRACTION_PREDITE_STATIONNAIRE` | recalcul complet à la main des deux termes | identique au bit près |
| `SURVIE_TOLERANCE_STATIONNAIRE` | recalcul à la main du produit des trois facteurs | identique au bit près |
| `SURVIE_CONVERGENCE_DELTA` | recalcul à la main | identique au bit près |
| `SURVIE_TOLERANCE_SENSIBILITE` | recalcul à la main | identique au bit près |
| `fraction_survie_dans_tolerance_stationnaire` | monde G3 rejoué, horizon complet puis moitié | identique |
| `sensibilite_hds_05_passe` | trois régimes rejoués à `200` ticks | identique |
| `sensibilite_hds_2_passe` | idem | identique |
| `sensibilite_drr_direction_passe` | comparaison des deux prédictions | identique |
| `famine_tue_cellule_5hab` | cellule construite à la main, boucle de mortalité | identique |
| `mortalite_precision_n_ticks` | trois cellules rejouées, somme exacte recalculée | identique |
| `hunger_ticks_cellule_ravitaillee` | monde témoin/source/receveuse rebâti, tick complet | identique |
| `deficit_reduction_infinitesimal` | appel direct à la consommation | identique |
| `deficit_reduction_proportionnel` | idem | identique |
| `invariant_reduction_bornee_cas_testes` | six surplus rejoués | identique |
| `cellules_affamees_monde_reel_017` | script de mesure rejoué depuis la racine | identique |
| `morts_cumules_monde_reel_017` | idem, et recalculé dans mon propre script | identique |
| `kg_transportes_monde_reel_017` | script de mesure rejoué | identique |
| `fraction_survie_monde_reel_017` | recalculé dans mon propre script | identique |

Aucun compteur n'a résisté à la reconstruction, et aucun n'a dû être pris pour
argent comptant.

---

## 3. Verdict ligne à ligne de la rubrique

| Condition de succès | Verdict | Preuve que j'ai produite moi-même |
|---|---|---|
| **SC1** — la prédiction est une expression, pas un littéral | PASS | Analyse de l'arbre syntaxique de `sim/constants.py` : les cinq constantes du modèle et `N_BOUND_MORT` sont des nœuds `Call`, jamais `Constant`. |
| **SC1** — dépend de `HUNGER_DEATH_SCALE` à l'exécution | PASS | Remplacement de la constante en mémoire puis appel de `compute_survie_fraction_predite_stationnaire()` : la prédiction baisse quand on double, monte quand on divise par deux, et revient exactement à sa valeur d'origine après restauration. Voir aussi § `5` sur le point `3` de la rubrique. |
| **SC1** — signe du successeur de `DEFICIT_RECOVERY_RATE_PER_TICK` | PASS | `DEFICIT_RECOVERY_RATE_PER_SURPLUS_KG` doublée fait **monter** la prédiction. La contradiction de signe dénoncée par l'audit est corrigée. |
| **SC1** — signe de `FOOD_PRODUCTION_KG_PER_KM2_PER_TICK` | PASS | Doublée, la prédiction monte franchement. |
| **SC1** — documentation dans `sim/SEEDING.md` AVANT toute mesure | PASS avec réserve `N5` | Les sections `SC1` à `SC5` du brief `017` de `sim/SEEDING.md` ne citent **aucune** valeur mesurée du monde réel : ni fraction de survie, ni population, ni nombre de cellules affamées. Toutes les tolérances y sont données en forme fermée à partir des constantes. |
| **SC1** — horizon `≥ 1000`, justifié avant mesure | PASS | Recalcul à la main de la période d'oscillation et de l'horizon dérivé : je retrouve la valeur du manifeste, et le plancher domine bien le terme dérivé. |
| **SC1** — bornes des constantes | PASS | Tolérance stationnaire dans `(0.0, 0.5)`, delta de convergence dans `(0.0, 0.1)`, prédiction dans `(0.0, 1.0)` : les trois vérifiées par recalcul. |
| **SC1** — test de conformité : convergence ET tolérance | PASS | Monde `World.from_g3(rng_seed=42)`, `random.Random(42)`, rejoué à l'horizon complet puis à la moitié. Dérive et écart au modèle reproduits à l'identique, tous deux sous leur borne. |
| **SC1** — indépendance à l'horizon (exigence du Fil A) | PASS, et c'est le point fort du lot | J'ai poussé la simulation bien au-delà de l'horizon exigé (jalons à `250`, `500`, `1000`, `2000`, `4000` et `8000` ticks). La fraction mesurée se stabilise, la dérive entre jalons successifs décroît de façon monotone, et l'écart au modèle plafonne loin sous la tolérance. Le défaut du brief `013` — vert à court horizon, rouge à long horizon — n'est pas repoussé plus loin : il est réellement éteint. |
| **SC2** — direction concordante sur trois régimes | PASS | Mesure et prédiction décroissent toutes deux quand la mortalité par faim augmente. Reproduit indépendamment, sans passer par le test. |
| **SC2** — tolérance respectée dans chaque régime | PASS avec réserve `N3` | Les trois écarts sont sous la tolérance de sensibilité. |
| **SC2** — la tolérance est une expression, documentée avant mesure | PASS | Nœud `Call` dans l'arbre syntaxique, recalculée à la main, documentée dans `sim/SEEDING.md` sans citation de mesure. |
| **SC2** — le test peut vraiment rougir (paire A) | PASS | **Contre-preuve montée par moi** : copie hors dépôt, `HUNGER_DEATH_SCALE` remplacée par un littéral dans la formule de prédiction. Le test `test_sensibilite_hds` tombe en `FAILED` avec le bon message (« la prédiction ne répond pas »), les trois prédictions devenant identiques pendant que les trois mesures continuent de bouger. |
| **SC3** — champ `mortality_remainder`, sentinelle | PASS | Cellule construite sans argument : la valeur par défaut est bien la sentinelle négative, pas un zéro qui se ferait passer pour une mesure. |
| **SC3** — formule avec report persisté | PASS | Lecture de `_apply_mortality` : le reste est relu, ajouté, tronqué, et la fraction résiduelle est réécrite sur la cellule. Aucune troncature nue ne subsiste. |
| **SC3** — cellule de `5` habitants, `≥ 1` mort en `≤ N_BOUND_MORT` | PASS | Reconstruction tick par tick : le reste monte à la moitié d'une mort au premier tick, et la première mort tombe au **deuxième** tick, très en deçà de la borne. J'ai aussi calculé le contre-modèle arithmétique sans report : la cellule reste intacte sur toute la borne. |
| **SC3** — précision sur l'horizon | PASS avec réserve `N4` | Trois cellules rejouées : l'écart maximal entre morts appliqués et somme exacte est celui du manifeste, très en dessous de la mort entière exigée, et il est exactement égal à la fraction encore en attente. |
| **SC3** — le test peut vraiment rougir (paire B) | PASS | **Contre-preuve montée par moi** : copie hors dépôt, retour à la troncature nue. Les deux tests de `test_mortalite_accumulateur.py` tombent en `FAILED`, avec deux cellules sur trois qui ne perdent **aucun** habitant sur tout l'horizon. La marge de rougeur est massive, pas marginale. |
| **SC4** — le critère n'est plus le stock résiduel | PASS | Aucune occurrence de la comparaison du stock à zéro ne subsiste dans `sim/engine.py`. Le maillon faim ne reçoit qu'une pénurie en kilogrammes. |
| **SC4** — témoin et receveuse à `hunger_ticks` nul | PASS | Monde à trois cellules **rebâti par moi** (production désactivée, ration exacte livrée par le commerce), un tick complet : les deux cellules finissent avec un stock nul, un déficit nul, et un compteur de faim nul. |
| **SC4** — le critère reste falsifiable dans l'autre sens | PASS | La pénurie retournée par la consommation vaut exactement le besoin non couvert, pas un booléen déguisé. **Contre-preuve montée par moi** : en remettant l'ancien critère, les deux cellules rassasiées repartent à un tick de faim chacune et le test tombe en `FAILED`. |
| **SC5** — la réduction de dette est bornée par le surplus | PASS | Six surplus rejoués, de `1e-9` kg à deux fois la dette : dans tous les cas la réduction reste sous le surplus, le stock reste positif, et la réduction croît bien avec le surplus. |
| **SC5** — un surplus infinitésimal n'efface pas `1000` kg | PASS | Reproduit : la dette reste au-dessus du seuil exigé par le brief. **Contre-preuve montée par moi** : en remettant la formule proportionnelle à la dette, le même surplus efface un dixième de la dette et met le stock en négatif — les trois tests du fichier tombent en `FAILED`. |
| **SC5** — les kilogrammes remboursés quittent le stock | PASS | Vérifié : après remboursement, le stock vaut le surplus moins le remboursement, jamais le surplus intact. Rien ne se téléporte. |
| **SC5** — note documentaire P3-`2` | PASS | Une seule phrase dans `sim/SEEDING.md`, section `SC2` du brief `017`, qualifiant l'écrêtage sans réallocation de choix de simplicité. Le code du commerce n'est pas touché. |
| **SC6** — les quatre compteurs `> 0`, script reproductible | PASS | Script rejoué depuis la racine : les quatre valeurs sortent **identiques** au manifeste, et le script sort en succès. |
| **SC6** — échantillon = monde chargé par G3 | PASS | Le script part de `World.from_g3(rng_seed=42)` ; le nombre de cellules et d'arêtes est lu du monde chargé, jamais codé en dur. Aucun monde construit à la main, aucun échantillon vide. |
| **SC6** — cellules affamées mesurées avec la définition SC4 | PASS | Le script n'observe que le compteur de faim, lequel n'est plus alimenté que par une pénurie réelle. Le lien est établi par lecture du code, pas par confiance. |
| **SC6** — archives `012` et `013` intactes | PASS | Aucun fichier des dossiers `011` à `014` n'apparaît dans le différentiel du lot. La valeur archivée du brief `013` est toujours en place dans son manifeste. |
| **SC7** — tests précédents adaptés avec motivation écrite | PASS | Aucun fichier de test supprimé. Un seul test disparaît à l'intérieur d'un fichier, et il est nommé et motivé dans la section `4` du journal. |
| **SC7** — suite complète verte | PASS | Les deux suites rejouées par moi : côté simulation tout passe, côté harnais tout passe avec les seuls sauts Unity attendus sous Linux. Aucun `FAILED`. |
| **SC8** — registre de coût | PASS | Le rapport du registre fait apparaître le brief avec une invocation Cursor, et la dernière ligne du fichier porte l'événement, le brief et l'identifiant d'audit exigés. |
| **Preuves rouges** — paire A, deux fichiers | PASS | Le fichier rouge contient des `FAILED`, le fichier vert n'en contient aucun. Reproduit indépendamment par ma propre contre-preuve. |
| **Preuves rouges** — paire B, deux fichiers | PASS | Idem. |
| **Interdits de forme** | PASS | Aucune commande `python` nue dans les livrables ni dans `sim/`. Aucune valeur hexadécimale de condensé recopiée nulle part. |

---

## 4. Verdict global : **PASS** (conditionné au rejeu de la porte)

Les huit conditions de succès sont satisfaites, et elles le sont *fonctionnellement*,
pas seulement *formellement* : les quatre gardes du lot rougissent réellement
quand on remet le défaut qu'elles sont censées attraper. Je l'ai vérifié en
montant les quatre sabotages moi-même, dans des copies hors dépôt, sans
toucher au dépôt.

Le passage reste subordonné à une porte mécanique renvoyant `VERDICT: ACCEPT`
une fois le présent fichier committé. Si le rejeu échoue pour un autre motif
que l'absence de verdict, ce PASS tombe.

---

## 5. Le point `3` de la rubrique SC1 : la commande écrite ne pouvait pas fonctionner

La rubrique demandait de vérifier le signe de `HUNGER_DEATH_SCALE` en
remplaçant la constante en mémoire **puis** en rechargeant le module. Le
Générateur signale, à juste titre, que cette séquence ne peut rien montrer :
recharger un module ré-exécute son fichier source et réécrit donc la valeur
qu'on venait de remplacer. La commande de la rubrique aurait renvoyé « faux »
quelle que soit l'implémentation, correcte ou non.

Ce n'est **pas** un défaut du lot, et ce n'est pas non plus une dérogation
accordée sur parole. La propriété exigée par le brief est celle du signe ;
j'ai vérifié cette propriété par une autre commande, qui ne recharge rien :
remplacement en mémoire, puis appel de la fonction de calcul, qui relit les
variables courantes du module. La propriété tient dans les deux sens et se
restaure exactement.

Un point mérite d'être noté pour le prochain Planificateur : la constante
figée au chargement, elle, ne bouge pas. La distinction entre la constante
et la fonction est documentée dans `sim/SEEDING.md` et dans les tests, mais
elle reste un piège pour qui lirait vite.

---

## 6. Écarts de périmètre

Un seul, déclaré par le Générateur et sans conséquence :

**`sim/world.py` n'était pas dans la liste des fichiers autorisés.** Le contrat
d'exécution du brief dit couvrir « exclusivement » `sim/engine.py`,
`sim/constants.py`, `sim/model.py`, `sim/SEEDING.md`, `sim/tests/`, le dossier
du lot et le registre de coût. `sim/world.py` a été modifié.

Je juge cet écart **non bloquant**, pour trois raisons cumulées :

1. Le fichier n'est dans aucune des listes d'interdits — ni les Non-Goals du
   brief, ni le tableau des échecs disqualifiants de la rubrique.
2. La modification est de deux blocs et strictement nécessaire à `SC3` :
   initialiser le nouveau champ de mortalité à zéro à l'amorçage (sinon le
   monde chargé porterait la sentinelle « non calculé »), et l'ajouter à la
   sérialisation canonique pour que l'empreinte de déterminisme couvre ce
   nouvel état. Le second point est même une bonne surprise : il aurait été
   plus facile de laisser le champ hors de l'empreinte.
3. Elle est déclarée noir sur blanc dans la section `9` du journal, pas
   dissimulée.

C'est un défaut de rédaction du brief (la liste exclusive a oublié un fichier
que sa propre condition `SC3` rendait obligatoire), pas une transgression du
Générateur.

Aucun fichier réellement interdit n'a été touché : rien sous les répertoires du
harnais, de l'architecture, de la géographie, d'Unity, ni les documents de
vision et de feuille de route, ni les archives des briefs `011` à `014`.

---

## 7. Réserves — non bloquantes, à verser au prochain Planificateur

### `N1` — la prédiction dépend de la mortalité, mais faiblement

C'est ma réserve principale et je la formule sans l'adoucir.

La prédiction se compose de deux termes. Le premier, le dépassement
déterministe, vaut quatre cinquièmes et **ne dépend pas du tout** de
`HUNGER_DEATH_SCALE`. Le second, l'érosion stochastique, est celui qui porte la
constante de mortalité : il pèse `0.43` pour cent de la prédiction. Doubler la
constante de mortalité déplace la prédiction d'environ trois millièmes, soit
`3` pour cent de la tolérance stationnaire.

Le brief exigeait deux choses distinctes, et le lot les satisfait toutes deux :
que la prédiction **dépende explicitement** de la constante (`SC1`) et que le
sens du mouvement concorde avec la mesure (`SC2`). Le test de sensibilité
compare les prédictions **entre elles**, donc il attrape bien le signe, et ma
contre-preuve démontre qu'il rougit si on l'ôte.

Mais le test de **conformité** (`SC1`), lui, reste en pratique aveugle à la
mortalité : sa fenêtre est trente fois plus large que l'effet total du
doublement de la constante. Le brief `017` a corrigé le défaut de principe
dénoncé par les audits — le critère n'ignore plus ce qui tue — sans encore
lui donner de mordant numérique.

**Comment y remédier au prochain lot** : exiger que la tolérance stationnaire
soit dérivée de telle sorte que le régime à mortalité doublée tombe **hors** de
la fenêtre nominale. Autrement dit, demander une propriété de discrimination,
pas seulement une propriété de signe. Cela obligera à modéliser correctement le
poids de la mortalité dans le régime stationnaire, ce que le terme d'érosion
actuel ne fait qu'esquisser.

### `N2` — `MAX_DEATH_RATE_PER_TICK` est présent mais inerte dans la prédiction

Le brief exigeait que la prédiction dépende explicitement du plafond de
mortalité. Il y figure, sous la forme correcte : le même plafonnement que celui
appliqué par le moteur. Mais avec les constantes actuelles, le taux de
mortalité stationnaire est trois ordres de grandeur sous le plafond, si bien
que le plafonnement ne mord jamais. Je l'ai vérifié : multiplier le plafond par
dix ne change **strictement rien** à la prédiction.

Ce n'est pas une dépendance décorative — c'est la bonne physique, simplement
inactive dans ce régime. Mais la lettre du brief (« dépendant explicitement
de ») est satisfaite par une variable dont la dérivée est nulle.

**Comment y remédier** : un futur brief qui exige une dépendance devrait exiger
aussi qu'elle soit **observable**, c'est-à-dire qu'un test montre la prédiction
bouger quand la constante bouge, sur au moins un régime atteignable.

### `N3` — les tolérances sont larges, mais je ne trouve pas trace de calibration après mesure

Le mode d'échec numéro `5` (ajuster une tolérance après avoir vu la mesure) est
le motif qui a fait rejeter le lot `013` en première itération. Je l'ai
cherché ici, et je conclus qu'il n'est **pas** présent :

- Les trois tolérances sont des produits en forme fermée de constantes
  existantes, chacune avec une justification physique distincte, recalculables
  à la main — je les ai toutes les trois retrouvées au bit près sans lire le
  code du Générateur.
- Aucune section du brief `017` dans `sim/SEEDING.md` ne cite une valeur
  mesurée du monde réel.
- Surtout, une tolérance calibrée après coup se reconnaît à ce qu'elle épouse
  la mesure de près. Ici c'est l'inverse : chacune est nettement plus large que
  l'écart qu'elle doit couvrir — d'un facteur proche de deux pour la conformité,
  d'un facteur voisin de dix pour la convergence. On ne calibre pas « au
  large », c'est contre-productif.

La réserve porte donc non sur l'honnêteté de la dérivation mais sur le
**mordant** du résultat : la fenêtre de conformité accepte toute fraction de
survie mesurée entre environ `0.696` et `0.898`. Un effondrement partiel du
monde y passerait sans rougir.

**Comment y remédier** : voir `N1`. La tolérance doit être resserrée par une
meilleure modélisation, jamais par un chiffre choisi à la main.

### `N4` — le test de précision de la mortalité joue moins de ticks qu'annoncé

Le brief demandait la vérification « sur `N_STAT_SURVIE` ticks ». Le test
s'arrête dès qu'une cellule est éteinte, ce qui survient entre le quart et le
milieu de l'horizon selon la cellule : la seconde moitié de l'horizon n'est
jamais jouée.

La propriété reste vraie et le test reste falsifiable — ma contre-preuve le
fait rougir avec une marge énorme. Mais l'horizon annoncé dans le nom du
compteur n'est pas l'horizon effectivement parcouru, et une fois la cellule
éteinte les deux sommes comparées sont bornées par la population initiale, ce
qui affaiblit mécaniquement le pouvoir discriminant de la comparaison.

**Comment y remédier** : maintenir la population du micro-monde (ou réinjecter
les habitants) pour que l'accumulateur soit sollicité sur tout l'horizon, et
faire imprimer au test le nombre de ticks réellement joués par cellule.

### `N5` — « documenté avant mesure » n'est pas vérifiable mécaniquement

Le brief et la rubrique exigent que `sim/SEEDING.md` précède la mesure. Tout le
travail du Générateur tient dans un seul commit : l'ordre interne de sa
rédaction n'est donc pas contrôlable par l'historique. Ce que j'ai pu vérifier,
c'est la preuve indirecte : aucune valeur mesurée n'est citée dans les sections
concernées, et chaque tolérance est une fonction fermée de constantes
antérieures. C'est cohérent, et c'est tout ce qui est vérifiable.

**Comment y remédier** : demander au Générateur un commit distinct pour
`sim/SEEDING.md`, antérieur au commit qui produit les mesures. La porte
mécanique pourra alors comparer deux horodatages au lieu de croire une
déclaration.

### `N6` — la coupure du déficit résiduel efface encore des kilogrammes

Héritée du brief `013` et explicitement conservée : un déficit résiduel
inférieur au seuil de coupure est ramené à zéro sans contrepartie physique.
C'est une infraction minuscule au principe « rien ne se téléporte », assumée et
documentée, et hors du périmètre du brief `017`. Je la signale pour qu'elle ne
se perde pas : maintenant que le remboursement est physique, cette coupure est
la dernière porte par laquelle des kilogrammes disparaissent.

### `N7` — la liste exclusive du périmètre a oublié un fichier nécessaire

Voir la section `6`. Le prochain Planificateur qui ajoute un champ à une entité
doit inclure dans le périmètre autorisé **tous** les fichiers qui construisent
ou sérialisent cette entité, pas seulement celui qui la déclare.

---

## 8. Ce qui s'est amélioré depuis l'itération précédente du thème

Il n'y a pas d'itération antérieure du brief `017` : c'est un premier passage.
La comparaison utile est donc avec l'état laissé par le brief `013`, que les
deux audits sources avaient mis en cause.

- **Le critère de survie n'est plus aveugle à ce qui tue.** C'était le reproche
  central. La constante de mortalité entre désormais dans la prédiction, avec
  le bon signe, et un test dédié le prouve — test qui rougit franchement quand
  on retire la dépendance.
- **La dépendance à l'horizon de test est réellement éteinte, pas repoussée.**
  C'est le point que j'ai le plus cherché à mettre en défaut, en poussant la
  simulation huit fois au-delà de l'horizon exigé. La fraction de survie
  converge, la dérive entre jalons décroît de façon monotone, l'écart au modèle
  plafonne. Le scénario « vert à court terme, rouge à long terme » ne se
  reproduit pas.
- **Le signe inversé de la récupération du déficit est corrigé**, et corrigé à
  la racine : la constante fautive est supprimée, pas rafistolée, et son
  successeur a une sémantique physique explicite.
- **Les petites cellules ne sont plus immortelles par arrondi.** La démonstration
  est nette : sans report, deux cellules sur trois ne perdent aucun habitant sur
  mille ticks ; avec report, la première mort tombe au deuxième tick.
- **La dette alimentaire ne s'efface plus sans contrepartie.** L'invariant est
  testé sur sept ordres de grandeur de surplus, pas sur un cas unique — c'est
  la bonne façon de prouver une borne.
- **Les quatre gardes sont falsifiables.** Le brief n'exigeait de preuve rouge
  que pour deux d'entre elles ; j'ai monté les quatre sabotages, et les quatre
  rougissent. Aucune garde décorative dans ce lot.
- **Aucune suppression silencieuse.** Le seul test retiré est nommé et motivé,
  et son fichier hôte a gagné un test de remplacement qui rougirait si la
  densité stationnaire redevenait la simple capacité de charge.

## 9. Ce qui a régressé

Rien. Aucun test préexistant n'a été affaibli ou supprimé sans contrepartie,
l'ordre du tick est inchangé, le plancher de mortalité proscrit par le brief
`013` n'est pas réintroduit, le commerce ne touche toujours pas au déficit, et
les archives sont intactes.

---

## 10. Retour pour la suite

Le lot est reçu. Les sept réserves ci-dessus ne demandent aucune correction au
Générateur du brief `017` : elles décrivent ce que le **prochain** brief devra
exiger pour que le seuil de survie passe d'honnête à mordant.

Par ordre d'importance décroissante pour le prochain Planificateur :

1. `N1` — exiger que le test de conformité **discrimine** les régimes de
   mortalité, et pas seulement que la prédiction en dépende par le signe.
2. `N2` — exiger que toute dépendance déclarée soit observable sur un régime
   atteignable.
3. `N5` — imposer un commit de documentation antérieur au commit de mesure,
   pour rendre l'ordre vérifiable par la machine plutôt que par déclaration.
4. `N4` — corriger le micro-monde du test de précision pour qu'il joue
   réellement l'horizon annoncé.
5. `N7` — inclure dans le périmètre autorisé tous les fichiers qui construisent
   ou sérialisent une entité dont on modifie la forme.
6. `N6` — décider du sort de la coupure du déficit résiduel, maintenant qu'elle
   est le dernier endroit où des kilogrammes disparaissent.

**Celui qui produit ne prononce pas la recevabilité — et celui qui prononce
n'a touché à aucune ligne de code.**
