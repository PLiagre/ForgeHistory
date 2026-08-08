# Feedback 1 — Brief 005 (refonte visuelle carte)

**Authored**: 2026-08-02T21:10:00
**Author**: forge-evaluateur
**Verdict amont**: REJECT (voir `verdict.md`)

Le gate mécanique passe `9`/`9`. Ce n'est pas ce qui bloque. Les `5`
manquements ci-dessous sont tous des choses qu'un compteur vert ne montre
pas : il a fallu ouvrir les images, ouvrir les logs cités, et chercher une
preuve qui n'existait pas.

Chaque point dit **quoi corriger**, pas seulement ce qui ne va pas.

---

## 1. SC7 — `ms_per_tick_measured` n'est pas re-dérivable du log qu'il cite

**BLOQUANT. C'est le plus grave, parce qu'il touche à la confiance, pas au
rendu.**

Constat : le manifest annonce une série de `30` valeurs commençant par
`191`,`091` ms, de moyenne `9`,`497` et de fraction de budget `0`,`0317`. Le
log cité par ce compteur,
`deliverables/evidence/v005-resume-diagnostic-after.log`, contient une série
**différente** : premier tick `192`,`316`, `ms_per_tick_sample_size=30
avg=9,775 min=2,065 max=192,316`, fraction `0`,`0326`.

J'ai cherché la série du manifest dans tout `deliverables/evidence/` et dans
`unity/game_unity/Logs/` : elle n'existe **nulle part** hors de
`manifest.json`. Le log du `2026-08-01`
(`v005-diagnostic-editmode.log`) contient une **troisième** série encore
(`avg=2,886 min=1,94 max=14,41`). Et `generator-log.md` §2.8 mélange les
deux dernières : il cite « minimum `1`,`94` » (qui vient de la série du
`2026-08-01`) à côté d'une moyenne de `9`,`497` (qui vient de la série
introuvable).

Cause probable, sans reproche : `V005DiagnosticRunner` a été relancé `4`
fois en phase `after` et chaque run **écrase** le même fichier de log. La
valeur retenue au manifest venait d'un run intermédiaire dont le log a été
écrasé par le suivant.

**Correctif attendu, précisément :**
- Soit remplacer les `30` valeurs du manifest par celles réellement présentes
  dans le log cité (`avg=9`,`775`, `max=192`,`316`, fraction `0`,`0326`), et
  corriger `generator-log.md` §2.8 pour que min/moyenne/max viennent tous de
  **la même** série.
- Soit relancer la mesure une dernière fois et copier le log résultant en
  evidence **au moment même** où la valeur est portée au manifest.
- Et, structurellement : faire écrire à `V005DiagnosticRunner` un nom de log
  horodaté ou suffixé (`v005_diag_after_v5.log`) au lieu d'écraser un chemin
  fixe, pour qu'un run intermédiaire ne puisse plus disparaître sous le
  suivant.

Précision utile pour la suite : la **conclusion** de SC7 (Outcome B, cadence
inchangée, `TickControl.DefaultSecondsPerTick` et `RefreshIntervalTicks` non
touchés) n'est pas contestée — je l'ai vérifiée par `git`, et
`harness_tick_advance_unchanged_flag` = `1` est solide. C'est uniquement le
compteur chiffré qui n'est pas traçable.

---

## 2. SC3 — mesurée, diagnostiquée, non corrigée

**BLOQUANT au sens de la rubrique.** La déclaration honnête faite en §2.7 et
§4 est la bonne conduite et je l'enregistre comme telle — mais l'Outcome A
exige la correction *puis* la re-mesure, et il n'y a pas de valeur « après ».

Ce qui est déjà acquis et qu'il ne faut pas refaire : les `5` valeurs
`fullRedrawMs` sont exactes et re-dérivables de `v005-zoom-gpu-run.log`
(lignes `5`, `8`, `11`, `14`, `17`) ; toutes dépassent le budget de `33` ms ;
la cause est identifiée et corroborée par le log lui-même (ligne `24` :
`RenderPoliticalPixels` + `PresentFrame` s'exécutent inconditionnellement
après un `TryRenderGpuBackground` réussi).

**Correctif attendu :**
- Dans `MapDisplaySystem.OnUpdate`, court-circuiter la rastérisation CPU et
  `PresentFrame` quand `GpuBackgroundUsedThisFrame` est vrai pour cette
  frame, en gardant le chemin CPU comme repli quand le GPU échoue.
- Re-jouer **exactement la même** séquence de `5` transitions
  (Monde→Pays→Province→Monde→Pays), avec le même harnais, et publier les `5`
  valeurs « après » à côté des `5` valeurs « avant », dans le même tableau.
- La prudence invoquée pour ne pas tenter ce correctif en fin de session est
  légitime en soi ; mais elle appelle alors une itération dédiée, pas un
  report silencieux. Le garde-fou existe déjà : `V1095GpuMapTests` sous son
  invocation sans `-nographics` re-vérifie précisément la parité
  d'orientation CPU/GPU que l'on craint de casser. Le rejouer avant/après le
  correctif suffit à lever le risque cité.

---

## 3. SC5(a) — la légende du liseré n'est prouvée que par une lecture de diff

**BLOQUANT, et c'est exactement le piège « présence n'est pas fonction ».**

`front_rim_legend_reachable_flag` = `1` a pour `command`, de l'aveu du
manifest lui-même : « `git diff` review of `MapDisplaySystem.cs`'s
hover-label code ». Or la source d'échantillon que le brief définit pour ce
compteur est : « `1` si une légende/infobulle expliquant le marquage est
atteignable depuis la carte elle-même **dans une capture fraîche** ».

J'ai cherché la chaîne de légende (`Front de guerre actif…`) dans
l'intégralité de `deliverables/evidence/` : elle n'apparaît que dans le code
source. Aucune capture, aucune ligne de log ne montre cette légende rendue.
Je n'ai donc pas pu, comme la clause manuelle non-waivable me l'impose,
atteindre la légende et juger si elle explique le marquage dans des termes
qu'un joueur comprend.

Le code peut être parfaitement correct ; ce n'est pas la question. Un flag
d'atteignabilité établi par lecture de code est un flag de présence, pas de
fonction.

**Correctif attendu :**
- Produire une capture où le curseur survole une province présente dans
  `MapSnapshotExporter.LastFrontDrawnProvinceIds`, avec le texte de légende
  **visible à l'écran**.
- À défaut de pouvoir simuler un survol dans la chaîne de capture, faire
  émettre au harnais une ligne de diagnostic textuelle
  (à la manière de `investir_status_default`, qui fonctionne très bien) du
  type `front_rim_hover_label tag=… text='…'`, et la citer.
- Si ni l'un ni l'autre n'est atteignable, ne pas porter `1` : porter le
  sentinelle et déclarer le gap, comme cela a été correctement fait pour le
  readback GPU.

---

## 4. SC4 — le critère déclaré ne distingue pas avant et après

**BLOQUANT.** Sous le critère que le Générateur a lui-même choisi (médiane
des plages sombres contiguës en px), before `9`/`9`/`7` == after `9`/`9`/`7`.
La ligne ne peut donc être ni Outcome A (aucune valeur « avant » ne tombe en
échec puis n'est redressée) ni Outcome B propre (un changement a bien été
appliqué).

J'ai ouvert les `3` paires déclarées : elles sont visuellement
indiscernables. J'ai ouvert `v005_border_crop_{before,after}_feather.png`,
seule preuve en géo réelle : l'arête après reste **visiblement en escalier**.
Un anneau de plume d'`1` px mélangé à `50` % ne rend pas l'arête non
crénelée ; ma propre inspection ne valide pas la clause manuelle.

**Correctif attendu, au choix mais explicitement :**
- Soit assumer un vrai Outcome B sur l'axe largeur : déclarer un seuil
  chiffré de finesse, montrer que `9`/`9`/`7` le satisfait aux `3` niveaux,
  et **retirer le changement de plume** puisqu'il ne prouve rien sous ce
  critère.
- Soit rendre l'axe qualité d'arête mesurable, et pas seulement affirmé :
  compter la proportion de pixels de transition (valeurs intermédiaires
  entre couleur de trait et couleur de province) le long d'un segment de
  frontière, avant et après — un anti-aliasing réel fait monter ce compte,
  un escalier dur le laisse à zéro. Publier les `3` niveaux × `2` phases.
- Et surtout : mesurer sur la **carte géo réelle**, pas sur le monde
  EditMode à `50` provinces. Aux `3` niveaux de zoom testés, ce monde ne
  produit que de longs segments rectilignes où le crénelage ne s'exprime
  pas — la mesure ne pouvait rien détecter par construction.

---

## 5. SC6b — dénominateur à `1` province au lieu des `>= 2` exigés

**BLOQUANT, mais c'est le plus facile à lever.** La porte elle-même est
réelle : je l'ai vérifiée à l'œil sur
`v005_after_03_province_selected_default.png` (français seul) et
`v005_after_03_province_selected_debug.png` (français **plus** les jetons
bruts appendus). C'est bien une porte, jamais une suppression, exactement le
patron `LAWMOD`/`EFF` du brief `004`. Le gap est déclaré honnêtement.

**Correctif attendu :** ajouter une seule sélection de province
supplémentaire à la séquence de capture standalone (n'importe quelle
province autre qu'Île-de-France), et rejouer les deux builds défaut/debug.
Les compteurs `investir_raw_token_*` passeront à un dénominateur de `2`. Le
mécanisme de diagnostic textuel `investir_status_default` /
`investir_status_debug` fonctionne déjà parfaitement — il n'y a rien à
inventer, seulement un scénario à ajouter.

---

## 6. Corrections de manifest — non bloquantes, mais à faire

Deux valeurs du `manifest.json` ne correspondent pas aux logs qu'elles
citent. Elles ne changent aucun verdict, mais elles usent la confiance qu'on
peut accorder au reste :

- `panel_overlap_pairs_before_count` : le `sample_size_note` nomme les paires
  en chevauchement comme `02_country_selected (TaxBar+LawBar)` et
  `05_tax_min (TaxBar+LawBar)`. Le log
  `v005-resume-standalone-before-fix.log` dit `ProvincePanel+LawBar` dans les
  deux cas (lignes `29` et `92`), et c'est bien ce que montre
  `v005_panel_overlap_before.png` que j'ai ouverte. Le compte (`3` sur `10`)
  est juste ; ce sont les noms qui sont faux. À noter que la version du
  manifest fait croire que le grief d'origine `Lois`/`Impôt` a été reproduit
  tel quel avant correctif, ce qui n'est pas ce que la mesure montre.
  Corriger le `sample_size_note` pour qu'il reprenne les noms du log.
- `map_orientation_reference_checks_*_cpu` : le dénominateur `3` compte comme
  « point de repère géographique » un contrôle qui n'en est pas un (« après
  diffère d'avant » est un contrôle de non-no-op). Et le libellé décrit
  `Île-de-France` comme un « libellé accentué » alors que le libellé rendu
  est `ILE-DE-FRANCE`, en ASCII majuscule non accentué — donc valable comme
  contrôle d'asymétrie/miroir, pas comme contrôle d'accent. Nommer les
  repères réellement vérifiés (par exemple : Écosse au nord de l'Angleterre,
  Danemark/Suède les plus au nord, sens de lecture d'un libellé asymétrique)
  et ajuster le dénominateur en conséquence.

---

## 7. Traçabilité des cycles `git stash` — à durcir pour la prochaine passe

Le procédé lui-même est bon et je l'ai validé : les captures « avant » sont
authentiquement pré-correctives (SHA distincts, comptes de couleur en miroir
parfait entre phases, jetons bruts et chevauchements présents côté « avant »
et absents côté « après »), la pile de stash est vide, et le diff fait bien
`756` insertions / `36` suppressions sur les `7` fichiers Presentation.

Un point reste corroboré plutôt que prouvé : `InGameHud.cs` et
`MapSnapshotExporter.cs` ont un mtime **postérieur** à la XML de la Success
Condition `9`, ce qui laisse croire, au premier regard, que la suite de
référence a été jouée avant les derniers changements de code. En réalité ces
mtimes viennent des `git stash push`/`pop` journalisés, et le décompte du
diff correspond à ce qui avait été relevé avant le stash. Mais aucun hachage
de l'état pré-stash n'a été enregistré, donc rien ne le **prouve**.

**Correctif attendu :** avant chaque `git stash push` et après chaque
`git stash pop`, écrire le SHA256 du ou des fichiers concernés dans un log
d'evidence. Deux lignes suffisent à transformer une corroboration en preuve,
et à éviter que la fraîcheur de la suite de référence soit questionnable.

---

## Ce qui est acquis et qu'il ne faut pas refaire

Pour que l'itération suivante ne dépense pas son budget deux fois :

- **SC1 (CPU et GPU)** est prouvée et vérifiée indépendamment, y compris sur
  le vrai chemin joueur. Le correctif d'orientation est structurellement
  propre (une seule convention réutilisée). Ne pas y retoucher.
- Le rouge `V1095GpuMapTests` de la suite fraîche est **définitivement**
  attribué : j'ai comparé les XML des briefs `003`, `004` et `005` — même
  assertion, même offset, même ligne, dans les trois. Ce n'est pas une
  régression de ce brief. La question est close, ne pas la rouvrir.
- **SC6a** est acquise et vérifiée à l'œil sur tous les scénarios « après ».
- **SC5(b)** (discrétion du liseré) est acquise et vérifiée à l'œil.
- **SC9** (`266`/`266`) et les `7` fichiers legacy intacts sont acquis, ces
  derniers prouvés par `git` lui-même.
- **SC10** est correct.
- Les `3` constats du propriétaire sont correctement qualifiés de
  préexistants — je l'ai vérifié moi-même en ouvrant la capture du brief
  `004` — et correctement reportés en findings. Ne pas les corriger dans ce
  brief : les Non-Goals l'interdisent explicitement.

## Note de calibrage

La qualité déclarative de cette passe est nettement au-dessus de la moyenne :
SC3 non corrigée, dénominateur `Investir` insuffisant, largeur de trait sans
différence mesurable, écart à la lettre de l'Outcome B de SC2 — tout cela a
été déclaré par le Générateur, sans maquillage, avant que je l'ouvre. C'est
ce qui a rendu cette évaluation rapide et vérifiable.

Le rejet porte sur les manquements, jamais sur le fait de les avoir déclarés.
La bonne réponse à ce feedback n'est pas de déclarer moins, c'est de fermer
les `5` points bloquants ci-dessus.
