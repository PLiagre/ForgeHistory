import re, sys, pathlib

def parse(path):
    text = pathlib.Path(path).read_text(encoding="utf-8")
    blocks = re.split(r"^=== (.+?) ===$", text, flags=re.MULTILINE)[1:]
    out = {}
    for i in range(0, len(blocks), 2):
        name = blocks[i].strip()
        body = blocks[i+1]
        m = re.search(r"\[(PASS|FAIL|N/A)\] verdict_is_not_self_authored", body)
        out[name] = m.group(1) if m else None
    return out

before = parse(sys.argv[1])
after = parse(sys.argv[2])
assert set(before) == set(after), (set(before) - set(after), set(after) - set(before))
flips = [n for n in before if before[n] == "PASS" and after[n] == "FAIL"]
print(f"briefs compared: {len(before)}")
print(f"PASS->FAIL regressions: {len(flips)} {flips}")
for n in sorted(before):
    print(f"  {n}: {before[n]} -> {after[n]}")
