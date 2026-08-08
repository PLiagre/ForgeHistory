# Verdict — Brief `005` (refonte visuelle carte)

**Authored**: 2026-08-02T21:05:00
**Author**: forge-evaluateur

Reprise d'une passe Générateur interrompue le `2026-08-01`. Les preuves sur
disque proviennent de deux sessions ; chaque compteur a été jugé sur sa
re-dérivabilité depuis un log cité, sans égard à la session qui l'a produit.

## Mechanical Gate Result

`py harness/verdict_audit.py harness/queue/briefs/005-refonte-visuelle-carte`

Avant l'écriture de ce fichier : exit `1`, `VERDICT: REJECT`, avec `7` checks
verts et les `2` rouges (`verdict_numbers_traceable`,
`verdict_is_not_self_authored`) dépendant uniquement de ce `verdict.md`.
Après l'écriture de ce fichier, le gate est re-joué ; le rapport est cité par
chemin, pas recopié (hard-won rule `12`).

Le gate mécanique n'est **pas** l'obstacle ici : il ne mesure pas ce qui
échoue. Le rejet ci-dessous est prononcé sur la rubrique, pas sur le gate.

## Per-Rubric-Line Verdict

| Success Condition (eval-rubric.md) | PASS/FAIL | Evidence (reconstruite par l'Évaluateur) |
|---|---|---|
| Precondition — pas de lancement Unity sur lockfile tenu | PASS | `deliverables/evidence/unity-lock-checks.log` relu intégralement. Une seule ligne `LOCKFILE_EXISTS=True` est explicitement orpheline avec `UNITY_PROCESS_COUNT=0` ; les deux `Unity.exe` transitoires n'ont jamais tenu le lockfile. Chaque invocation de la session de reprise est précédée d'une vérification combinée horodatée. |
| SC1 (CPU) — orientation prouvée identique à l'export | PASS (avec défauts de méthode consignés) | Défaut réellement présent et réellement corrigé, vérifié **à l'œil** par moi : `v005_orientation_before_fix.png` est intégralement retourné (Écosse/Angleterre en bas, Danemark/Suède en bas, libellés en miroir) ; `v005_orientation_after_fix.png` est nord-en-haut avec libellés lisibles. J'ai re-vérifié moi-même des repères géographiques nommés sur l'après : Écosse au nord de l'Angleterre, Danemark/Suède les plus au nord, Portugal/Castille au sud-ouest, Naples au sud de Milan, aucun libellé en miroir. Corroboration indépendante sur le **vrai chemin joueur** (`v005_after_01_world_neutral.png`, `v005_zoom_01_world.png`, données géo réelles) : Angleterre au nord de la France, Bretagne à l'ouest, golfe de Gascogne au sud-ouest — orientation correcte. Le correctif lu dans le diff est bien **une seule** convention réutilisée (`MapSnapshotExporter.FlipMapBufferRows` appelée par `InGameHud.PresentFrame`), pas deux inversions compensatoires. Compteur `3`/`3`, échantillon `3`. |
| SC1 (GPU) — prouvée ou honnêtement waivée | PASS | Acceptable Waivers row `1` invoquée. L'échec est réel et reproduit frais : `deliverables/evidence/v005-resume-diagnostic-after.log` ligne `12`, `gpu_readback_unavailable reason=palette_refusée…`. Ce n'est pas une hypothèse narrée. **Point de méthode du coordinateur, reconstruit par moi et confirmé** : la ligne de commande de `deliverables/evidence/v005-resume-v1095-no-nographics.log` ne contient effectivement **pas** `-nographics` (bloc `COMMAND LINE ARGUMENTS` relu) ; le run se termine par `V1095BatchRunner: DONE` (pas `DONE_PARTIAL`) ; le fichier de verdicts `unity/game_unity/Logs/v1_095_gpu_map.log` a un mtime frais du `2026-08-02`, postérieur aux changements de ce brief ; les `6` verdicts nommés y lisent tous `VERT` et les chiffres d'accord CPU/GPU figurent bien **dans ce log frais de cette session**, pas seulement dans le brief `003`. |
| SC2 — cadrage initial | PASS (Outcome B, avec réserves) | Re-dérivé par moi depuis `v005-resume-diagnostic-after.log` : emprise jouable `x=[-6,148 ; 22,024] y=[-60,312 ; -31,2]` strictement incluse dans l'emprise monde `x=[-7,556 ; 23,433] y=[-61,44 ; -29,76]` ⇒ `before_count` `0` est correct par confinement, pas par affirmation ; la fenêtre après `x=[-7,275 ; 23,151] y=[-61,152 ; -30,048]` contient aussi l'emprise jouable ⇒ `after_count` `0` correct. Dénominateur réel `50` provinces jouables, nommé. Paire visuelle ouverte : différence réelle mais marginale. |
| SC3 — fluidité du zoom | **FAIL** | Les `5` valeurs `fullRedrawMs` du manifest sont exactement re-dérivables de `deliverables/evidence/v005-zoom-gpu-run.log` (lignes `5`, `8`, `11`, `14`, `17`) et **les `5` dépassent le budget de `33` ms déclaré**. La rubrique exige, en Outcome A, la correction *puis* la re-mesure de la même séquence. Aucun correctif n'a été appliqué, aucune valeur « après » n'existe. Le Générateur le déclare lui-même (`generator-log.md` §2.7, §4). Un aveu honnête reste un manquement : la ligne est FAIL. |
| SC4 — finesse du trait | **FAIL** | Sous le critère que le Générateur a lui-même déclaré (largeur médiane en px), before `9`/`9`/`7` == after `9`/`9`/`7` — re-dérivé par moi des deux logs de phase. Ni Outcome A (aucune valeur « avant » ne tombe en échec puis n'est redressée) ni Outcome B propre (un changement a été fait). J'ai ouvert **les 3 paires déclarées** : `v005_border_zoom_{min,mid,max}_{before,after}` sont visuellement indiscernables. J'ai ouvert la paire `v005_border_crop_{before,after}_feather.png` (seule preuve en géo réelle) : l'arête après reste **visiblement en escalier**. La clause manuelle de la rubrique (« juger l'arête nette et non en escalier ») n'est pas satisfaite par mon propre jugement. |
| SC5 — front rouge lisible et discret | **FAIL** | (b) **discrétion : satisfaite** — comptes exacts re-dérivés des deux logs de phase (`2049` + `985` = `3034` = `LastFrontPixelCount`, miroir parfait entre phases, auto-validant) ; à l'œil, le liseré après lit bien comme un marquage secondaire tout en restant distinguable des frontières noires. (a) **lisibilité : NON PROUVÉE** — `front_rim_legend_reachable_flag` = `1` est établi, de l'aveu même du manifest, par une *relecture de diff git*, pas par la capture fraîche que la source d'échantillon du compteur exige. J'ai cherché la chaîne de légende dans l'intégralité de `deliverables/evidence/` : elle n'apparaît que dans le code source, dans **aucune capture ni aucun log**. Je n'ai pas pu atteindre la légende moi-même. La clause manuelle est non-waivable : présence n'est pas fonction (hard-won rule 7). |
| SC6a — chevauchement `Lois`/`Impôt` éliminé | PASS | Re-dérivé : before `1`+`3`+`3`+`3` = `10` paires, `3` chevauchements ; after `0`+`6`+`3`+`6` = `15` paires, `0` chevauchement. J'ai ouvert **toutes** les captures « après » des scénarios (`02`, `03` défaut, `03` debug, `04`, `05`) : aucune intersection de rectangle de panneau, aucun glyphe tronqué. Le « avant » est authentiquement pré-correctif (build standalone avec `InGameHud.cs` remis à HEAD) et montre bien le panneau `Lois` posé sur le panneau province. |
| SC6b — dump brut `Investir` derrière le mode debug | **FAIL** | La porte elle-même est réelle et je l'ai vérifiée à l'œil : défaut = français seul, debug = français **plus** les jetons bruts appendus — jamais une suppression, exactement le patron `LAWMOD`/`EFF` du brief `004`. Mais la rubrique impose le plancher `>= 2` provinces/scénarios distincts ; `sample_size` = `1` (Île-de-France seule). Le Générateur le déclare honnêtement, ce qui est à son crédit, mais le plancher de la rubrique n'est pas atteint. |
| SC7 — cadence mesurée puis fixée défendablement | **FAIL (traçabilité)** | `harness_tick_advance_unchanged_flag` = `1` : **vérifié par moi** via `git status --porcelain` — `Assets/Tests/SimulationHarness.cs` et `Assets/Scripts/Core/Components/TickControl.cs` n'apparaissent dans aucun diff. Mais `ms_per_tick_measured` **n'est pas re-dérivable du log qu'il cite** : `v005-resume-diagnostic-after.log` contient une série *différente* (premier tick `192`,`316` ms, `avg=9`,`775`, `max=192`,`316`) là où le manifest annonce un premier tick de `191`,`091` et une moyenne de `9`,`497`. J'ai cherché la série du manifest dans tout `deliverables/evidence/` et dans `unity/game_unity/Logs/` : elle n'existe **nulle part** sur disque en dehors de `manifest.json`. Le log du `2026-08-01` contient une troisième série encore (`avg=2`,`886`, `max=14`,`41`), et `generator-log.md` §2.8 mélange les deux (il cite un minimum de `1`,`94` qui vient de cette troisième série, pas de la liste du manifest). Un compteur requis non reproductible est un FAIL, pas un bénéfice du doute. |
| SC8 — galerie montrant chaque correctif Outcome A | PASS | Toutes les images de `deliverables/evidence/gallery/` portant sur une condition ont été ouvertes par moi. Les correctifs Outcome A réellement établis y sont visibles : orientation (SC1), non-chevauchement des panneaux (SC6a), bloc `Investir` en français + porte debug (SC6b), désaturation du liseré (SC5b). Fraîcheur mécanique : `mtime_after_brief` vert. |
| SC8 — chaîne standalone utilisée si constructible | PASS | Aucun waiver row `5` invoqué, et à raison : les captures `v005_after_*` et `v005_zoom_*` proviennent bien du framebuffer standalone (`source=standalone framebuffer` dans `v005-zoom-gpu-run.log`, filigrane `Development Build` visible sur les images). |
| SC9 — suite de référence `100` % verte et fraîche | PASS (avec divulgation) | J'ai parsé la XML moi-même : `total=274 passed=265 failed=8 skipped=1`, `274` − `7` legacy − `1` Skipped = `266`, et `265` + `1` (V1095 sous son invocation correcte) = `266`. **Le 8e rouge est bien celui du brief `003`, pas une régression de ce brief** : j'ai comparé les trois XML (`v003`, `v004c`, `v005d`) et le cas `V1095_Artifacts_And_Verdict` y échoue à l'identique — même assertion, même offset, même ligne source — dans les trois, donc **avant** que le correctif d'orientation de ce brief n'existe. Divulgation honnête : je n'ai **pas** relancé une 4e fois la suite sous Unity ; j'ai reconstruit les comptes par trois voies indépendantes (parsing direct de la XML, comparaison croisée des XML des briefs `003`/`004`, et preuve par `git` que les fichiers de test sont intacts). C'est la seule clause de la rubrique que je n'ai pas exercée à la lettre, et je le dis plutôt que de le masquer. |
| SC9 — `7` fichiers legacy intouchés | PASS | Re-haché par moi les `7` fichiers : identiques au snapshot d'evidence. **Preuve plus forte encore** : `git status --porcelain` sur ces `7` chemins est vide — ils sont octet pour octet identiques au HEAD commité (état de sortie des briefs `003`/`004`). |
| SC10 — verdict artistique littéral | PASS | `A_REVOIR_HUMAINEMENT` présent dans `generator-log.md` et `manifest.json`. `ADOPTÉ` n'y apparaît que sous forme niée (« jamais auto-déclaré », « NON ADOPTÉ ») — jamais comme revendication d'acceptation. |
| Non-Goal — zéro ligne de logique de simulation | PASS | Diff vérifié par moi : `756` insertions / `36` suppressions sur exactement `7` fichiers `Assets/Scripts/Presentation/**`. `TickControl.cs`, `SimulationHarness.cs`, `Assets/Scripts/Economy/**`, `Assets/Scripts/Population/**`, `Assets/Scripts/Core/**` : aucun diff. La clause d'exception nommée n'a jamais été exercée. |
| Non-Goal — mécanisme d'avancement de tick du harnais inchangé | PASS | Voir SC7 : diff vide sur `SimulationHarness.cs`. |
| Non-Goal — pas de nouvel écran/panneau/vue | PASS | La légende SC5 réutilise le mécanisme de libellé au survol existant ; la porte SC6 réutilise `InGameHud.ShowDebugIds` du brief `004`. Aucun nouvel UXML ni asset `Resources/UI/`. |
| Non-Goal — aucun asset/paquet externe, aucune dépendance | PASS | Aucun fichier manifeste de paquet dans le diff. |
| Non-Goal — aucun test affaibli/supprimé/skippé | PASS | Total de la suite inchangé (`274`, identique aux briefs `003` et `004`) ; aucun fichier `Assets/Tests/**` modifié ; le seul ajout, `V005DiagnosticRunner.cs`, n'est décoré d'aucun attribut de test (l'unique occurrence est dans un commentaire) et ne participe donc pas au comptage. |
| Non-Goal — aucun ancrage de parité cité par valeur | PASS | Recherche de littéraux hexadécimaux dans `generator-log.md` et `manifest.json` : aucune occurrence. |
| Non-Goal — rien de corrigé hors SC1–7 | PASS | Diff relu ; les `3` constats du propriétaire sont bien **reportés** en findings, pas corrigés. |
| Non-Goal — aucun `.meta` écrit à la main | PASS | Vérifié et **écarté après examen** : `V005DiagnosticRunner.cs.meta` a la forme minimale à `2` lignes, qui paraît suspecte isolément — mais des `.meta` déjà commités et antérieurs (`SimulationHarness.cs.meta`, `V1095GpuMapTests.cs.meta`) ont exactement la même forme. C'est une convention préexistante du dépôt, pas une écriture manuelle. |
| Non-Goal — aucun `git commit` par le Générateur | PASS | Aucun commit dans la fenêtre de travail ; la pile `git stash` est vide (les cycles push/pop documentés ont bien été refermés). |
| Compteurs — `sample_size` réel partout | PASS (gate) | — |
| Paires `must_differ_from` réellement distinctes | PASS | Re-hachées par moi : les `8` paires diffèrent, et les préfixes SHA correspondent exactement à ceux consignés dans les logs de diagnostic. |

## Overall Verdict: REJECT

`5` lignes numérotées de la rubrique échouent : SC3, SC4, SC5, SC6b, SC7. La
règle de verdict de `eval-rubric.md` est « ACCEPT seulement si **chaque**
ligne numérotée passe ». Aucune de ces `5` n'est rattrapable par la verdeur
des autres compteurs.

Deux d'entre elles échouent pour la raison même que ce brief existe : un
résultat mécaniquement vert mais non regardé. SC4 et SC5(a) auraient été
comptées vertes sur la seule foi de leurs compteurs ; c'est en ouvrant les
images et en cherchant la légende dans les preuves qu'elles tombent.

## Vérification des paires « avant » (git stash)

Demandé explicitement, vérifié indépendamment : les captures « avant » ne
sont **pas** des re-captures de l'état corrigé.
- Les `8` paires ont des SHA256 distincts, re-calculés par moi.
- Les SHA des fichiers « avant » sont ceux consignés dans
  `v005-resume-diagnostic-before.log`, produit pendant que
  `MapSnapshotExporter.cs` était remis à HEAD.
- Cohérence de contenu avec le défaut décrit : la phase « avant » compte
  `2049` pixels de l'ancienne couleur de liseré et `0` de la nouvelle, la
  phase « après » l'exact inverse — impossible à obtenir en re-capturant
  l'état corrigé.
- Côté UI, le « avant » standalone montre bien les jetons bruts et les
  chevauchements de panneaux, absents de l'« après ».
- La pile `git stash` est vide et le diff total fait bien `756` / `36` sur
  les `7` fichiers Presentation.

Réserve de traçabilité : les mtimes de `InGameHud.cs` et
`MapSnapshotExporter.cs` sont postérieurs à la XML SC9, ce qui *paraît*
disqualifier la fraîcheur de la suite. Après examen, ces mtimes sont
attribuables aux cycles `git stash push`/`pop` journalisés, et le nombre de
lignes du diff correspond à ce que le Générateur avait relevé avant le stash.
C'est corroboré, pas prouvé : aucun hachage de l'état pré-stash n'a été
enregistré. À corriger la prochaine fois (voir feedback).

## Boundary Violations

Aucune violation franche de Non-Goal. Trois écarts de périmètre à consigner :

1. **SC2 : la clause « et ne change rien » de l'Outcome B n'a pas été
   suivie.** Le cadrage a été resserré alors que le compteur mesuré était
   `0`/`0`. Le Générateur le déclare lui-même (§2.3, §4), ce qui est correct ;
   le changement reste dérivé des données et à l'intérieur de SC2, donc ce
   n'est pas un « fix hors liste ». Consigné, non sanctionné.
2. **Le monde d'échantillonnage de SC1/SC2/SC4/SC5 n'est pas le monde géo
   du joueur.** Les diagnostics tournent sur le monde EditMode à `50`
   provinces, pas sur la carte pilote à `237` cellules que le joueur voit.
   C'est un monde réellement chargé (pas vide, pas fabriqué), donc pas une
   violation du Non-Goal sur les mondes synthétiques — mais cela affaiblit
   nettement la portée de SC2 et de SC4, dont les griefs vivent précisément
   dans le rendu géo réel.
3. **Le dénominateur de SC1 est gonflé.** Le compteur annonce `3` « points de
   repère géographiques nommés », mais le 3e contrôle est « après diffère
   d'avant » — un contrôle de non-no-op, pas un repère géographique. Et le
   libellé dit « accentué » (`Île-de-France`) alors que le libellé
   effectivement rendu est en ASCII majuscule non accentué. La condition tient
   sur le fond ; l'étiquetage du compteur, non.

## What Improved Since Last Iteration

Par rapport à la galerie du brief `004`, comparée directement par moi
(`unity/game_unity/Captures/v004_after_default/02_country_selected.png`) :

- **L'orientation de la carte est réellement corrigée**, et c'est le gain le
  plus net de ce brief. Les libellés étaient renversés en miroir dans `004` ;
  ils sont droits et lisibles ici, sur le vrai chemin joueur comme sur le
  chemin d'export. Le correctif est structurellement propre : une seule
  fonction de convention réutilisée, pas deux inversions qui se compensent.
- **`Lois` et `Impôt` ne se chevauchent plus**, et l'empilement est désormais
  calculé sur la hauteur rendue plutôt que sur des constantes en dur — la
  cause, pas le symptôme. Le contrôle `HudLayoutProbe` a été élargi à
  **toutes** les paires de panneaux visibles, corrigeant un défaut de
  couverture de l'ancien contrôle.
- **Le bloc `Investir` est passé au français lisible avec une vraie porte
  debug**, du même patron que celui déjà prouvé au brief `004`.
- **Le liseré de front est réellement plus discret** sans cesser d'être
  distinguable.
- **L'honnêteté déclarative de cette passe est nettement au-dessus de la
  moyenne** : SC3 non corrigée, dénominateur `Investir` à `1`, largeur de
  trait sans différence mesurable, écart à la lettre de l'Outcome B de SC2 —
  tout cela est déclaré par le Générateur lui-même, sans maquillage. C'est
  exactement le comportement que la boucle doit récompenser, et c'est ce qui
  a rendu cette évaluation possible. Le rejet ci-dessus porte sur les
  manquements, pas sur leur déclaration.

## What Regressed Since Last Iteration

Aucune régression fonctionnelle détectée. En particulier, le rouge
`V1095GpuMapTests` de la suite fraîche **n'est pas** une régression de ce
brief : il est présent à l'identique dans les XML des briefs `003` et `004`.

Une seule régression de *méthode* par rapport au brief `003` : le brief `003`
avait copié le log de verdict V1095 en evidence **et** l'avait rattaché à un
compteur dédié ; ici, la valeur `ms_per_tick_measured` a été portée dans le
manifest sans que le log correspondant ait survécu à l'écrasement.

## Findings reportés (constats propriétaire, hors périmètre de ce brief)

Vérifiés par moi plutôt que pris pour argent comptant, en ouvrant
`v004_after_default/02_country_selected.png` en regard de la galerie `005`.
Les trois sont **préexistants au brief `005`** et hors SC1–7 ; ils ne pèsent
donc pas contre les conditions de ce brief, et le Générateur les a bien
consignés (`generator-log.md` §3) :

1. **La carte n'occupe qu'environ `43` % de la largeur** aux zooms Pays et
   Province. Mesuré indépendamment par moi (balayage de colonnes sur la
   capture existante) : dernière colonne de carte à `838` sur `1920`, soit
   une fraction de `0`,`437` — cohérent, à la méthode près, avec les
   `0`,`4292` du compteur reporté et avec l'estimation du propriétaire. Le
   même bandeau étroit et la même moitié droite vide sont présents dans la
   capture du brief `004`.
2. **Libellés de provinces surdimensionnés et empilés** — présents à
   l'identique dans la capture `004` (en miroir de surcroît). Distinct de
   SC6, qui ne porte que sur les rectangles de panneaux HUD : ces libellés
   sont dessinés dans la carte. Le contrôle mécanique de ce brief ne pouvait
   pas les voir — portée différente, pas défaut du contrôle.
3. **Bandeau de crédits tronqué par la barre supérieure** — visible sur les
   trois captures du chemin joueur de `005` et déjà sur celle de `004`.

Rappel de la rubrique, qui vaut quel que soit ce verdict : même un ACCEPT
complet n'aurait enregistré qu'un PASS de rubrique. Le jugement artistique
appartient au propriétaire, et il est ici négatif pour la deuxième fois.

## Feedback for Next Iteration

Détail complet, avec correctif précis par point, dans
`feedback/feedback-1.md`.
