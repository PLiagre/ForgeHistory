# Package QA du socle cartographique G1.
from qa.checks import (  # noqa: F401
    CheckResult,
    q1_polygon_validity,
    q10_determinism,
    q2_no_holes,
    q3_no_overlaps,
    q4_no_isolated,
    q5_cities_in_exactly_one_land_cell,
    q7_adjacency_contiguous,
    run_all_green,
)
