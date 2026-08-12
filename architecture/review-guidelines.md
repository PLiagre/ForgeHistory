# Guide de critique des PR — bonnes pratiques d'ingénierie IA, sourcées

Référentiel utilisé par le rôle `cursor-auditor`
(`architecture/agents/cursor-auditor.md`) quand il relit une pull request
(ADR-0010 : Cursor est le maillon **critique** de la chaîne à quatre
acteurs). Ce fichier dit **comment juger** ; il ne dit jamais quoi
implémenter (ça, c'est le brief — `CLAUDE.md` › Single Source of
Instruction).

Chaque pratique ci-dessous est adossée à une source externe datée (§ Sources
en bas). À re-sourcer chaque trimestre : une bonne pratique de 2026 peut
être périmée en 2027.

## Les six lentilles d'une critique

1. **Intention avant diff.** Commencer par la spec (le brief, la rubrique,
   la description de PR) et vérifier que le diff résout **le bon problème
   avec les bonnes contraintes** — pas seulement que le code « a l'air
   correct ». Une PR sans intention lisible est critiquable pour cela même.
   [S1, S2]
2. **Preuve d'exécution, pas d'affirmation.** Toute affirmation « ça marche »
   doit être adossée à une preuve rejouable : sortie de test, commande +
   retour, capture. La forme la plus forte : un test qui échoue sur le
   comportement d'avant et passe après. Un correctif sans ce test est une
   **prétention**, pas une démonstration. C'est déjà la discipline du gate
   (`harness/verdict_audit.py`) — la critique vérifie qu'elle est tenue.
   [S3, S2]
3. **Portes mécaniques d'abord.** Ne pas dépenser du jugement humain (ou
   agent) sur ce qu'un linter, un typage, un test ou un scan couvre déjà.
   La critique vérifie que les portes mécaniques ont tourné et sont vertes,
   puis concentre le jugement sur l'architecture, l'intention et les limites
   que les machines ne voient pas. La revue humaine s'effondre au-delà
   d'environ 400 lignes ; le mécanique n'a pas cette limite. [S2, S1]
4. **Cadrage adverse.** Formuler la relecture comme « trouve où cette
   affirmation est fausse », jamais « relis ce code ». L'acteur qui critique
   doit être distinct de l'acteur qui a produit (même règle que le harnais :
   celui qui produit ne prononce pas la recevabilité). [S3, S4]
5. **Taille et découpage.** Un diff qui dépasse ~5 fichiers ou quelques
   centaines de lignes dépasse ce qu'une relecture honnête peut connecter à
   l'intention. Le signaler et recommander le découpage en lots — c'est la
   discipline `NEEDS_SPLIT` que le harnais applique déjà côté briefs. [S1, S2]
6. **Pièges spécifiques au code généré par IA.** Chercher en priorité :
   la correction hallucinée (succès affirmé non mesuré), la sur-ingénierie
   (« production-ready » confondu avec « complexe »), la structure de
   données naïve (liste où il faut un dictionnaire, boucle où il faut du
   bulk), les portes de test affaiblies pour faire passer, et la
   dépendance inventée. [S5, S3, S4]

## Forme imposée des constats

- Chaque constat porte une **sévérité** : `P0` (bloque la fusion), `P1`
  (à corriger avant fusion sauf dérogation), `P2` (à planifier), `P3`
  (information).
- Chaque constat **cite sa preuve** : fichier + lignes, sortie de commande,
  ou source externe. Un constat sans preuve citable ne doit pas être émis —
  et un lecteur est en droit de l'ignorer. [S4]
- Pas de rubber-stamping inverse : répéter un motif déjà écarté par une
  décision enregistrée (ADR, décision propriétaire) sans élément nouveau
  est du bruit, pas de la critique. [S4]
- La critique **n'instruit rien** : elle propose, la décision reste à la
  boucle (`architecture/README.md`, ADR-0005/0006).

## Sources

| # | source | consulté le |
|---|---|---|
| S1 | The New Stack — *Move code review before the code* — <https://thenewstack.io/move-code-review-upstream/> | 2026-08-12 |
| S2 | Augment Code — *Reviewing AI-Generated Code: A Verification Discipline for the Loop* — <https://www.augmentcode.com/guides/reviewing-ai-generated-code> | 2026-08-12 |
| S3 | aiarch.dev — *Reviewing AI-Written Code: A Diff Discipline Workflow* — <https://aiarch.dev/workflows/ai-assisted-review> | 2026-08-12 |
| S4 | AnAr Solutions — *The Five Lenses of AI Code Review* — <https://anarsolutions.com/ai-code-review-framework/> | 2026-08-12 |
| S5 | danicat.dev — *How to Do Code Reviews in the Agentic Era* (2026-03-03) — <https://danicat.dev/posts/20260303-code-reviews-in-2026/> | 2026-08-12 |
