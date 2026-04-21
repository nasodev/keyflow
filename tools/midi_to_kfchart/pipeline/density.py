"""NPS-target density thinning. Deterministic, no random."""


def thin(notes: list[dict], target_nps: float, duration_ms: int) -> list[dict]:
    if duration_ms <= 0:
        return []
    if not notes:
        return []
    duration_sec = duration_ms / 1000.0
    current_nps = len(notes) / duration_sec
    allowed_nps = target_nps * 1.1
    if current_nps <= allowed_nps:
        return list(notes)
    keep_ratio = allowed_nps / current_nps  # in (0, 1)
    step = round(1 / (1 - keep_ratio))
    if step < 2:
        return []  # ratio so extreme that every note drops
    return [n for i, n in enumerate(notes) if (i + 1) % step != 0]
