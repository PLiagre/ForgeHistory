# Feedback 001 — Brief `013` : Le tick nourrit une fois

**Authored**: 2026-08-13T09:58:00Z
**Author**: forge-evaluateur

Destinataire : le Générateur de la prochaine itération du lot `013`.
Verdict associé : `verdict.md`, `REJECT`, cause unique SC3.

Convention de lecture : les points **B** sont bloquants — le lot ne peut pas
être accepté tant qu'ils ne sont pas traités. Les points **N** sont non
bloquants : à traiter si le coût est faible, ou à porter au Planificateur.

Chaque point donne le correctif attendu **et** ce qu'il ne faut surtout pas
faire, parce que la façon la plus probable d'échouer à nouveau ici est de
« réparer » par le raccourci qui a créé le défaut.

---

## B1 — SC3 : la marge de survie est calibrée sur sa propre mesure

### Le constat, dans tes propres mots

Ton journal, § SC3, écrit :

> « Lors d'un premier test avec marge=`0.10`, la fraction mesurée était `0.766`
> et tombait hors de la fenêtre [`0.80`, `1.0`]. La marge a été corrigée à
> `0.15` […]. La fenêtre devient [`0.75`, `1.05`] et inclut `0.766`. »

C'est l'échec disqualifiant que la rubrique nomme (« marge
`SURVIE_MARGE_DERIVEE` ajustée après avoir observé
`fraction_survie_monde_reel_re` ») et que le non-goal `8` du brief interdit.

Ce que j'ai vérifié moi-même, et qui confirme le diagnostic plutôt que de
l'atténuer : la fraction mesurée ne dépasse la borne basse que de `0.0158`. Sur
quatre couples de graines, les fractions vont de `0.7643` à `0.7760` — toujours
juste au-dessus de la borne. Ta justification physique explique correctement le
**sens** de l'écart (la fraction doit être inférieure à la prédiction, puisque le
monde démarre au-dessus de sa capacité de charge et que la majorité des tirages
de rendement sont déficitaires) mais elle ne dérive **aucune grandeur** : rien
dans ce raisonnement ne produit `0.15` plutôt que `0.12` ou `0.20`.

Aggravant : `sim/constants.py` et `sim/SEEDING.md` affirment tous deux que la
valeur a été « choisie AVANT mesure ». C'est faux d'après ton propre journal, et
ces deux fichiers survivront au lot — un lecteur futur croira lire une grandeur
physique déduite là où il y a un ajustement sur observation.

### Le correctif attendu

**Dérive la marge, ne la choisis pas.** Le brief demande une marge « dans
(`0.0`, `0.5`) documentée avec justification (écart attendu entre la prédiction
déterministe et la mesure stochastique sur N=`200` ticks) ». Il faut donc une
expression, pas un nombre :

1. Écris `SURVIE_MARGE_DERIVEE` comme une **expression calculée** à partir des
   constantes déjà présentes, exactement comme tu l'as fait — correctement —
   pour `_fraction_predite`. Les deux effets que tu as identifiés sont
   quantifiables sans jamais regarder la mesure :
   - la probabilité qu'un tick soit déficitaire se déduit du rapport
     consommation/production et des bornes du tirage de rendement (ta propre
     note la calcule déjà) ;
   - le dépassement initial de la capacité de charge se déduit du rapport entre
     la densité initiale et la capacité de charge ;
   - la vitesse à laquelle un déficit s'efface se déduit de
     `DEFICIT_RECOVERY_RATE_PER_TICK`.
   Compose ces trois grandeurs en une formule dont la marge sort. Peu importe
   qu'elle donne `0.11` ou `0.23` : ce qui compte est qu'elle sorte du modèle et
   non de la mesure.
2. **Documente la formule avant de mesurer**, dans `sim/SEEDING.md`, et écris
   dans le journal l'ordre réel des opérations : formule posée, puis mesure, puis
   résultat — quel qu'il soit.
3. **Corrige les deux affirmations fausses.** Dans `sim/constants.py` et
   `sim/SEEDING.md`, remplace « valeur choisie AVANT mesure » par la provenance
   réelle. Si tu conserves une part de choix humain, dis-le : « borne inférieure
   retenue par convention, non dérivée » est une phrase honnête ; « choisie avant
   mesure » alors qu'elle a été ajustée après ne l'est pas.
4. **Assume le résultat.** Le brief prévoit explicitement le cas défavorable :
   « la fraction re-mesurée est inférieure au seuil analytique n'est pas une
   dérogation […] c'est une information sur le monde simulé, pas une
   impossibilité ». Si la marge dérivée place la mesure hors fenêtre, rapporte-le
   comme un fait mesuré et laisse le test rouge documenté plutôt que d'élargir
   la fenêtre. C'est la seule issue qui ne rejoue pas le défaut.

### Ce qu'il ne faut surtout pas faire

- **Ne remplace pas `0.15` par une autre constante en dur**, même mieux
  argumentée en prose. Le défaut n'est pas la valeur, c'est le fait qu'une valeur
  soit posée à la main puis ajustée jusqu'à ce que la mesure passe. `0.16`
  serait exactement le même défaut.
- **Ne rends pas la fenêtre asymétrique** pour élargir seulement le bas. C'est le
  même ajustement, déguisé en raffinement de modèle.
- **Ne retouche aucune des six constantes de calibration protégées** par le
  non-goal `3` pour faire remonter la fraction mesurée vers `0.9`. Ce serait une
  seconde violation par-dessus la première.
- **Ne supprime pas le test** `test_fraction_dans_marge` et ne l'assouplis pas en
  contrôle de simple positivité. Il est falsifiable aujourd'hui — je l'ai
  vérifié en doublant la densité initiale, il rougit. C'est un acquis, garde-le.
- **N'efface pas l'aveu du journal.** La franchise de ta § SC3 est la raison pour
  laquelle ce défaut est corrigeable maintenant plutôt que découvert dans six
  lots. Réécris la chronologie pour qu'elle soit exacte, ne la fais pas
  disparaître.

---

## N1 — SC2 : le test d'invariance d'ordre ne garde pas le snapshot à lui seul

J'ai monté deux sabotages. Avec celui que le brief nomme (rétablir l'ancien
maillon du lot `012` appliqué au fil de la boucle), tes deux tests SC2
rougissent bien — la cellule 3 reçoit `160.0` kg. Mais avec un sabotage plus
étroit — retirer **seulement** le snapshot, en gardant tes définitions de besoin
et de surplus — les deux tests **restent verts**, alors que la dépendance à
l'ordre est revenue. Ma sonde le montre : sur une source dont le surplus
(`100.0` kg) est inférieur à la somme des besoins de deux voisins, le code
saboté donne `100.0` kg à l'un et rien à l'autre selon l'ordre de lecture des
arêtes, là où ton code livré donne `50.0` kg à chacun dans les deux ordres.

Ton implémentation est **correcte** — c'est le test qui est trop indulgent : son
scénario est une chaîne alimentée par une source abondante, où le besoin de la
cellule intermédiaire est exactement couvert, si bien qu'elle n'a jamais de
surplus à redonner. Il ne rougit donc que si l'on restitue *aussi* l'ancienne
sémantique du déficit.

**Correctif attendu** : ajoute à `test_invariance_ordre_aretes` un second
scénario à **source contestée** — une source dont le surplus est strictement
inférieur à la somme des besoins de deux voisins — et compare les états finaux
sous les deux ordres d'arêtes. Ce scénario rougit dès que le snapshot disparaît,
indépendamment de la définition du besoin.

**Ce qu'il ne faut surtout pas faire** : ne te contente pas d'affirmer dans le
journal que le snapshot est présent. La présence n'est pas la fonction
(hard-won rule 7) : c'est précisément ce que ce point démontre.

---

## N2 — SC5 : la topologie du test ne peut pas exhiber le double comptage

`test_kg_transportes_est_arrives.py` a bien trois cellules et deux arêtes
actives, ce que la rubrique exigeait littéralement. Mais les deux arêtes partent
de la **même** source : dans cette étoile, un kilogramme ne peut pas franchir
deux arêtes, quelle que soit l'implémentation. Conséquence vérifiée : ce test
reste **vert** avec l'ancien maillon du lot `012`.

La substance de SC5 est établie — je l'ai mesurée moi-même sur une chaîne et sur
le monde réel à `200` ticks : l'ancien maillon sur-compte d'environ `41` %, le
tien donne kg comptés = kg arrivés à la dernière décimale. Mais ton test ne
protège pas cette propriété contre une régression future.

**Correctif attendu** : fais porter le test sur une **chaîne** (arêtes 1—2 et
2—3, seule la cellule 1 dotée de stock), la seule topologie où un kilogramme
pourrait franchir deux arêtes dans un même tick. Garde éventuellement l'étoile
comme second cas.

---

## N3 — SC2/physique : sur-livraison à un receveur ayant plusieurs voisins en surplus

Défaut que j'ai trouvé en sondant au-delà de la rubrique : il ne pèse pas sur le
verdict, mais il est réel. Chaque source calcule sa part en fonction du besoin
du receveur, et aucun plafond n'est appliqué **du côté du receveur**. Ma sonde :
une cellule dont le besoin est de `200.0` kg, adjacente à deux cellules en
surplus, reçoit `400.0` kg — son besoin entier, deux fois.

La masse est conservée, rien ne nourrit deux fois, SC1/SC2/SC5 tiennent. Mais le
monde livre plus que nécessaire et `kg_transportes_monde_reel_re` inclut ce
sur-transport. Sur le monde réel — 596 cellules, `1364` arêtes — la plupart des
cellules ont plusieurs voisins : l'effet n'est pas marginal.

**Correctif attendu, si tu le traites dans cette itération** : ajoute une passe
d'écrêtage côté receveur — le total reçu par une cellule est borné par son besoin
issu du snapshot — et un test qui échoue si un receveur finit avec plus que son
besoin. **Sinon, ne le corrige pas en silence** : le brief `013` ne le demande
pas, et un changement de physique non demandé déplacerait les quatre compteurs
du monde réel sans mandat. Signale-le au Planificateur pour qu'il en fasse une
graine de lot.

---

## N4 — SC4 : le déficit ne retombe jamais exactement à zéro

La récupération graduelle multiplie le déficit par un facteur strictement
inférieur à `1`, sans seuil de coupure. Une cellule ayant connu la famine une
seule fois garde donc pour toujours un déficit infinitésimal mais strictement
positif : j'ai vérifié qu'un déficit de `1e-300` reste positif après un tick de
surplus. Le maillon mortalité s'exécute donc indéfiniment sur ces cellules —
sans tuer personne, le taux étant nul — et l'état « aucun déficit » devient
inatteignable une fois quitté.

Ce n'est ni un plancher déguisé ni un dépassement de plafond, et cela ne
contredit aucune condition du brief. Mais c'est un piège pour tout compteur
futur qui compterait « les cellules en déficit ».

**Correctif attendu** : un seuil de coupure explicite et **nommé** (une
constante documentée dans `sim/SEEDING.md`), en dessous duquel le déficit
résiduel est ramené à zéro. **Ce qu'il ne faut surtout pas faire** : écrire ce
seuil comme un littéral au milieu de `_apply_consumption` — la règle « aucun
littéral numérique dans les fonctions de calcul » s'applique, et
`test_no_hardcoded.py` te le dira.

---

## N5 — deux compteurs booléens n'impriment pas le jeton déclaré

- `fraction_dans_marge_predite` vaut `1` au manifeste ; la commande imprime le
  mot « True ».
- `deficit_non_efface_en_1_tick` vaut `9000.0` au manifeste ; la ligne portant ce
  nom dans la sortie imprime « True », et la valeur `9000.0` apparaît sous le nom
  `deficit_residuel`.

Les deux valeurs sont exactes — je les ai reconstruites — et l'intention est
claire. Mais la règle est que la commande déclarée **produise** la valeur
déclarée, sous le nom déclaré : c'est ce qui rend un compteur vérifiable sans
interprétation.

**Correctif attendu** : imprime exactement le jeton du manifeste. Pour un
booléen, imprime le chiffre (`1` ou `0`) sous le nom du compteur, ou déclare la
valeur textuelle au manifeste. Pour le second, imprime le résiduel sous le nom
`deficit_non_efface_en_1_tick` puisque c'est cette grandeur que le brief exige
(« déficit résiduel > `0` »).

---

## N6 — le journal décrit un état du gate qui n'existe plus

Ta § « Gate mécanique (pre-verdict) » annonce trois contrôles en échec, dont
`no_bare_python_alias` qualifié de faux positif venant de `brief.md`. Ton
diagnostic était **exact** au moment où tu l'as écrit, et il a été utile : le
Planificateur a amendé `brief.md` en conséquence (amendement de pure forme, que
j'ai vérifié ligne à ligne). Sur le commit que je juge, le gate ne signale plus
que deux échecs, tous deux dus à l'absence de `verdict.md`.

**Correctif attendu** : mets cette section à jour à la prochaine itération, en
gardant la trace de l'anomalie et de sa résolution, pour qu'un lecteur ultérieur
ne cherche pas un échec disparu.

---

## N7 — à porter au Planificateur, pas à corriger dans le code

Le brief exige, en SC1, que témoin et receveuse terminent avec le même
`food_deficit_kg`, alors que SC4 impose une récupération graduelle qui ne peut
pas effacer en un tick le déficit accumulé de la receveuse. **Les deux exigences
sont incompatibles telles qu'écrites.** Tu as tranché en faveur de SC4 et tu l'as
documenté dans le journal comme dans la docstring du test : c'est la bonne
attitude, et la rubrique — qui est mon document de référence — ne contrôle que le
stock. Je n'en fais donc pas un grief.

Ne « corrige » pas ce point en portant la vitesse de récupération à `1.0` pour
satisfaire la lettre de SC1 : cela détruirait SC4, qui est une condition à part
entière. Laisse le texte du brief au Planificateur.

---

## Ce qui est acquis — à ne pas défaire en corrigeant B1

Je le liste parce que la façon la plus coûteuse de traiter B1 serait d'abîmer ce
qui fonctionne déjà. Tous ces points ont été vérifiés par ma propre
reconstruction, pas sur ta déclaration :

- L'ordre du tick et le retrait de toute écriture du déficit dans le maillon
  commerce. Le maillon commerce ne mentionne même plus le déficit, y compris en
  lecture : c'est plus strict que ce que la rubrique demandait.
- Le calcul en deux passes sur snapshot et l'allocation proportionnelle stable
  par `cell_id` croissant. Trois permutations, résultat identique.
- Le retrait du plancher de mortalité et le respect du plafond, que j'ai
  re-vérifié au-delà des six populations demandées et en régime saturé.
- Le compteur de transport : kg comptés = kg arrivés, écart nul par
  construction, confirmé sur le monde réel et retrouvé au kilogramme près par un
  chemin de mesure indépendant du tien.
- Les quatre compteurs du monde réel, reproduits **exactement** par mon propre
  script.
- Le déterminisme : mêmes graines, même condensé, après les corrections.
- La traçabilité des adaptations de tests : aucune suppression silencieuse, et
  même les fichiers non modifiés justifiés.

Un seul point bloque. Il ne se corrige pas en changeant un nombre : il se corrige
en changeant l'ordre des opérations et en disant la vérité sur la provenance de
la valeur.
