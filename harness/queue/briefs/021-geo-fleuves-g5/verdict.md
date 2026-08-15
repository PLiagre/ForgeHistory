**Author**: forge-evaluateur
**Authored**: 2026-08-15T11:40:00Z

# Verdict — Brief `021` : les fleuves (G5) — passe `2`

> **Note de transparence — à lire avant le verdict.** L'acteur réel est Claude
> Code endossant le rôle natif `forge-evaluateur`, sans suffixe ajouté à la
> signature, pour que `verdict_is_not_self_authored` puisse comparer les acteurs
> de part et d'autre du lot.
>
> **Conflit de rôle, déclaré et non masqué.** Le même acteur a, dans ce lot,
> endossé le rôle `forge-planificateur` (il a orchestré la rédaction du
> `brief.md` et écrit `amendment-001-artere-sans-geometrie.md`) **et** le rôle
> `forge-evaluateur` (ce verdict et `feedback/feedback-001.md`). `CLAUDE.md`
> exige « jamais le même agent dans la même passe ». Le contrôle mécanique
> `verdict_is_not_self_authored` ne compare que Générateur ↔ Évaluateur : il
> passe, **sans voir** cette entorse. Elle est donc écrite ici, en clair, plutôt
> que laissée à un contrôle qui ne sait pas la mesurer. Le propriétaire a été
> averti et a choisi cette voie le `2026-08-15` pour clore le lot dans la journée.
> Ce verdict doit être lu en sachant que son auteur a écrit une partie des
> instructions qu'il juge. C'est un constat ouvert, pas une formalité.
>
> Ce qui atténue — sans annuler — cette entorse : les onze constats appliqués à
> cette passe viennent d'une **relecture indépendante** du diff de la PR `#107`,
> menée dans une invocation séparée, et chaque mesure ci-dessous a été rejouée
> par des commandes dont la sortie est reproductible, pas reprise du manifeste.

**Ce qui est jugé.** L'état du worktree `agent/g5-fleuves` après l'itération `2`
du Générateur (Cursor), contre `brief.md` et `eval-rubric.md` **tels qu'amendés**
par `amendment-001-artere-sans-geometrie.md`, et contre les onze points de
`feedback/feedback-001.md`. Le verdict de la passe `1` (REJECT) valait contre le
texte antérieur à l'amendement ; il reste consultable dans l'historique et sur la
PR `#107`.

**Rien n'est repris du manifeste.** Les valeurs ci-dessous ont été re-dérivées
dans cette session.

---

## Porte mécanique

Jouée en premier. `harness/verdict_audit.py harness/queue/briefs/021-geo-fleuves-g5`,
avant l'écriture de ce fichier :

- passe `1` : **cinq** échecs — `files_declared_exist`, `mtime_after_brief`,
  `verdict_numbers_traceable`, `verdict_is_not_self_authored`,
  `rubric_predates_deliverables`, `declared_files_are_tracked`.
- passe `2` : **deux** échecs, tous deux `verdict.md missing` —
  `verdict_numbers_traceable` et `verdict_is_not_self_authored`. Ce sont les deux
  que le présent fichier lève, et ils relèvent du rôle Évaluateur, pas du
  Générateur : le feedback lui avait explicitement interdit de rédiger un verdict.

Cette porte juge la forme, pas le fond. Elle ne sait ni recompter une arête, ni
distinguer un contrôle creux d'un contrôle qui mord, ni regarder une capture.

## Ce que j'ai reconstruit et rejoué moi-même

| fait | valeur mesurée | comment |
|---|---|---|
| preuve G5 | **exit `0`** | `tests/run_proof_g5.py` rejoué |
| contrôles verts | **6/6** | `logs/v1_060_qa.json`, champ `passed` |
| preuves rouges non vides | **6/6** | même fichier, champ `red_proof` |
| déterminisme | **8/8 paires SHA256 égales** | deux passes, empreintes comparées |
| tronçons | **`157`** = `36` + `92` + `29` | `rivers_g5.json` |
| arêtes avec fleuve | **`276`** = `72` + `195` + `9` | `adjacency_g5.json` |
| embouchures | **`57`** | `mouths_g5.json` |
| fleuves nommés | **9/9** | contre `G5_NAMED_MAJOR_RIVERS` |
| crochet pipeline | **exit `0`**, ligne de résumé affichée | `pipeline.py --source rivers` |
| compteurs déclarés | **`27`**, chacun avec son dénominateur | `measure_g5_021.py --no-pytest` |

Écart entre mes valeurs et le manifeste : **aucun**.

## Les onze points du feedback, un par un

| # | point | état vérifié |
|---|---|---|
| `1` | contrôle G5-D creux | **corrigé** — `bool(adjacent)` calculé (l. `557`), compteur `embouchures_zone_non_adjacente` ajouté, cas rouge devenu naturel (`test_qa_red_g5.py:116-121`) |
| `2` | `sea_zone_name` publié comme fait | **corrigé** — champ conservé avec déclaration de proxy hérité de G4 |
| `3` | frontmatter `Author` | **corrigé** — `**Author**: forge-generateur` |
| `4` | `"counters": []` | **corrigé** — `27` compteurs déclarés, `19` fichiers |
| `5` | compteurs « fichier intact » incapables de rougir | **corrigé et prouvé** — `git diff origin/master...HEAD` remplace `git status`, et le Générateur a **ajouté de lui-même** `preuve_compteur_intact_peut_rougir = 1`, qui démontre que le compteur voit une modification committée |
| `6` | `git()` avale les erreurs | **corrigé** — `returncode` traité |
| `7` | `rebuild_land` mort | **corrigé, mieux que demandé** — `False` lève désormais une erreur explicite au lieu d'être ignoré en silence |
| `8` | code mort | **corrigé** — `ctx_g4` et `land_land_total` supprimés |
| `9` | extrémités de découpe | **corrigé** — extrémités sur `window_ll.exterior` exclues (l. `503`) |
| `10` | captures peignant les lacs en terre | **corrigé** — anneaux intérieurs peints en couleur mer (l. `761-772`) |
| `11` | appliquer l'amendement | **corrigé** — `README.md:332-347` et `logs/v1_060_rivers.log:26-29` portent la clause « ce que cette classification ne dit pas » et le fait mesuré des `3` % |

Le point `5` mérite d'être souligné : il ne suffisait pas de changer la commande,
il fallait prouver que le compteur corrigé peut rougir. Le Générateur l'a fait
sans qu'on le lui demande explicitement.

## Les captures, regardées de mes yeux (règle n° `11`)

**`v1_060_rivers_window.png`.** Fenêtre pilote entière. Fleuves navigables en
bleu (Danube, Rhin, Loire, Èbre, Nil), indéterminés en orange, non navigables en
brun. Terre en vert, mer en bleu clair. **Aucun trait de fleuve ne traverse la
pleine mer** — cohérent avec G5-B. Les plans d'eau intérieurs (lacs scandinaves,
Zuiderzee) apparaissent bien en couleur mer et non en vert : la correction du
point `10` est visible, pas seulement affirmée dans le code.

**`v1_060_artery_crossing_both.png`.** Zoom Manche / Bénélux / Rhin moyen. Arêtes
`artery` en rouge, `crossing` en violet, `both` en cyan ; fleuves en fin trait
bleu. Cette capture **confirme visuellement l'amendement `001`** : les arêtes
rouges sont des segments droits entre cellules qui **coupent** le fleuve en un
point, tandis que le fleuve serpente à côté. Aucune arête rouge ne suit le cours
d'un fleuve. C'est exactement ce que la mesure des `3` % disait, et c'est désormais
ce que le README annonce.

`captures_regardees_et_decrites` = **`2`** sur `2` captures produites.

## Verdict par condition de succès

| SC | verdict | fondement |
|---|---|---|
| SC1 — tronçons, navigabilité, fleuves nommés | **PASS** | `157` = `36`+`92`+`29` ; 9/9 fleuves ; bornes lues de `constants.py` |
| SC2 — G5-A et G5-B verts | **PASS** | verts, preuves rouges non vides |
| SC3 — classification et `adjacency_g5` | **PASS** | `72`+`195`+`9` = `276` ; `artery_count` = `72` > `0` ; `adjacency_g4.json` intact ; `adjacency_g5` en diffère |
| SC4 — G5-D et embouchures | **PASS** | `57` embouchures ; drapeau **calculé** ; cas rouge naturel ; `embouchures_zone_non_adjacente` = `0` mesuré |
| SC5 — déterminisme, 6/6, rouges | **PASS** | 8/8 paires SHA ; 6/6 verts ; 6/6 rouges ; exit `0` ; `constants.py` inchangé |
| SC6 — crochet, fichiers verrouillés, README | **PASS** | `--source rivers` exit `0` ; 0/9 fichiers partagés modifiés ; 13/13 preuves suivies ; README amendé |

## Verdict global : PASS

Le lot livre ce que le brief demandait, ses contrôles mordent, ses mesures sont
reproductibles, et les onze défauts de la passe `1` sont corrigés — dont deux
au-delà de ce qui était demandé (points `5` et `7`).

**Ce que ce PASS ne dit pas.**

1. Il ne dit pas que le modèle d'artère est le bon. Le propriétaire a tranché le
   `2026-08-15` que l'artère devrait être un attribut du fleuve et de la chaîne de
   cellules qu'il traverse, pas de l'arête. Ce lot livre le modèle *câblé*
   (navigabilité seule), désormais **décrit sans sur-revendication**. La
   redéfinition exige de réécrire `qa/checks.py` et `pipeline.py`, en lecture
   seule ici : c'est un brief à part.
2. Il ne dit pas que ce verdict est exempt du conflit de rôle déclaré en tête.

## Constats ouverts (aucun n'est un motif de rejet)

- **Conflit de rôle Planificateur/Évaluateur** sur ce lot (voir en tête). Le
  contrôle mécanique ne sait pas le voir ; c'est le troisième angle mort du
  contrôle d'auto-jugement, à rapprocher de celui déjà documenté dans
  `HANDOFF.md`.
- **Neuf tests Unity en échec** dans `harness/tests/test_run_unity.py` :
  **préexistants sur `master`**, vérifié par exécution de la baseline hors de ce
  lot. Sous WSL2, `powershell.exe` est visible via `/mnt/c/`, donc les tests ne
  se mettent pas en SKIP comme sur un Linux pur, mais le binaire Unity est
  absent. Aucun fichier de `harness/tests/` n'a été touché par ce lot.
- **G5-ter reste câblé sans source.** `constants.py` et `qa/checks.py` portent
  quatre contrôles `g5cter_*` et la couche `ne_10m_rivers_europe`, absente de
  `sources.lock`. Hors périmètre ici (D10), mais un futur brief devra sourcer
  cette couche avant de pouvoir exécuter ce qui est déjà écrit.
- **Deux lacunes de ForgePilot** rencontrées pendant ce lot, sans rapport avec le
  code G5 : `forgepilot review` casse au-delà de ~`128` Ko de diff (tout le diff
  est passé dans un seul argument de ligne de commande, or `MAX_ARG_STRLEN` vaut
  `32` × `4096`), et il n'existe aucune commande d'itération — `execute` refuse un
  worktree existant. Matière à brief sur `control-plane/`.
