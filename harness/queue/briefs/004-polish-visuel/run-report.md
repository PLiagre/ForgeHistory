# Run Report — 004-polish-visuel

**Backend**: claude
**Iterations**: 3 (Générateur) + 3 passes Évaluateur + 2 amendements Planificateur
**Score history**: [7, 9, 9, 9] (sur 9 checks mécaniques ; le premier point de
mesure est la passe de gate d'ouverture de session 2026-08-01, avant tout
travail de cette session)
**Outcome**: PASS

## Chronologie

Le brief 004 a été livré par son Générateur le 2026-07-31 mais jamais évalué
(arrêt de session à la demande du propriétaire). La session 2026-08-01 l'a
rouvert, débloqué, puis clos.

| Étape | Verdict gate | Score | Verdict Évaluateur | Notes |
|---|---|---|---|---|
| Ouverture 2026-08-01 | REJECT | 7/9 | — | `Authored:` futur-daté (Planificateur) + `verdict.md` absent |
| amendment-001 | REJECT | 7/9 | — | Planificateur corrige ses `Authored:` vers les mtimes réels ; les 2 checks de timestamp passent, restent les 2 dus à l'absence de verdict |
| Évaluateur passe 1 | ACCEPT | 9/9 | **FAIL** | 4 lignes de rubrique en échec ; `feedback-001.md` |
| amendment-002 | ACCEPT | 9/9 | — | Planificateur ouvre la voie « défaut absent » (Outcome B) pour SC1/SC3 : l'investigation honnête cesse d'être un échec de rubrique |
| Générateur itération 2 | ACCEPT | 9/9 | — | SC7 (`artistic_verdict` au manifest) + SC4/P1 (`LAWMOD`/`EFF` gatés, `STAB`/`LEG` en français, 2e point d'émission trouvé) |
| Évaluateur passe 2 | ACCEPT | 9/9 | **FAIL** | 1 seule ligne : SC3, séparateur décimal du bandeau joueur ; `feedback-002.md` |
| Générateur itération 3 | ACCEPT | 9/9 | — | Correctif au site d'appel (`FormatPanelLine` → `HudValueFormatter`), `WorldMetrics.Fmt1` intact |
| Évaluateur passe 3 | ACCEPT | 9/9 | **PASS** | Toutes Success Conditions fermées, aucun feedback |

## Ce que le brief a réellement produit

- **SC2** — fuite du token de debug `HOVER` fermée par une vraie porte
  (`InGameHud.ShowDebugIds`), atteignable dans les deux sens via `--debug-ids`.
- **SC3** — le bandeau joueur affichait `Trésor -269.8` (point anglais)
  pendant que les panneaux affichaient `4,6` (virgule). Corrigé au site
  d'appel uniquement : `WorldMetrics.Fmt1`/`Fmt0` restent en
  `InvariantCulture`, car 12+ fichiers de tests et les lignes de log de
  parité en dépendent.
- **SC4/P1** — trois occurrences du dump technique trouvées : `LAWMOD`/`EFF`
  (gatés), `STAB`/`LEG` (traduits « Stabilité »/« Légitimité »), `lawmod=`
  du panneau Lois (gaté). La quatrième (bloc `Investir`) délibérément non
  corrigée et transférée au brief 005.
- **SC1 et SC3-accents** — défauts nommés par le brief mais **inexistants**
  dans ce port (11/11 noms accentués correctement repliés). Aucun faux
  correctif appliqué ; c'est la rubrique qui a dû s'amender, pas la mesure.

## Ce que la boucle a appris au harnais

1. Un Planificateur qui future-date son propre brief bloque mécaniquement
   tout Générateur honnête (amendment-001).
2. Une rubrique qui exige `before_count > 0` sur un défaut absent force
   soit un mensonge, soit un échec — la voie « Outcome B » doit être écrite
   dès l'origine (amendment-002 ; le brief 005 l'intègre nativement).
3. Le gate mécanique a été vert aux trois itérations alors que l'Évaluateur
   a trouvé un vrai défaut à deux d'entre elles : les défauts visuels sont
   invisibles au gate. Seul l'œil les voit.

## Verdict artistique

**A_REVOIR_HUMAINEMENT**, jamais auto-`ADOPTÉ`. Le propriétaire a rendu son
jugement humain le 2026-08-01 (`owner-verdict-2026-08-01.md`) : **non
adopté**. Ses 8 griefs sont l'intrant du brief 005.

## Artefacts finaux

- verdict.md: `harness/queue/briefs/004-polish-visuel/verdict.md` (chronologie des 3 passes)
- feedbacks: `harness/queue/briefs/004-polish-visuel/feedback/feedback-001.md`, `feedback-002.md`
- amendements: `amendment-001-authored-correction.md`, `amendment-002-absent-defect-waiver.md`
- verdict propriétaire: `owner-verdict-2026-08-01.md`
- galeries: `unity/game_unity/Captures/v004_after3_default/`, `v004_after3_debug/`
