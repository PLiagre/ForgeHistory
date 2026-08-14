# Verdict — Brief `020` : provenance du littoral de G3

**Authored**: 2026-08-14T12:36:00Z
**Author**: forge-evaluateur

> **Note de transparence (contrat de l'Évaluateur) :** le rôle signataire est le
> rôle natif du harnais `forge-evaluateur`. L'acteur réel est un sous-agent
> Cursor Cloud (modèle Claude Opus 5), orchestré de l'extérieur par un agent
> Cursor Cloud qui remplace le CTO Claude. Aucun suffixe n'est ajouté à la
> signature : le contrôle mécanique `verdict_is_not_self_authored` compare les
> acteurs de part et d'autre d'un lot, et un couple de signatures suffixées
> serait refusé.

Ce verdict est rendu contre le `brief.md` et l'`eval-rubric.md` écrits **avant**
tout code. Aucun fichier de code, d'artefact, de brief ou de rubrique n'a été
modifié pour le rendre. Aucune valeur d'empreinte n'est citée : les empreintes
sont nommées par leur source et comparées à l'exécution (règle durement acquise
n° `12`).

---

## Résultat de la porte mécanique

**Avant écriture de ce verdict.** Commande jouée en premier, avant toute lecture
de fond : `.venv/bin/python harness/verdict_audit.py <dossier du brief>`.
Rapport intégral conservé hors dépôt sous `/tmp/020-eval/gate_avant.txt`.

Code de sortie `1`, `VERDICT: REJECT`. Deux contrôles au rouge, et **seulement**
ces deux-là :

- `verdict_numbers_traceable` : `verdict.md missing` ;
- `verdict_is_not_self_authored` : frontmatter `Author` absente côté verdict.

Ces deux contrôles lisent `verdict.md`, que seul l'Évaluateur écrit. Ce n'est
donc pas un rejet de fond, et le Générateur l'a dit lui-même sans arrangement
dans son journal. Les **huit** autres contrôles étaient déjà au vert à cette
étape : fichiers déclarés présents, horodatages postérieurs au brief, les cinq
couples `must_differ_from` qui diffèrent bien, dérogations munies d'une commande
et d'une erreur, aucun `sample_size` nul ou à la sentinelle, aucun alias nu de
l'interpréteur, rubrique antérieure au premier livrable, et les `11` fichiers
déclarés à l'intérieur du dossier du brief tous suivis par git.

**Après écriture de ce verdict**, la même commande rend `VERDICT: ACCEPT`, dix
contrôles sur dix — sortie conservée sous `/tmp/020-eval/gate_apres.txt`.

---

## Ce que j'ai re-dérivé moi-même

Je n'ai lu `deliverables/manifest.json` qu'**après** avoir produit mes propres
chiffres. Mon script de reconstruction vit hors dépôt
(`/tmp/020-eval/recount.py`, sortie sous `/tmp/020-eval/recount_out.txt`) et
re-dérive `60` valeurs, dont les `38` compteurs exigés. Aucun nombre n'est repris
du manifeste.

Points saillants de la reconstruction :

- **La terre.** J'ai chargé la géométrie de terre du littoral vivant et les
  `596` géométries de cellule, calculé leur union en projection `EPSG:3035`, puis
  les deux aires. La part de l'union qui sort de la terre est **nulle** ; la
  terre qu'aucune cellule ne couvre est de `554,304` m². L'epsilon que j'ai
  **lue** de `constants.py` vaut `10 000,0` m². Les deux résidus sont donc sous
  l'epsilon, et je conclus moi-même : **écart de sérialisation**, pas écart de
  géométrie. La surface mesurée (`6 667 146,53` km²) coïncide avec le
  `land_area_km2` que porte l'artefact — la mesure n'a pas été faite sur une
  géométrie vide ni sur une fenêtre tronquée (mode d'échec n° 6 écarté).
- **Contre-preuve du diagnostic.** J'ai amputé l'union d'une cellule entière
  (hors dépôt) : la terre non couverte passe à environ `42,5` millions de m²,
  soit plus de quatre mille fois l'epsilon lue, et `ecart_est_serialisation`
  tombe à `0`. Le diagnostic mesure donc bien quelque chose.
- **Contre-preuve de la commande d'écart.** J'ai muté **un octet** du littoral
  vivant dans une copie hors dépôt et rejoué
  `deliverables/check_provenance_coastline_020.py` : elle repasse au code `1`.
  Elle lit donc bien le fichier qu'elle prétend lire.
- **Sabotage refait par moi.** J'ai monté ma propre copie hors dépôt, muté la
  seule **déclaration** `inputs.coastline_1400` du `MANIFEST_g3.json` de la
  copie — jamais le code de la garde — et rejoué
  `tests/run_proof_coastline_provenance.py` : code de sortie `1`, quatre
  comparaisons en désaccord sur `6` (sortie sous
  `/tmp/020-eval/garde_rouge_evaluateur.txt`). La preuve rouge du lot est donc
  **reproductible**, pas recopiée.
- **Cas d'absence.** Dans la même copie, littoral retiré, la garde sort au code
  `2` et nomme la commande de régénération. Jamais `1` : une absence n'est pas
  confondue avec un écart mesuré (règle n° `10`).
- **Idempotence.** J'ai relancé l'alignement une passe supplémentaire, puis la
  garde. Les trois artefacts et les deux journaux de la garde sont restés
  **octet-identiques**, et `git status --porcelain` est resté entièrement vide
  sur le dépôt. C'est plus fort que ce que le Générateur pouvait montrer :
  chez lui la réparation n'était pas encore committée.
- **Le graphe G4 n'a pas bougé.** J'ai re-dérivé ses compteurs depuis les
  artefacts : `40` zones de mer, `2 085` arêtes réparties en `917` `land-land`,
  `437` `land-sea`, `63` `sea-sea` et `668` `strait`. Ce sont exactement les
  nombres que le README rapportait **avant** ce lot. Aucun semis rejoué, aucune
  arête recalculée, aucun compteur « amélioré ».
- **Instantanés non fabriqués.** J'ai comparé
  `pre-edit/MANIFEST_g3.json.orig` à l'état réellement committé par le
  Planificateur : ils sont identiques. L'instantané n'a pas été façonné après
  coup pour produire une jolie différence d'un seul champ.
- **Périmètre jugé sur le commit, pas seulement sur l'état de travail.** Le
  commit du Générateur touche `21` fichiers ; aucun ne sort du périmètre de D10.
  Les fichiers interdits (`constants.py`, `qa/checks.py`, `pipeline.py`,
  `io_util.py`, `projection.py`, `steps/02_coastline.py`,
  `steps/02b_corrections_1400.py`, `steps/03_cells.py`, `steps/04_adjacency.py`,
  `.gitignore`, `VISION.md`, `ROADMAP.md`, `HANDOFF.md`) sont tous sans diff, de
  même que `sim/`, `unity/`, `harness/*.py`, `docs/`, `architecture/` et les
  archives du brief `019`.

Toutes mes valeurs coïncident avec le manifeste, **à l'unité près**, pour les
`38` compteurs exigés. Les deux seuls dénominateurs qui diffèrent des miens sont
expliqués plus bas, en observations non bloquantes : ils ne changent aucune
valeur de compteur.

---

## Verdict ligne à ligne de la rubrique

| Condition de succès | Verdict | Preuve (re-dérivée par l'Évaluateur) |
|---|---|---|
| SC1 — diagnostic rejoué, « sérialisation » et non « géométrie » | **PASS** | Débordement des cellules hors terre `0,0` m² ; terre non couverte `554,304` m² ; epsilon **lue** de `constants.py` = `10 000,0` m² ; `ecart_est_serialisation` = `1`. `cellules_lues_g3` = `596` = `cell_count` lu. Commande d'écart : le texte pré-édition committé porte `ECART` (code `1`), la même commande rejouée par moi rend le code `0`. Contre-preuve d'amputation : le diagnostic tombe à `0`. L'epsilon n'apparaît en littéral ni dans la mesure, ni dans l'alignement, ni dans la garde (recherche des motifs `10000` et `10_000` : aucune occurrence). |
| SC2 — maille gelée, identifiants consommés par `sim/` intacts | **PASS** | `artefacts_maille_diff_vides` = `4` sur `4`, et les quatre fichiers sont aussi sans diff face au commit du Planificateur. Instantané `596` identifiants = `cell_count` ; intersection `596` sur `596` ; `0` ajouté et `0` retiré, deux zéros **mesurés** par différence d'ensembles. `fichiers_sim_modifies` = `0` sur `50` fichiers suivis. Suite `sim/tests/` : `65 passed`. `sim/world.py` relu : il consomme `cell_id` et `area_km2` de `cells_g3.json` plus les arêtes de `adjacency_g3.json`, aucune géométrie de littoral, aucun manifeste. |
| SC3 — `MANIFEST_g3.json` déclare le littoral produit | **PASS** | Empreinte du littoral vivant calculée par moi : elle égale l'entrée déclarée par G3 **et** la sortie déclarée par G2-bis. `sorties_g3_conformes` = `5` sur `5`. `champs_manifeste_g3_modifies` = `1` sur `17` feuilles, le seul chemin étant `.inputs.coastline_1400`. `fixed_timestamp` conservé à l'époque figée. Sérialisation canonique de la chaîne intacte. Le script d'alignement calcule l'empreinte par `io_util.sha256_file` **sur le fichier vivant** : elle n'est recopiée ni d'un littéral, ni de `MANIFEST_g2b.json`, ni de `MANIFEST_g4.json` — j'ai relu le code ligne à ligne. |
| SC4 — G4 relit la provenance, son graphe ne bouge pas | **PASS** | `coastline_1400_sha_declared_by_g3` égale l'entrée que G3 déclare désormais ; `inputs.coastline_1400` de G4 également. Les deux drapeaux valent `1`. `sorties_g4_conformes` = `6` sur `6`. `artefacts_g4_modifies_hors_liste` = `0` sur `13`. `graphe_g4_diff_vides` = `16` sur `16`, et ces `16` fichiers sont sans diff face au commit du Planificateur. `stats_g4.json` : une seule feuille changée sur `455`. `MANIFEST_g4.json` : `3` feuilles changées sur `23`, exactement celles de D5 plus l'empreinte de sortie du seul fichier G4 réécrit. Compteurs de graphe re-dérivés identiques à ceux du README d'avant le lot. |
| SC5 — garde vue rouge, alignement déterministe | **PASS** | Garde rejouée sur le dépôt : code `0`, `6` comparaisons concordantes sur `6`. Sabotage **refait par moi** hors dépôt sur la déclaration : code `1`. Cas d'absence : code `2` avec la commande de régénération nommée. Les deux sorties committées diffèrent et le couple est déclaré. Passe d'alignement supplémentaire : `3` fichiers sur `3` octet-identiques, `git status --porcelain -- pipeline/geo/artifacts` **vide**. La garde est lançable seule, ne porte aucune valeur attendue en dur, et compare les drapeaux de G4 à la comparaison qu'elle vient elle-même de calculer — pas à un `1` écrit dans son code. Elle est nommée d'après ce qu'elle dérive, la provenance du littoral, et non d'après le fichier surveillé. `qa/checks.py` n'a pas été touché. |
| SC6 — le README ferme le constat sans sur-revendiquer | **PASS** | `constats_ouverts_README` = `1`, contre `2` sur l'instantané : strictement inférieur. L'entrée fermée est bien celle de l'empreinte du littoral ; celle des **bornes d'intention** de surface et de compacité reste ouverte, mot pour mot. La nouvelle section dit ce que les mesures établissent, et rien de plus : la terre mesurée inchangée aux epsilon près **d'abord**, la déclaration alignée **ensuite**, la maille non rejouée, les drapeaux dérivés. Aucune sur-revendication : E1 explicitement non clos, `Not yet landed` intact, la mer n'est pas dite « simulée ». Le README reste descriptif — `test_single_source_of_instruction.py` passe. Ordre tenu : la fermeture est écrite après que la commande d'écart soit passée au code `0`. |
| SC7 — mesure rejouable, manifeste complet, périmètre, suites | **PASS** | Le script de mesure imprime les `38` compteurs, chacun avec son dénominateur, en lisant artefacts, constantes et état du dépôt. Manifeste : `38` compteurs, aucun `sample_size` nul ou à la sentinelle, les `5` couples `must_differ_from` déclarés, la dérogation munie de sa commande et de son erreur — dérogation que j'ai vérifiée en rejouant `budget.py status`, qui rend bien `UNMEASURABLE`. Balayage hexadécimal (seuil `32` caractères et plus) sur les livrables hors les deux instantanés d'artefact, le README, l'alignement, la garde et les journaux `v1_051_*` : **zéro**. Alias nu de l'interpréteur et chemins de lanceur Windows : **zéro**. `fichiers_preuve_suivis_par_git` = `9` sur `9`, vérifié par `git ls-files`. `coastline_1400.json` et `MANIFEST_g2b.json` restent hors suivi, comme exigé. Suites : `348 passed, 16 skipped` sur le harnais (les `16` SKIP sont ceux d'Unity sous Linux, déclarés), `65 passed` sur `sim/`. Registre de coût : dernière ligne au bon format. |

---

## Verdict global : **PASS**

Le lot `020` tient ses sept conditions de succès. Je le dis nettement, et je dis
aussi **pourquoi** c'est recevable, parce que la raison compte plus que le
résultat.

La mer et les cellules décrivent le même monde — non pas parce qu'on a remaillé
pour faire coïncider une empreinte, mais parce que **deux choses ont été
établies dans cet ordre** :

1. **La terre n'a pas bougé, et c'est une mesure.** L'union des `596` cellules
   ne sort pas d'un mètre carré de la terre du littoral vivant, et la terre
   qu'aucune cellule ne couvre tient dans `554,304` m² — face à une epsilon
   **lue** de `10 000,0` m². J'ai refait cette mesure moi-même, et j'ai vérifié
   qu'elle sait rougir : amputer une seule cellule la fait exploser de plus de
   quatre mille fois l'epsilon. Ce n'était donc pas la terre qui avait changé,
   seulement les octets qui la sérialisent.
2. **La déclaration a été alignée, et rien d'autre.** Un seul chemin de feuille
   a changé dans le manifeste des cellules. La maille est intacte, les `596`
   identifiants que `sim/` consomme sont ceux du fichier committé, le graphe de
   G4 est octet pour octet celui de `019`.

C'est bien la réparation que le brief exigeait, et pas son maquillage : l'égalité
n'a pas été obtenue en changeant de cible. Le compteur
`empreinte_vivant_egale_sortie_g2b` existe toujours et vaut `1`, mais il ne
**remplace** pas la comparaison contre l'entrée de G3 — celle-ci est vraie, la
commande d'écart le prouve en passant du code `1` au code `0`, et la garde
durable la repose à chaque exécution.

Enfin, la garde a été vue rougir, et je l'ai vue rougir moi-même sous mon propre
sabotage. Un contrôle qui ne peut pas rougir ne prouve rien ; celui-ci le peut,
et il distingue une absence de source (code `2`) d'un écart mesuré (code `1`).

---

## Violations de périmètre

**Aucune.**

J'ai cherché, sur le commit du Générateur et non seulement sur l'état de
travail : `21` fichiers touchés, `0` hors du périmètre de D10. Aucun des douze
non-objectifs n'est enfreint — la maille n'a pas été rejouée, `sim/` n'a pas été
écrit, le graphe G4 n'a pas été régénéré, aucune valeur de `constants.py` n'a
bougé, `qa/checks.py` n'a pas reçu de quinzième entrée, aucune empreinte n'est
citée par valeur, aucun alias nu, aucun barème de jeu, et le Générateur n'a ni
committé, ni poussé, ni créé de branche : les deux seuls commits de la branche
portent les préfixes de rôle de l'orchestrateur (`planificateur:` puis
`generateur:`), et aucune branche parasite `cursor/*` n'a été créée localement.

---

## Observations non bloquantes

Elles sont nommées, elles ne sont pas transformées en rejet.

1. **`diff_apres_seconde_passe` — défaut du brief, pas du travail.** Le brief
   exige que `git status --porcelain -- pipeline/geo/artifacts` soit **vide**
   après la seconde passe. Pris à la lettre, un rôle à qui l'on interdit de
   committer ne peut pas satisfaire cette condition : la réparation elle-même
   apparaît en `porcelain`. Le Générateur a mesuré autre chose — les empreintes
   des trois fichiers avant et après la seconde passe — et l'a **déclaré
   ouvertement** dans son journal et dans la note du compteur, sans maquiller.
   J'ai tranché en refaisant la mesure littérale du brief après le dépôt de
   l'orchestrateur : `porcelain` sur `pipeline/geo/artifacts` est **vide**.
   Les deux lectures donnent `0`. Recommandation au Planificateur : formuler
   cette condition contre l'état d'après la première passe, ou la déléguer
   explicitement à l'Évaluateur, qui juge après commit.
2. **`code_sortie_ecart_avant` est dérivé du texte committé**, pas d'une
   nouvelle exécution — ce qui est la seule conduite possible, l'état « avant »
   n'existant plus. Le premier mot de `pre-edit/check_provenance_avant.txt` est
   `ECART`, ce qui redonne le code `1` sans ambiguïté, et le journal recopie la
   sortie réelle avec son code. Honnête et déclaré.
3. **La liste balayée compte `14` fichiers ; j'en ai balayé `15`** — j'y ai
   ajouté `deliverables/progress.jsonl`, que le Générateur n'avait pas déclaré
   dans son ensemble de balayage bien qu'il l'ait committé. Il est propre :
   aucune suite hexadécimale, aucun alias nu. La valeur du compteur reste `0`
   dans les deux lectures ; seul le dénominateur diffère.
4. **`fichiers_hors_perimetre_modifies` a pour dénominateur `21`**, qui était le
   nombre de lignes de `porcelain` au moment de la génération. Après dépôt,
   `porcelain` est vide ; j'ai donc refait la mesure sur le **diff du commit**,
   qui porte exactement `21` fichiers, dont `0` hors périmètre. Même valeur, même
   dénominateur, par un autre chemin.
5. **Le rejeu de la maille hors dépôt n'a pas été tenté.** Le Générateur le dit
   franchement plutôt que de l'omettre. La rubrique le présentait comme une
   contre-preuve facultative aux deux issues instructives ; le brief interdisait
   le rejeu dans le dépôt et n'imposait pas la copie. Ce n'est donc pas un
   manquement, mais la question « la maille est-elle rejouable bit à bit dans
   cet environnement ? » reste ouverte et mérite un lot dédié.

---

## Ce qui s'est amélioré depuis le lot précédent

Ce brief est à sa première itération : il n'y a rien à comparer à l'intérieur du
lot. Mais il ferme proprement un constat que `019` avait **mesuré sans le
boucher**, et la comparaison avec `019` mérite d'être faite, parce qu'elle
calibre la boucle :

- **La règle n° `12` est tenue du premier coup.** Le lot `019` avait été rejeté en
  première passe, entre autres pour une empreinte citée par valeur. Ici, le
  balayage rend zéro sur l'ensemble des fichiers de prose et de code, et le
  journal raconte même l'incident inverse — une première version du script de
  mesure écrivait sa propre cible de balayage en clair et se comptait une
  infraction. Le texte a été reformulé sans affaiblir la recherche. C'est
  exactement la bonne conduite : on ne désarme pas le contrôle, on corrige la
  prose.
- **La garde est posée avant l'effet, pas après.** L'alignement refuse d'écrire
  quoi que ce soit si le littoral présent n'est pas la sortie que G2-bis
  déclare (code `1`), et distingue l'absence (code `2`). Le contrôle ne peut
  donc pas s'exécuter après le mal qu'il doit prévenir.
- **Les zéros sont des zéros mesurés.** Débordement nul, cellules ajoutées et
  retirées nulles, fichiers hors périmètre nuls : chacun est obtenu par une
  différence d'ensembles ou une aire, et la sentinelle `-1` n'apparaît nulle
  part comme substitut.

## Ce qui a régressé

**Rien.** Les deux suites sont vertes, la maille et le graphe G4 sont
octet-identiques à leur état d'avant le lot, aucune borne n'a été déplacée, et
aucun constat ouvert n'a été supprimé sans avoir été traité.

---

## Retour pour la suite

Aucune correction n'est demandée sur ce lot : il est accepté tel quel. Les
points ci-dessous s'adressent au **Planificateur** et à un lot ultérieur, pas au
Générateur de celui-ci.

1. **Reformuler la condition de déterminisme** (SC5, `diff_apres_seconde_passe`)
   pour qu'elle soit satisfiable par un rôle qui n'a pas le droit de committer.
   Formulation concrète : « les empreintes des fichiers écrits par l'alignement
   sont identiques avant et après la seconde passe » — c'est ce qui a été mesuré
   — et laisser à l'Évaluateur la vérification de `porcelain` vide, qu'il peut
   faire après dépôt. En l'état, la condition littérale piège tous les briefs qui
   réécrivent un artefact suivi par git.
2. **Déclarer `deliverables/progress.jsonl` dans l'ensemble balayé** des futurs
   lots, ou décider explicitement de l'en exclure avec la raison. Un fichier
   committé mais hors balayage est un angle mort, même quand il est propre — et
   ici il l'est.
3. **Ouvrir un lot pour la question laissée en suspens** : la maille de G3 est-
   elle rejouable bit à bit dans cet environnement ? Ce lot-ci a mesuré que la
   géométrie actuelle recouvre encore la terre, ce qui suffisait à interdire le
   remaillage, mais ne dit rien sur la reproductibilité du semis. La conduite
   exigée serait un rejeu **hors dépôt** comparé à
   `deliverables/pre-edit/cell_ids_actifs.txt`, avec les comptes d'identifiants
   ajoutés et retirés.
4. **Les constats ouverts restants sont bien restés ouverts** et attendent leur
   lot : les bornes d'intention de surface et de compacité des zones de mer
   (`24` zones sur `40` hors bornes) et la saturation de `SEA_ZONE_COUNT_MAX`.
   Le jalon E1 n'est **pas** clos, et ce verdict ne le prononce pas.
