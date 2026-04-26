"""Drop notes past duration_ms and clamp overhanging hold sustains."""


def truncate(raws: list[dict], duration_ms: int) -> list[dict]:
    out: list[dict] = []
    for n in raws:
        if n["t_ms"] >= duration_ms:
            continue
        end = n["t_ms"] + n["dur_ms"]
        if end > duration_ms:
            out.append({**n, "dur_ms": duration_ms - n["t_ms"]})
        else:
            out.append(n)
    return out
