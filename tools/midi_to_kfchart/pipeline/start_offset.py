"""Shift notes earlier by offset_ms; drop notes that would land at t < 0."""


def shift(raws: list[dict], offset_ms: int) -> list[dict]:
    if offset_ms <= 0:
        return raws
    out: list[dict] = []
    for n in raws:
        new_t = n["t_ms"] - offset_ms
        if new_t < 0:
            continue
        out.append({**n, "t_ms": new_t})
    return out
