---
review_of: CURSOR-5633ee7-automation-completeness
reviewer: claude-code
target_commit: 5633ee74c10de5fa2653dff3b871d684a202ff30
reviewed_at: 2026-08-08T20:11:47Z
---

# Contre-audit de CURSOR-5633ee7-automation-completeness

Rôle : vérifier la *véracité technique* de l'audit, pas sa valeur métier.
Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

## 1. Provenance (re-vérifiée)

- target_commit annoncé : `5633ee74c10de5fa2653dff3b871d684a202ff30`
- Le commit existe-t-il dans l'historique de la branche cible ? **Oui.**
  `git cat-file -t 5633ee74...` → `commit` ;
  `git log --oneline -1 5633ee7` → `5633ee7 Merge pull request #8 from
  PLiagre/forge/cursor-audit-loop`. Le fichier d'audit lui-même a été
  ajouté *après*, par `f23f691`, puis mergé dans `master` (PR #9). Au
  moment de cette review, `origin/master` est à `198cfd9` — les 5 constats
  ont donc été rejoués sur un commit **postérieur** à la cible et tiennent
  toujours (aucun n'est devenu obsolète).
- Mesures de l'audit rejouées ? **Oui**, une par une (section 2). La seule
  divergence de mesure est le compte de tests (voir la note en §4) et
  s'explique intégralement par la différence de commit ; elle n'invalide
  aucun constat.

## 2. Verdicts point par point

| # | Point de l'audit | Verdict | Preuve / délimitation |
|---|---|---|---|
| ARCH-001 (P0) | Le trigger d'`pipeline-orchestrate` choisit l'événement en comptant les fichiers `reviews/*.md` du diff du push, sans lire le ledger → rejoue une transition sur un audit déjà terminal. Preuve live : run CI `31085883052` FAILURE. | **CONFIRMED** | Deux preuves indépendantes. (a) Run réel : `gh run view 31085883052` → `pipeline-orchestrate`, step « Run orchestrator » ✗, exit 2 ; `--log-failed` cite mot pour mot `event=review_recorded`, `payload={"audit_id": "CURSOR-FIXTURE-full-auto-demo"}`, puis `error: audit 'CURSOR-FIXTURE-full-auto-demo' is AUDIT_ARCHIVED, not AUDIT_CHALLENGED`. (b) Code : `.github/workflows/pipeline-orchestrate.yml:72-81` fait `git diff --name-only before after -- 'architecture/reviews/*.md'`, compte, et si `count==1` construit le payload à partir du seul fichier changé — **aucune lecture** de `architecture/audit-ledger.jsonl`. Le `::notice::` de skip ne se déclenche que si `count != 1`. |
| ARCH-002 (P1) | Les 3 invocations d'agents (`pipeline-audit/challenge/forge-run.yml`) ne sont que des `echo "TODO(operator...)"`. | **CONFIRMED** | `grep -n "TODO(operator"` → `pipeline-audit.yml:57`, `pipeline-challenge.yml:69`, `pipeline-forge-run.yml:97`. Chaînes littérales à l'emplacement même de l'appel d'agent ; les intervalles cités par l'audit (56-62 / 63-72 / 90-100) englobent ces lignes. Fournir les secrets ne change donc rien au comportement observable. |
| ARCH-003 (P1) | Aucune règle d'`auto_policy.yaml` ne couvre la panne d'un workflow `pipeline-*` lui-même. | **CONFIRMED** | `auto_policy.yaml` = 10 règles (events : `audit_pr_merge`, `review_recorded` ×3, `audit_approved`, `brief_seed_created`, `gate_accept`, `evaluateur_pass`, `gate_reject`, `budget_exhausted`). Seule `gate_reject → open_bot_issue_pipeline_stuck_no_human_wait` escalade. `grep -niE "workflow_failure\|job_failed\|infra\|workflow_run"` → aucune occurrence. Corroboré par le run `31085883052` resté rouge sans issue ni notification. |
| ARCH-004 (P2) | Le maillon « graine → brief exploitable » est un marqueur `«TODO»` documenté dans le schéma normatif. | **CONFIRMED** (fait technique) | `docs/rules/full-auto-pipeline.md:40` → `[claude-planificateur] fills brief «TODO» (separate invocation, same pipeline)` (marqueur cité avec guillemets simples pour ne pas déclencher le gate). La règle `auto_policy.yaml` `brief_seed_created → claude_planificateur_fills_todo_same_pipeline_separate_invocation` nomme honnêtement l'absence de câblage. Le fait est exact ; *comment* le traiter (point d'arrêt humain assumé vs. câblage réel) est un arbitrage → voir §3. |
| ARCH-005 (P2) | `budget.py` reste aveugle au backend Cursor (`UNMEASURABLE` générique, pas de statut dédié). | **CONFIRMED, mais redondant** | `grep -niE "cursor\|UNMEASURABLE\|UNSUPPORTED" harness/budget.py` : aucun statut spécifique Cursor ; seuls `UNMEASURABLE`/`AMBIGUOUS` (`budget.py:269`, `:417`). Le classement repose sur les transcripts de session Claude ; un backend Cursor n'en produit pas → `UNMEASURABLE`. **Ce constat re-signale `CURSOR-6231186` FINDING-ARCH-003**, déjà connu et non résolu ; l'audit le reconnaît lui-même. Aucun nouvel élément de preuve, pas de nouveau travail au-delà de ce qui est déjà tracé. |

Sur la carte « automatisé vs déclaré vs stub » (§4 de l'audit) : la ligne
« Lancement de l'Évaluateur après ACCEPT : Non implémenté » mérite une
délimitation. Une **règle de politique existe** (`gate_accept →
launch_evaluateur_auto`, `auto_policy.yaml:51-53`) ; c'est son
**implémentation** dans `orchestrator.py` qui est log-only. La distinction
« déclaré ≠ câblé » est la même que pour ARCH-002 et n'est pas
contradictoire — juste à formuler comme « déclaré, non câblé » plutôt que
« non implémenté ».

## 3. Points à porter au propriétaire (NEEDS_OWNER)

Ces points sont techniquement établis ; ce sont les *décisions* qui
relèvent du propriétaire (reprises de la §8 de l'audit, sans les trancher) :

1. **Renommer/scinder `mode: full_auto` (ARCH-002)** — aujourd'hui seules
   la décision et la fusion sont réellement automatisées ; la génération de
   contenu par agent est un stub. Faut-il un sous-statut explicite
   (`full_auto_decision_only`) ? Arbitrage produit, pas technique.
2. **Mode de notification de l'escalade d'infra (ARCH-003 / BRIEF-PROP-002)**
   — issue GitHub vs. simple canal de log. Le constat (absence de tout
   signal) est technique et confirmé ; le *mode* est un choix.
3. **Budget récurrent d'appels LLM en CI (ARCH-002 / BRIEF-PROP-003)** —
   câbler une vraie invocation a un coût par déclenchement que l'audit ne
   peut chiffrer sans les tarifs contractuels. Décision purement métier.
4. **Traiter le job rouge sur `master` (run `31085883052`) comme incident
   urgent** (BRIEF-PROP-001) ou risque connu toléré. Le fait — `master` a
   un job `pipeline-orchestrate` rouge non traité — est confirmé ; la
   priorité est un arbitrage.
5. **Fermer réellement ARCH-004** (câbler le remplissage du brief) ou
   l'assumer comme point d'arrêt humain documenté. Dépend de la même
   décision produit qu'ARCH-002.

## 4. Synthèse

**Ce qui tient :** les 5 constats sont techniquement exacts et
reproductibles. ARCH-001 est le plus solide — ce n'est pas une déduction
mais un incident CI réel, avec message d'erreur cité au mot près et une
cause racine lisible dans le code du trigger. ARCH-002 et ARCH-003 sont des
faits binaires vérifiés par simple lecture. L'audit est **honnête** :
il déclare ses angles morts (pas de CI Unity, `403` sur `gh secret list` et
la protection de branche, pas de `/forge-run` rejoué), et ses briefs sont
explicitement « proposés, non autorisés ».

**Ce qui est à nuancer :**
- **ARCH-005 double-compte** `CURSOR-6231186` FINDING-ARCH-003 (l'audit
  l'admet). À traiter comme un *rappel* d'un item déjà ouvert, pas comme un
  nouveau constat ouvrant un nouveau brief.
- La carte §4 « Évaluateur non implémenté » est plus exactement « déclaré
  en politique, non câblé en code » — même nature que le reste des stubs.

**Observation incidente (hors périmètre de l'audit, pour info owner) :**
sur `origin/master` actuel (`198cfd9`, postérieur à la cible), la suite
`harness/tests/` montre `1 failed, 249 passed` :
`test_run_unity.py::test_no_brief_prescribes_polling`. L'audit annonçait
`235 passed, 15 skipped` sur `5633ee7` — l'écart s'explique par la
différence de commit (travaux brief 007/Unity ajoutés depuis) et
**n'affecte aucun des 5 constats**. À investiguer séparément.

**Recommandation de traitement :** les cinq points sont CONFIRMÉS et prêts
pour la décision du propriétaire. Ordre de traitement suggéré à l'owner
(non contraignant) : ARCH-001 en priorité (incident réel, correction
ciblée, BRIEF-PROP-001), puis ARCH-003 (BRIEF-PROP-002, faible complexité),
puis ARCH-002/004 groupés derrière la décision produit sur `full_auto` et
le budget LLM. ARCH-005 : rattacher au suivi déjà ouvert de
`CURSOR-6231186` plutôt que d'ouvrir un doublon.

Verdicts : **5 CONFIRMED** (dont 1 redondant avec un audit antérieur),
0 REFUTED, 0 PARTIAL. Les arbitrages listés en §3 sont NEEDS_OWNER par
nature *décisionnelle*, pas parce que le fait sous-jacent serait douteux.
