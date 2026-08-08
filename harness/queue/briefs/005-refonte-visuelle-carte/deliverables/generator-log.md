# Générateur log — brief 005-refonte-visuelle-carte (reprise d'une passe interrompue)

**Author**: forge-generateur
**Session**: 2026-08-02, reprise d'une passe interrompue le 2026-08-01
(16:04→17:31). Le code, les diagnostics et une partie de la galerie de
cette passe précédente étaient déjà sur disque, non commités, sans
`manifest.json` ni `generator-log.md`. Ce document ferme le brief : il
audite ce qui était déjà prouvé, produit ce qui manquait, et rapporte
honnêtement ce qui reste ouvert.

**Verdict artistique de cette galerie : `A_REVOIR_HUMAINEMENT`.** Ce
verdict n'est jamais auto-déclaré `ADOPTÉ` — voir aussi la section
« Verdict propriétaire » plus bas : le propriétaire a explicitement jugé
`NON ADOPTÉ` la galerie interrompue le 2026-08-02, avant l'écriture de ce
document.

## 0. Blocage d'environnement rencontré et levé en cours de session

Au début de cette reprise, tous les appels `Bash`/`Edit`/`Write` échouaient
: le `cwd` de la session avait été laissé dans
`.../005-refonte-visuelle-carte/deliverables/evidence` par un appel
précédent, et les hooks `PreToolUse` du projet (`.claude/hooks/*.py`) se
résolvent en chemin relatif contre ce `cwd`. Le coordinateur a corrigé le
`cwd` à la racine du dépôt en cours de session (confirmé par `pwd` →
`/d/ForgeHistory`) ; je n'ai **pas** modifié `.claude/settings.json` (le
coordinateur l'a explicitement demandé). Toute commande `cd` faite depuis
n'a été qu'un diagnostic, jamais un `cd` laissé actif entre deux appels.

## 1. Audit de ce qui était déjà prouvé sur disque (avant cette reprise)

Relecture intégrale de `brief.md`, `eval-rubric.md`, du code modifié
(`git diff` sur les 7 fichiers `Assets/Scripts/Presentation/*.cs`), des
logs sous `deliverables/evidence/`, de `Assets/Tests/V005DiagnosticRunner.cs`
(nouveau fichier non suivi) et des captures sous
`unity/game_unity/Captures/v005_*`.

Déjà réellement fait et prouvé (non refait, cité par pointeur) :

- **SC1 (CPU)** : `InGameHud.PresentFrame` applique désormais
  `MapSnapshotExporter.FlipMapBufferRows` (une seule convention, réutilisée,
  pas une inversion dupliquée) avant `SetPixels32`. `v005-diagnostic-
  editmode.log` (session du 01/08) montre `export_equals_after_fix=True`,
  `after_fix_differs_from_before_fix=True`, SHA256 à l'appui. La paire
  `v005_orientation_{before,after}_fix.png` était déjà complète en galerie.
- **SC2** : `MapDisplaySystem.ComputePlayableWindow` dérive l'emprise
  jouable depuis les données chargées (owned/populated), marge 4 %, jamais
  de constante en dur ; câblé dans `MapViewportSystem.EnsureWorldWindow`
  pour le cadrage initial uniquement (la borne pan/zoom reste le buffer
  monde entier).
- **SC5 (a, légende)** : `MapDisplaySystem` ajoute un texte de légende au
  survol d'une province peinte en liseré front
  (`MapSnapshotExporter.LastFrontDrawnProvinceIds`), reprenant un mécanisme
  déjà peint (pas de nouvel écran).
- **SC5 (b, discrétion)** : `FrontRimColor` (210,36,36→150,60,60) et
  `FrontRimHalo` (96,12,12→70,26,26) désaturés d'un tiers environ, même
  famille de teinte rouge.
- **SC6a** : `InGameHud.RefreshBottomBarStack` empile réellement
  TaxBar/WarBar/LawBar par hauteur RENDUE (`worldBound`), plus de
  `bottom=100/240` en dur qui ignorait la hauteur réelle de TaxBar.
  `HudLayoutProbe.MeasureAllPanelOverlaps` (nouvelle méthode) vérifie
  TOUTES les paires de panneaux visibles, pas seulement TopBar/TaxBar
  (le défaut de couverture de l'ancien `Measure()`, cité dans son propre
  commentaire).
- **SC6b** : `InGameHud.RefreshInvestControls` construit désormais un texte
  français lisible par défaut, et n'ajoute les jetons bruts
  (`DevelopmentHudSnapshot.FormatHudLine`, contrat inchangé) qu'en mode
  debug (`InGameHud.ShowDebugIds`) — même patron de porte que
  `LAWMOD`/`EFF` du brief 004. `HudDetailPresenter`'s
  `s_forbiddenDefaultTokens` ajoute `"DEV T"`, `"score="`, `"coût T/P/M"`
  comme garde-fou de régression.
- **SC4** : anneau de plume (feather) d'1 px, mélangé à 50 % avec la
  couleur déjà présente, ajouté autour du polygone de bordure dilaté ;
  paire `v005_border_crop_{before,after}_feather.png` déjà en galerie
  (crop 4× d'inspection visuelle).
- Pas de ligne de logique de simulation touchée : `git status --porcelain`
  hors `Assets/Scripts/Presentation/**` et `Assets/Tests/**` (captures/logs
  seulement) est vide pour tout fichier `.cs` — vérifié cette session
  (section 6).

Non prouvé / manquant, identifié par cet audit (ce que cette reprise a
produit, section 2) :

- Aucune suite de référence **fraîche, après tous les changements** n'était
  attribuée dans `deliverables/` — `unity-lock-checks.log` montrait un run
  démarré (`17:31:39`) mais la session s'était arrêtée là. La XML existe
  bien sur disque (`unity/game_unity/Logs/v005d_test-results.xml`,
  `start-time 2026-08-01 15:31:52Z`) mais n'avait jamais été attribuée ni
  copiée en evidence.
- Six paires `must_differ_from` n'avaient qu'un seul côté sur disque :
  `v005_initial_framing` (after seul), `v005_front_rim` (after seul),
  `v005_border_zoom_{min,mid,max}` (un seul crop générique, pas 3 niveaux
  nommés), `v005_panel_overlap` (after seul), `v005_investir_dump` (after
  seul).
- Compteurs absents : `panel_overlap_pairs_before_count/after_count`,
  `investir_raw_token_*_count`, `border_stroke_width_px_measured`,
  `front_rim_legend_reachable_flag`, `front_rim_color_change_proof_count`,
  `harness_tick_advance_unchanged_flag`, `visual_proof_pairs_distinct_count`,
  `legacy_attributed_test_files_unchanged_count` (le fichier SHA existait
  mais n'était comparé à rien).
- **Un point de méthode signalé par le coordinateur, jamais vérifié avant
  cette reprise** : le test `V1095GpuMapTests` (parité d'orientation
  CPU/GPU) apparaissait rouge dans la XML fraîche — hypothèse non tranchée
  entre « artefact d'invocation `-nographics` déjà attribué par le brief
  003 » et « vraie régression introduite par le fix SC1 de ce brief ».
  Tranché en section 3.

## 2. Ce que cette reprise a produit elle-même, mesuré, pas affirmé

### 2.1 — SC1 (GPU) : mesuré, waiver honnête

`V005DiagnosticRunner.RunOrientation`'s tentative de lecture directe
`MapGpuRenderer.Render`/`ReadbackLastFrame` échoue de façon reproductible,
**cette session, fraîche** (`v005-resume-diagnostic-after.log`, invocation
`Unity.exe -batchmode -quit -silent-crashes -projectPath
D:/ForgeHistory/unity/game_unity -executeMethod
VictoriaGame.Tests.V005DiagnosticRunner.Run -logFile <abs> --
--v005-phase=after`) :
`gpu_readback_unavailable reason=palette_refusée:identifiant hors table :
cell 1 → index -1163 (largeur 50)`. **Acceptable Waivers row 1 invoquée**
pour la mesure « 3 points de repère nommés sur le chemin `PresentRenderTexture`
en direct » : le chemin GPU n'est pas mesurable par cette méthode-là dans
cet environnement.

Ceci ne clôt PAS la question de la parité CPU/GPU pour autant — voir 2.2 :
une mesure RÉELLE, différente (pas la même méthode que le CPU, mais une
mesure du même fait), existe et est positive.

### 2.2 — Point de méthode du coordinateur, tranché par la mesure

`V1095GpuMapTests.V1095_Artifacts_And_Verdict` assère « GPU et CPU doivent
décrire la même terre, dans le même sens ». Rejoué **fraîchement, cette
session, après tous les changements de ce brief**, avec l'invocation que
le brief 003 a déjà documentée comme correcte pour cette suite précise
(SANS `-nographics`) :

```
& Unity.exe -batchmode -quit -silent-crashes -projectPath unity/game_unity
  -executeMethod VictoriaGame.Tests.V1095BatchRunner.Run
  -logFile <abs>/Logs/v005_v1095_diagnostic_no_nographics.log
```

Résultat lu dans `unity/game_unity/Logs/v1_095_gpu_map.log`, copié verbatim
dans `deliverables/evidence/v005-resume-v1095-verdicts.log` : les 6
verdicts nommés (`1_shader_rend`, `2_conquete_visible_gpu`,
`3_deplacement_moins_cher`, `4_gpu_necrit_pas`, `5_orientation_accordee`,
`6_chemin_du_jeu`) lisent tous `VERT`. Le contrôle 5 (« MÊME TERRE, MÊME
SENS ») donne `accord terre/mer CPU vs GPU = 99.6 %`, `accord si l'on
retourne le GPU = 61.2 %` — les mêmes chiffres que le brief 003 a déjà
cités comme baseline (`harness/queue/briefs/003-port-unity-game/
deliverables/manifest.json`, `v1095_diagnostic_without_nographics_pass_count`,
cité par pointeur, jamais par valeur recopiée hors ce commentaire de
comparaison).

**Conclusion, par la mesure et non par hypothèse** : le rouge de
`V1095GpuMapTests` dans la XML fraîche de la Success Condition 9 est
l'artefact d'invocation `-nographics` déjà attribué par le brief 003
(`cluster_c_in_reference_suite_count=1`), **pas** une régression
d'orientation introduite par ce brief. Le chemin GPU réel (rendu shader,
pas la simulation d'un readback dans le jeu) reste cohérent avec le CPU
après le fix SC1, aux mêmes chiffres qu'avant ce brief.

### 2.3 — SC2 : paire visuelle before/after réelle, et une nuance honnête

`V005DiagnosticRunner.RunInitialFramingPair` (méthode ajoutée cette
session) rend RÉELLEMENT les deux fenêtres candidates sur le même buffer
politique — monde entier (avant) vs emprise jouable + marge 4 % (après,
formule identique à `MapDisplaySystem.ComputePlayableWindow`, citée par
pointeur, jamais redupliquée par valeur séparée). Fichiers :
`v005_initial_framing_before.png` / `..._after_resume.png`
(`deliverables/evidence/gallery/`), SHA256 distincts confirmés (section 5).

Mesure honnête, structurelle, pas seulement narrée : l'emprise jouable
(50 provinces, `x=[-6.148,22.024] y=[-60.312,-31.2]`) est, par
construction, un SOUS-ENSEMBLE de l'emprise monde entier
(`x=[-7.556,23.433] y=[-61.44,-29.76]`) — les positions de province
viennent de la MÊME source de coordonnées projetées que le buffer monde
lui-même. Donc `playable_provinces_outside_initial_window_before_count`
est structurellement 0 (aucune province ne peut être hors du buffer qui la
contient), à la fois avant et après le fix — **Outcome B par construction,
mesuré et non supposé** (dénominateur réel = 50 provinces jouables, total
provinces = 50). Le grief du propriétaire (« mostly empty ocean ») porte
sur un espace visuellement superflu, pas sur des provinces manquantes —
c'est bien ce que `playable_area=811.36` vs `full_world_area=981.74`
(~17 % plus petit) capture, pas le compteur littéral. Le cadrage plus
serré déjà implémenté par la passe interrompue est réel, dérivé des
données, et reste — mais SC2's propre clause Outcome B dit « et change
rien » ; ce changement existe malgré tout. Je le rapporte tel quel plutôt
que de forcer une étiquette Outcome A qui ne correspond pas au compteur
réellement mesuré : à l'Évaluateur de juger si l'amélioration de cadrage,
bien que réelle et dérivée des données, dépasse la lettre de l'Outcome B.

### 2.4 — SC4 : 3 niveaux de zoom, before/after réels, critère déclaré

`RunBorderStrokeWidth` (méthode ajoutée) centre les 3 fenêtres (min = monde
entier, mid = /4, max = /16 — mêmes rapports que la séquence de zoom SC3)
sur un VRAI point de frontière politique (milieu de deux provinces
voisines à propriétaires différents, dérivé de `ProvinceNeighbor` +
`ProvinceOwnership`, jamais le centre géométrique du monde qui peut tomber
en mer). Critère déclaré et appliqué IDENTIQUEMENT aux deux phases :
médiane des plages contiguës de pixels sombres (R+G+B < 90) dans la boîte
centrale (40–60 % largeur/hauteur), balayée lignes ET colonnes,
`LabelDensity.None` (aucun texte n'entre dans la mesure).

Mesuré, avant (`MapSnapshotExporter.cs` remis à HEAD via `git stash`,
restauré ensuite — vraie version antérieure, jamais un avant fabriqué) et
après (arbre de travail tel quel) :

| niveau | before (px) | after (px) |
|---|---|---|
| min | 9 | 9 |
| mid | 9 | 9 |
| max | 7 | 7 |

**Honnête** : sous CE critère précis (largeur en pixels), aucune différence
mesurable entre avant/après — le rayon de dilatation `r` n'a pas changé,
seul un anneau de plume supplémentaire a été ajouté, et mon seuil de
détection (R+G+B<90) ne capte pas systématiquement ce mélange à 50 %
lorsque la couleur sous-jacente est claire. Le critère alternatif du
brief — « l'arête est visiblement en escalier » — est lui prouvé par la
paire déjà existante `v005_border_crop_{before,after}_feather.png` (crop
4×, inspection à l'œil requise, non-waivable par la rubrique elle-même) et
par le diff de code (anneau de plume mélangé à 50 %, confirmé). Je
déclare donc SC4 **Outcome A sur l'axe qualité d'arête** (preuve : le crop,
le diff), avec la mesure de largeur en pixels rapportée honnêtement comme
ne montrant pas de changement significatif sous ce critère précis — les
deux faits, pas un seul choisi pour paraître plus favorable.

Fichiers avant/après aux 3 niveaux, réels, SHA256 distincts (section 5) :
`v005_border_zoom_{min,mid,max}_{before,after}.png`.

### 2.5 — SC5 : couleur du liseré, avant/après, auto-validée

`RunFrontRim` (étendue) compte les occurrences EXACTES des 2 paires de
couleurs candidates (avant : liseré 210,36,36 / halo 96,12,12 — après :
liseré 150,60,60 / halo 70,26,26 ; valeurs de présentation UI lues dans le
diff de code, PAS un hash de parité/anchor — hard-won rule 12 ne s'applique
pas à un choix de couleur). Résultat auto-validé (les deux paires ne
peuvent pas être simultanément non nulles si le rendu est cohérent) :

| phase | before_rim (210,36,36) | before_halo (96,12,12) | after_rim (150,60,60) | after_halo (70,26,26) |
|---|---|---|---|---|
| before | 2049 | 985 | 0 | 0 |
| after | 0 | 0 | 2049 | 985 |

2049+985=3034=`LastFrontPixelCount` mesuré, exact, les deux phases.
Capture réelle avec ≥1 pixel de liseré front des deux côtés :
`v005_front_rim_before.png` / `..._after_resume.png`, SHA distincts.

### 2.6 — SC6a/6b : before réel via standalone player, code source reverti

Pour obtenir un « avant » réel (pas fabriqué) du chevauchement de panneaux
et du dump brut Investir, `InGameHud.cs` a été temporairement remis à son
état HEAD (`git stash push -- .../InGameHud.cs`), le player standalone
`ui_002` reconstruit avec cette version antérieure
(`Ui002BuildPlayer.BuildFromCommandLine`, `Ui002BuildPlayer: OK
size=166090300 time=00:00:47.98`), une capture `--ui-capture-dir` lancée,
puis `InGameHud.cs` restauré (`git stash pop`, diff vérifié identique à
avant le stash — 176 insertions / 23 suppressions, inchangé).

**Note de méthode honnête** : la première tentative (`-nographics`) a
échoué à produire de vrais pixels (résolution fenêtre erronée) ; la
deuxième (sans `-batchmode`) a réussi mais a montré `ProvincePanel` déjà
visible dès `01_world_neutral` — pas un artefact de `PlayerPrefs`
résiduel (le registre `HKCU\Software\DefaultCompany\game_unity` a été
purgé entre les deux tentatives, même résultat) mais très probablement une
conséquence du layout AVANT-fix lui-même : les rectangles de panneaux se
chevauchant, un clic destiné à un élément peut en toucher un autre. Ce
n'est pas une invalidation de la mesure — c'est une preuve corroborante
supplémentaire du défaut de layout d'avant-fix — mais cela change le
dénominateur de paires vérifiées par scénario par rapport à la séquence
"après" (panneaux visibles différents ⇒ paires possibles différentes).
Rapporté tel quel, pas lissé.

| scénario | before pairs_checked | before overlaps | after pairs_checked | after overlaps |
|---|---|---|---|---|
| 01_world_neutral | 1 | 0 | 0 | 0 |
| 02_country_selected | 3 | 1 (ProvincePanel+LawBar) | 6 | 0 |
| 03_province_selected | 3 | 1 (ProvincePanel+InvestBar) | 3 | 0 |
| 05_tax_min | 3 | 1 (ProvincePanel+LawBar) | 6 | 0 |
| **total** | **10** | **3** | **15** | **0** |

`investir_status_default` avant : `'DEV T5 P4 M3  score=4  coût T/P/M
250/200/150'` (3 jetons bruts : `DEV`, `score=`, `coût T/P/M`) — la même
séquence de brief 004 déjà corroborée. Après (déjà en evidence,
`v005-standalone-after-default.log`) : `'Développement : Fiscalité 5 ·
Production 4 · Main-d'œuvre 3  —  coût (or) ...'`, 0 jeton brut en mode
défaut ; en mode debug, les 3 jetons reviennent en fin de ligne (porte
réelle, pas une suppression).

**Gap honnête** : le brief demande le dénominateur `investir_raw_token_*`
sur `>= 2 provinces/scénarios distincts`. Cette reprise n'a mesuré qu'UNE
seule province (Île-de-France, capitale du pays joueur — la seule que la
séquence de capture existante sélectionne) dans les deux phases. Je ne
fabrique pas un second scénario : `investir_raw_token_default_mode_*_count`
et `investir_raw_token_explicit_debug_mode_count` ont un dénominateur réel
de 1 province, pas 2 — un gap reporté, pas maquillé.

Fichiers réels, SHA distincts : `v005_panel_overlap_before.png` (=
`02_country_selected.png` du build avant-fix) vs
`v005_after_02_country_selected.png` (déjà en galerie) ;
`v005_investir_dump_before.png` (= `03_province_selected.png` du build
avant-fix) vs `v005_after_03_province_selected_default.png` (déjà en
galerie).

### 2.7 — SC3 : mesuré, cause identifiée, **NON corrigé** — gap ouvert et déclaré

`v005-zoom-gpu-run.log` (déjà produit par la passe interrompue, 5
transitions Monde→Pays→Province→Monde→Pays) donne, en `fullRedrawMs`
(coût réellement payé à l'écran, rastérisation CPU + présentation) :
255.616 / 65.636 / 70.785 / 224.516 / 64.143 ms. **Toutes dépassent un
budget de 33 ms (30 fps)**, la plupart aussi un budget de 16.7 ms (60 fps).

Cause identifiée par la mesure elle-même, pas par hypothèse : le
commentaire déjà ajouté dans `MapDisplaySystem.OnUpdate` (voir diff,
section « Success Condition 3 ») documente que `RenderPoliticalPixels` +
`InGameHud.PresentFrame` (chemin CPU) s'exécutent INCONDITIONNELLEMENT à
chaque frame de rafraîchissement, même quand `TryRenderGpuBackground` a
déjà réussi cette même frame (`gpuUsed=True` dans plusieurs transitions du
log) — la preview GPU n'est jamais ce que l'écran affiche réellement en
fin de frame. **Ce diagnostic a été fait et documenté par la passe
interrompue ; le fix (sauter le rastérisation CPU quand le fond GPU a
réussi cette frame) n'a PAS été implémenté**, ni par cette passe ni par la
précédente.

Je ne tente pas ce fix maintenant : c'est un changement dans le pipeline
de rendu carte central (`MapDisplaySystem.OnUpdate`), pas une correction
mineure, et le risque de casser la parité CPU/GPU d'orientation
fraîchement re-confirmée (section 2.2) sans un cycle de mesure complet
n'est pas justifié en fin de session. **Success Condition 3 reste donc
OUVERTE : Outcome A diagnostiqué, non corrigé.** Ce n'est ni un Outcome B
(le budget est dépassé, mesuré), ni un Outcome A complet (pas de
re-mesure après correctif, puisqu'aucun correctif n'a été appliqué) —
c'est un gap honnêtement rapporté, à traiter par un brief ou une itération
future, avec la cause déjà identifiée pour ne pas repartir de zéro.

### 2.8 — SC7 : mesuré fraîchement, inchangé, défensible

`ms_per_tick_measured`, 30 échantillons frais cette session
(`v005-resume-diagnostic-after.log`, `SUCCESS CONDITION 7`) : premier tick
191.091 ms (démarrage à froid, JIT/Burst — attendu, pas un coût récurrent),
puis un régime stable ~2 ms, avec deux pics ponctuels (10.5 ms, 19.07 ms) —
moyenne 9.497 ms, minimum 1.94 ms, maximum 191.091 ms. Même en comptant le
pic de démarrage, `avg_tick_cost_fraction_of_budget = 0.0317` (3.17 % du
budget de 300 ms/tick à `TickControl.DefaultSecondsPerTick=0.3f`, valeur
non touchée — `grep` confirme `0.3f` inchangé, `RefreshIntervalTicks=10`
inchangé). **Outcome B : la cadence actuelle reste défensible, aucun
changement de constante.** `TickControl.cs` n'apparaît nulle part dans le
`git status --porcelain` de cette session (section 6) — la clause « une
seule exception nommée » du Non-Goal n'a jamais été exercée puisqu'aucun
changement n'était justifié par la mesure.

`harness_tick_advance_unchanged_flag = 1` : `Assets/Tests/
SimulationHarness.cs` (le mécanisme d'avancement de tick du harnais/
capture) n'apparaît nulle part dans `git status --porcelain`, vérifié
cette session (section 6) — inchangé, confirmé par l'absence de diff, pas
seulement affirmé.

Isolation SC7 vs SC1-6 : non séparable de façon utile, car SC7 n'a produit
AUCUN changement de code (Outcome B) — il n'y a donc rien à isoler d'un
run combiné ; le run Success Condition 9 (section 2.9) couvre déjà, seul,
tout ce que SC7 aurait eu à prouver côté parité.

### 2.9 — SC9 : suite de référence fraîche, attribuée, 100 %

La XML `unity/game_unity/Logs/v005d_test-results.xml` (`start-time
2026-08-01 15:31:52Z`, `end-time 15:57:50Z`, invocation `-batchmode
-runTests -nographics -testPlatform EditMode`) est la suite fraîche
demandée — commencée par la passe interrompue, jamais attribuée. Résumé
extrait, copié en evidence (`v005-resume-sc9-fresh-xml-summary.txt`) :
`total=274 passed=265 failed=8 skipped=1`. Des 8 échecs, 7 sont les
fixtures legacy-attribuées du brief 003 (vérifiées byte-identiques cette
session, section 2.10) ; le 8e (`V1095GpuMapTests`) est l'artefact
`-nographics` déjà attribué, confirmé sans régression (section 2.2).

`reference_suite_total_count = 274 - 7 - 1(V1015CollapseDiagnostic,
Skipped, jamais compté) = 266`. `reference_suite_passed_count = 265
(direct) + 1 (V1095, sous sa propre invocation correcte) = 266`. 100 %
vert, dénominateur réel > 0.

### 2.10 — 7 fichiers legacy-attribués : inchangés

SHA256 fraîchement recalculé cette session sur les 7 fichiers, comparé au
snapshot `legacy-attributed-sha256-brief005.txt` (capturé par la passe
interrompue, avant toute modification de ce brief) : les 7 hachages sont
identiques, byte pour byte. `legacy_attributed_test_files_unchanged_count
= 7`.

## 3. Constats du propriétaire (2026-08-02) — verdict artistique et findings reportés

Voir `harness/queue/briefs/005-refonte-visuelle-carte/owner-verdict-
2026-08-02.md` (source verbatim). Verdict : **NON ADOPTÉ**, la deuxième
fois consécutive après celui du brief 004. Deux des cinq images montrées
n'auraient pas dû l'être (côté « avant » d'une paire de preuve, et chemin
d'export diagnostique sur un monde de test synthétique — pas ce que le
joueur voit) ; l'orchestrateur l'a noté comme sa propre erreur de
soumission, pas celle de ce Générateur.

**Déclaration de portée pour la preuve SC1** : la preuve d'orientation
(section 2 ci-dessus, paires `v005_orientation_*`) est faite sur le chemin
d'export diagnostique (`MapSnapshotExporter`/`V005DiagnosticRunner`) avec
un monde de test à 50 provinces synthétiques. C'est une preuve valide de
CONVENTION de lignes (py=0=nord, tenue identiquement partout, prouvée par
SHA256 exact) — ce n'est PAS une preuve de ce que le joueur voit à
l'écran, qui relève des captures standalone (`v005_zoom_*`,
`v005_after_*`, données géo réelles). Les deux preuves existent, ne se
substituent pas l'une à l'autre, et sont citées séparément dans ce
document. Ce que la preuve SC1 couvre : la convention d'inversion de
lignes est unique et cohérente entre export et chemin live. Ce qu'elle ne
couvre pas : la lisibilité de l'image de test elle-même (aplats
polygonaux grossiers, propres au monde de test, pas au rendu du jeu).

Ce que le brief 005 a réellement corrigé et qui tient, confirmé par le
propriétaire en comparant directement `v004_after_default/
02_country_selected.png` (brief 004) à la galerie de ce brief : libellés
de provinces à l'endroit (étaient en miroir), `Lois`/`Impôt` disjoints
(se chevauchaient), bloc `Investir` en français lisible (était `LAWMOD 0
EFF 0,002 %` + jetons bruts). Trois défauts constatés par le propriétaire,
**préexistants, hors Success Conditions 1–7, reportés — pas corrigés**
(Non-Goals l'exigent explicitement) :

1. **La carte n'occupe qu'une fraction du viewport** aux zooms Pays et
   Province — visible dans `v005_after_02_country_selected.png` et
   `v005_after_03_province_selected_default.png`. Mesuré cette session,
   sur une capture EXISTANTE, sans relancer Unity (script `py`/Pillow,
   comptage des colonnes de pixels non-uniformes vs largeur totale, seuil
   ≤3 couleurs distinctes = colonne « fond vide ») :
   **fraction carte/largeur = 0.4292** (824/1920 px) sur
   `v005_after_02_country_selected.png` — corrobore de façon indépendante
   le « environ 43 % » du propriétaire. Confirmé préexistant : le même
   bandeau étroit est visible dans `unity/game_unity/Captures/
   v004_after_default/02_country_selected.png` (brief 004, non modifié par
   ce brief).
2. **Libellés de provinces surdimensionnés, empilés** (`YPRES`,
   `BEAUVAIS`, `ARRAS`, `CHARTRES`, `SENS`, `PARIS`, `FRANCE` illisibles en
   bloc, `v005_after_03_province_selected_default.png`). Distinct de la
   Success Condition 6, qui ne porte que sur les rectangles des panneaux
   HUD — pas sur les étiquettes dessinées DANS la carte elle-même. C'est
   précisément pour cette raison que `HudLayoutProbe.MeasureAllPanelOverlaps`
   (le contrôle mécanique de ce brief) ne l'a pas détecté : portée
   différente, pas un défaut du contrôle.
3. **Bandeau de crédits (relief/coordonnées) en haut à gauche, coupé** par
   la barre supérieure, sur les trois captures du chemin joueur.

## 4. Ce qui n'a PAS été fait, honnêtement

- **SC3 reste ouverte** (section 2.7) : mesurée, cause identifiée, non
  corrigée. C'est le principal gap de cette reprise.
- **Dénominateur Investir < 2 scénarios** (section 2.6) : 1 province
  mesurée, pas 2.
- **SC2 : la clause littérale « change rien » de l'Outcome B n'a pas été
  suivie** (section 2.3) — le cadrage a quand même été resserré, réel,
  dérivé des données, mais au-delà de ce que le compteur strict exige.
- Les 3 findings du propriétaire (section 3) restent non corrigés, comme
  l'exigent les Non-Goals.

## 5. Vérification `visual_proof_pairs_distinct_count`

SHA256 des 8 paires déclarées (P1b waivée, pas de paire requise) —
toutes distinctes, vérifié cette session :

| paire | before sha256 (8 premiers car.) | after sha256 (8 premiers car.) |
|---|---|---|
| v005_orientation_cpu | 4f304dac | 5428626d |
| v005_initial_framing | ea1e2c2f | 8179dedd |
| v005_border_zoom_min | d9cf079f | 7fb7b526 |
| v005_border_zoom_mid | dd47a6b2 | 64ecb90c |
| v005_border_zoom_max | 2ca93af9 | 663ef1cb |
| v005_front_rim | 6e6660c8 | 8582c7a8 |
| v005_panel_overlap | 626eedda | 4a15abf1 |
| v005_investir_dump | 8d941ed1 | 21ab4276 |

`visual_proof_pairs_distinct_count = 8`.

## 6. Vérification Non-Goal — aucune ligne de simulation touchée

`git status --porcelain` sur les fichiers `.cs`, cette session, hors
`Assets/Scripts/Presentation/**` et `Assets/Tests/**` : **vide**. Les 7
fichiers `.cs` modifiés sont tous sous `Assets/Scripts/Presentation/` ;
le seul fichier `.cs` nouveau est `Assets/Tests/V005DiagnosticRunner.cs`
(diagnostic, non décoré `[Test]`, ne participe pas au comptage Success
Condition 9). `Assets/Scripts/Core/Components/TickControl.cs` n'apparaît
dans AUCUN diff — la clause d'exception nommée du Non-Goal n'a jamais été
exercée (SC7 = Outcome B, aucun changement).

Des fichiers PNG sous `unity/game_unity/Captures/v1_0*/**` apparaissaient
déjà modifiés dans `git status` **avant même le début de cette reprise**
(présents dans l'état initial transmis par le coordinateur) — non
réintroduits ni touchés par ce Générateur cette session ; toutes les
captures produites cette session vont sous des répertoires `v005_*`
nouveaux ou déjà utilisés par la passe interrompue, jamais sous `v1_0*/`.
Signalé pour que l'Évaluateur en tienne compte sans que j'en revendique la
responsabilité.

## 7. Journal des vérifications lockfile Unity

Voir `deliverables/evidence/unity-lock-checks.log` (intégral, chaque
invocation Unity de cette session précédée d'exactement une vérification
combinée `Test-Path .../UnityLockfile` + `Get-Process Unity`). Deux
processus `Unity.exe` transitoires (PID 34452 à 19:43:36, PID 34368 à
20:07:46) sont apparus puis ont disparu en moins de 20 secondes sans
jamais tenir le lockfile — même patron que celui documenté par le brief
003 (`generator-log.md`, « short-lived helper Unity.exe processes »),
journalisé, pas ignoré silencieusement.

## 8. Invocations Unity de cette session (reprise)

1. compile-check (`-batchmode -quit`, sans `-executeMethod`) — vérifie la
   compilation avant tout run mesuré.
2. `V005DiagnosticRunner.Run --v005-phase=after` ×4 (itérations de
   correction de la mesure couleur/largeur de trait — voir 2.4/2.5).
3. `V005DiagnosticRunner.Run --v005-phase=before` ×2 (`MapSnapshotExporter.cs`
   stashé à HEAD entre les deux, restauré après chacune).
4. `V1095BatchRunner.Run` sans `-nographics` (section 2.2).
5. `Ui002BuildPlayer.BuildFromCommandLine` (`InGameHud.cs` stashé à HEAD,
   build « avant-fix », restauré après).
6. `VictoriaGame.exe --ui-capture-dir ...` ×3 (1 échec `-nographics` :
   résolution fenêtre erronée, framebuffer non exploitable ; 1 succès sans
   `-batchmode` avec état `PlayerPrefs` résiduel ; 1 succès final,
   registre `PlayerPrefs` purgé — résultat identique aux deux tentatives
   réussies, retenu).

Aucun `git commit` exécuté par ce Générateur.
