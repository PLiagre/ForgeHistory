# unity/

The render client. Zero simulation/business logic — rendering, input,
animation, and UI only (see `VISION.md`: "Unity n'est jamais responsable de
la logique métier... Le backend fonctionne sans Unity"). Empty stub; not
started in F0. When populated, it must only ever READ simulation state
exposed by `sim/`, never re-derive or re-filter it (see
`docs/rules/simulation-principles.md` failure mode #4 — presentation
re-implementing the simulation was one of VictoriaProject's costliest
defects).
