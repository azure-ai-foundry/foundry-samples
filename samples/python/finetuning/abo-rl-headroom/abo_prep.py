# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.

"""Amazon Berkeley Objects (ABO) -> editorial-JSON training data adapter.

Why this file exists
--------------------
This adapter targets a permissively licensed public dataset (Amazon Berkeley
Objects, CC BY 4.0) so the SFT->GRPO recipe and findings can be reproduced and
shared publicly. It emits rows in the 8-field editorial-JSON schema the training
and scoring pipeline expects::

    {"product_id": str, "student_prompt": str, "completion": <JSON string>}

where ``completion`` is the 8-field editorial JSON that the grader scores:

    image_descriptions.trend_signals   (list) -> Semantic-IoU   <- style/pattern/fabric + keywords
    image_descriptions.overall_vibe    (str)  -> Semantic-IoU   <- "<style> <product_type> in <color>"
    image_descriptions.quick_summary   (str)  -> cosine         <- lead bullet / item_name sentence
    semantic_descriptions.trend        (str)  -> cosine         <- "<style> <product_type>"
    semantic_descriptions.vibe         (list) -> Semantic-IoU   <- color/material/style/pattern aesthetics
    semantic_descriptions.rich_semantic(str)  -> cosine         <- ~25-word blend of bullet points
    semantic_descriptions.quick_semantic(list)-> Semantic-IoU   <- product_type/color/material/pattern
    semantic_descriptions.summary      (str)  -> cosine         <- one-sentence product summary

The 4 SIoU fields and 4 cosine text fields map 1:1 onto the grader's
weighted field sets, so every reward-shaping knob (threshold annealing, per-field
weights) transfers unchanged.

Distillation gap
----------------
The gold trend/vibe tags are derived from the richer catalog attributes
(``style``, ``pattern``, ``fabric_type``, ``item_keywords``), while the student
prompt shows the plainer catalog fields (name, bullets, product_type, color,
material). The model must still compose those into the correct editorial *style
and length* -- a genuine structured-generation RL task, with the sparse SIoU tag
fields as the bottleneck.

Optionally, pass ``--teacher`` (not required) to enrich the free-text cosine
fields with an LLM teacher model; the default path is fully deterministic and
needs no network, so the baseline is trivially reproducible.

Usage
-----
    # Download ABO listings metadata (CC BY 4.0), ~listings/metadata/*.json.gz:
    #   https://amazon-berkeley-objects.s3.amazonaws.com/index.html
    python abo_prep.py \
        --abo-glob "abo-listings/listings/metadata/listings_*.json.gz" \
        --out-dir data/processed \
        --category fashion --difficulty both --max 6000 --seed 42

    # This writes aligned easy/ and hard/ train/val/test splits. Point the
    # SFT/GRPO pipeline at one arm at a time.

    # No ABO download handy? Verify the mapping on built-in synthetic records:
    python abo_prep.py --selftest
"""

from __future__ import annotations

import argparse
import glob
import gzip
import json
import os
import random
import re
from typing import Any, Iterable, Optional

# The 8 graded field paths -- kept in sync with the grader's ALL_FIELDS so
# --selftest can validate schema completeness without importing the RL deps.
SIOU_FIELDS = [
    "image_descriptions.trend_signals",
    "image_descriptions.overall_vibe",
    "semantic_descriptions.vibe",
    "semantic_descriptions.quick_semantic",
]
COSINE_FIELDS = [
    "image_descriptions.quick_summary",
    "semantic_descriptions.rich_semantic",
    "semantic_descriptions.summary",
    "semantic_descriptions.trend",
]
ALL_FIELDS = SIOU_FIELDS + COSINE_FIELDS

# ABO product_type controlled-vocabulary values that are wearable fashion items.
# ABO has essentially no garments (no SHIRT/DRESS/PANTS); its fashion-relevant
# stock is footwear, bags/luggage, jewelry and hats. We match the RAW uppercase
# product_type value EXACTLY (not a substring of the free-text name) so we don't
# leak phone cases ("top" in "laptop") or furniture into the "fashion" subset.
_FASHION_PRODUCT_TYPES = {
    # footwear
    "SHOES",
    "BOOT",
    "SANDAL",
    # bags & luggage
    "HANDBAG",
    "BACKPACK",
    "SUITCASE",
    "LUGGAGE",
    # jewelry
    "FINERING",
    "RING",
    "FINEEARRING",
    "EARRING",
    "NECKLACE",
    "BRACELET",
    "FINENECKLACEBRACELETANKLET",
    "FASHIONNECKLACEBRACELETANKLET",
    # headwear & misc accessories
    "HAT",
    "ACCESSORY",
    "NECKTIE",
    "SALWAR_SUIT_SET",
    "TRACK_SUIT",
}

# Non-informative ABO attribute values to drop from tag/text fields.
_STOP_VALUES = {"others", "other", "n/a", "na", "none", "unknown", "misc", ""}


_EN_TAGS = ("en_us", "en_gb", "en_ca", "en_au", "en_in", "en")


# --------------------------------------------------------------------------- #
#  ABO field extraction (language-tagged arrays -> plain English values)       #
# --------------------------------------------------------------------------- #


def _lang_values(entries: Any, prefer_en: bool = True) -> list[str]:
    """ABO stores most fields as ``[{language_tag, value}, ...]``. Return the
    English (or, if none, all) ``value`` strings, de-duplicated in order."""
    out: list[str] = []
    if not isinstance(entries, list):
        if isinstance(entries, str):
            return [entries]
        return out
    en, other = [], []
    for e in entries:
        if isinstance(e, dict):
            val = e.get("value")
            tag = str(e.get("language_tag", "")).lower()
            if not val:
                continue
            (en if any(tag.startswith(t) for t in _EN_TAGS) else other).append(str(val))
        elif isinstance(e, str):
            en.append(e)
    chosen = (en or other) if prefer_en else (en + other)
    seen: set[str] = set()
    for v in chosen:
        k = v.strip()
        if k and k.lower() not in seen:
            seen.add(k.lower())
            out.append(k)
    return out


def _first(entries: Any) -> str:
    vals = _lang_values(entries)
    return vals[0] if vals else ""


def _norm_tag(text: str) -> str:
    """Lowercase, strip, collapse whitespace -- for tidy list-field tags."""
    return re.sub(r"\s+", " ", str(text).strip().lower())


def _dedup(tags: Iterable[str], limit: Optional[int] = None) -> list[str]:
    out: list[str] = []
    seen: set[str] = set()
    for t in tags:
        t = _norm_tag(t)
        if t and t not in seen and t not in _STOP_VALUES:
            seen.add(t)
            out.append(t)
            if limit and len(out) >= limit:
                break
    return out


def _clip_words(text: str, lo: int, hi: int) -> str:
    words = re.sub(r"\s+", " ", str(text).strip()).split()
    # Drop consecutive duplicate words ("shoes shoes" -> "shoes").
    deduped: list[str] = []
    for w in words:
        if not deduped or deduped[-1].lower() != w.lower():
            deduped.append(w)
    return " ".join(deduped[:hi]) if deduped else ""


def _en_only(entries: Any) -> list[str]:
    """English-tagged values only (no non-English fallback) -- for clean tags."""
    out: list[str] = []
    if not isinstance(entries, list):
        return out
    seen: set[str] = set()
    for e in entries:
        if isinstance(e, dict):
            val, tag = e.get("value"), str(e.get("language_tag", "")).lower()
            if val and any(tag.startswith(t) for t in _EN_TAGS):
                k = str(val).strip()
                if k and k.lower() not in seen:
                    seen.add(k.lower())
                    out.append(k)
    return out


# --------------------------------------------------------------------------- #
#  ABO record -> 8-field editorial gold                                        #
# --------------------------------------------------------------------------- #


def _product_type_raw(rec: dict) -> str:
    """The RAW controlled-vocab product_type value (uppercase), e.g. 'SHOES'."""
    pt = rec.get("product_type")
    if isinstance(pt, list) and pt:
        v = pt[0].get("value") if isinstance(pt[0], dict) else pt[0]
        if v:
            return str(v).strip().upper()
    return ""


def _product_type(rec: dict) -> str:
    pt = _product_type_raw(rec)
    return pt.replace("_", " ").strip().lower()


def build_gold(rec: dict, difficulty: str = "easy") -> Optional[dict]:
    """Map one ABO listing to the 8-field editorial JSON (gold ``completion``).

    ``difficulty="hard"`` derives the cosine text fields from the *structured*
    attributes (color/material/style/pattern) rather than copying the bullet
    text verbatim. Combined with the hard student prompt (which hides those
    attributes), this turns the task into genuine inference-from-prose instead
    of near-copying -- restoring the SFT->RL headroom that a clean copy task
    erases.

    Returns None if the record is too sparse to form a valid gold label."""
    brand = _first(rec.get("brand"))
    item_name = _first(rec.get("item_name"))
    product_type = _product_type(rec)
    colors = _dedup(rec.get("color") and _lang_values(rec.get("color")) or [])
    color = colors[0] if colors else ""
    materials = _dedup(
        _lang_values(rec.get("material")) + _lang_values(rec.get("fabric_type"))
    )
    material = materials[0] if materials else ""
    styles = _dedup(_en_only(rec.get("style")))
    style = styles[0] if styles else ""
    patterns = _dedup(_en_only(rec.get("pattern")))
    pattern = patterns[0] if patterns else ""
    bullets = _lang_values(rec.get("bullet_point"))
    # Keep multi-word keyword phrases intact (better SIoU tags than single words).
    keyword_phrases = _dedup(_lang_values(rec.get("item_keywords")), limit=4)

    if not product_type or not (item_name or bullets):
        return None

    # --- SIoU list fields -----------------------------------------------------
    # Prefer curated tags (style/pattern/fabric); only reach for raw keyword
    # phrases if that leaves the field empty, to keep tags clean.
    curated_tags = _dedup(
        [*styles, *patterns, *_lang_values(rec.get("fabric_type"))], limit=10
    )
    trend_signals = curated_tags or _dedup([product_type, *keyword_phrases], limit=10)
    overall_vibe = _clip_words(
        " ".join(
            x for x in [style, product_type, ("in " + color) if color else ""] if x
        ),
        4,
        10,
    ) or _clip_words(f"{product_type} in {color}" if color else product_type, 3, 10)
    vibe = _dedup([*colors[:2], *materials[:2], *styles[:2], *patterns[:1]], limit=10)
    quick_semantic = _dedup(
        [product_type, *colors[:1], *materials[:2], *patterns[:1], *styles[:2]],
        limit=12,
    )

    # --- cosine text fields ---------------------------------------------------
    if difficulty == "hard":
        # Compose editorial sentences from the STRUCTURED attributes (which the
        # hard prompt hides). Not a copy of any bullet -> the model must infer
        # color/material/style from prose and phrase them editorially.
        quick_summary = _clip_words(
            (f"A {style} " if style else "A ")
            + " ".join(x for x in [color, material, product_type] if x),
            4,
            25,
        )
        rich_semantic = _clip_words(
            " ".join(
                x
                for x in [
                    color,
                    material,
                    product_type,
                    (f"with {pattern} detailing" if pattern else ""),
                    (f"{style} aesthetic" if style else ""),
                    *keyword_phrases,
                ]
                if x
            ),
            12,
            30,
        )
        summary = _clip_words(
            (f"{brand} " if brand else "")
            + " ".join(x for x in [color, material, product_type] if x)
            + (f", a {style} piece" if style else ""),
            6,
            25,
        )
        trend = _clip_words(
            " ".join(x for x in [style, product_type] if x) or product_type, 1, 8
        )
    else:
        lead = bullets[0] if bullets else item_name
        quick_summary = _clip_words(lead, 1, 25)
        rich_semantic = _clip_words(" ".join(bullets) or item_name, 20, 30)
        summary_bits = [b for b in [brand, color, material, product_type] if b]
        summary = _clip_words(
            (" ".join(summary_bits) + (f" -- {style} piece" if style else "")).strip()
            or item_name,
            6,
            25,
        )
        trend = _clip_words(
            " ".join(x for x in [style, product_type] if x) or product_type, 1, 8
        )

    gold = {
        "image_descriptions": {
            "trend_signals": trend_signals,
            "overall_vibe": overall_vibe,
            "quick_summary": quick_summary,
        },
        "semantic_descriptions": {
            "trend": trend,
            "vibe": vibe,
            "rich_semantic": rich_semantic,
            "quick_semantic": quick_semantic,
            "summary": summary,
        },
    }

    # Require every graded field to be non-empty so has_all_fields() passes and
    # no field silently scores None.
    for f in ALL_FIELDS:
        cur: Any = gold
        for part in f.split("."):
            cur = cur.get(part) if isinstance(cur, dict) else None
        if cur in (None, "", []):
            return None
    return gold


# --------------------------------------------------------------------------- #
#  Student prompt (generic e-commerce editorial voice; ABO metadata visible)   #
# --------------------------------------------------------------------------- #

_OUTPUT_SCHEMA_BLOCK = """Otherwise, output as JSON only:

{
  "image_descriptions": {
    "trend_signals": ["list", "of", "style trends", "era/subculture/trend tags"],
    "overall_vibe": "4-10 words summarizing the mood or style/occasion",
    "quick_summary": "One-sentence summary, max 25 words"
  },
  "semantic_descriptions": {
    "trend": "Main trend associated with this product",
    "vibe": ["list", "of", "trend tags", "mood/aesthetic keywords", "max 20 words"],
    "rich_semantic": "20-30 word SEO-style description with synonyms",
    "quick_semantic": ["list", "of", "key attributes", "max 12 words"],
    "summary": "One-sentence summary of the product's style and use, max 25 words"
  }
}
"""

_VOICE_GUIDE = """ABOUT THE EDITORIAL VOICE:
- Confident and playful, never stuffy or generic
- Speaks to the customer with contemporary fashion/retail vocabulary
- Includes practical styling context where relevant
- Balances trend-awareness with accessibility"""


def build_student_prompt(rec: dict, difficulty: str = "easy") -> str:
    """Editorial instruction with a *plainer* subset of ABO metadata visible
    (no style/pattern tags -- those are the gold the model must infer).

    ``difficulty="hard"`` hides the structured attributes (color/material/
    productType/keywords) too, showing only the raw prose (brand + product name
    + bullet points). The model must then *infer* the structured tags and
    editorial phrasing from prose alone -- a genuinely harder task with real
    SFT->RL headroom on the fuzzy semantic-IoU fields."""
    brand = _first(rec.get("brand"))
    item_name = _first(rec.get("item_name"))
    product_type = _product_type(rec)
    color = "; ".join(_lang_values(rec.get("color"))[:2])
    material = "; ".join(
        _dedup(
            _lang_values(rec.get("material")) + _lang_values(rec.get("fabric_type"))
        )[:2]
    )
    bullets = _lang_values(rec.get("bullet_point"))[:5]
    keywords = " ".join(_lang_values(rec.get("item_keywords"))[:1])

    if difficulty == "hard":
        # Only raw prose -- no structured attribute labels.
        md_lines = [
            f"brand: {brand}" if brand else "",
            f"productName: {item_name}" if item_name else "",
        ]
    else:
        md_lines = [
            f"brand: {brand}" if brand else "",
            f"productName: {item_name}" if item_name else "",
            f"productType: {product_type}" if product_type else "",
            f"color: {color}" if color else "",
            f"material: {material}" if material else "",
            f"keywords: {keywords}" if keywords else "",
        ]
    if bullets:
        md_lines.append("productDetails:")
        md_lines.extend(f"- {b}" for b in bullets)
    metadata = "\n".join(line for line in md_lines if line)

    return (
        "Generate product metadata in editorial style.\n\n"
        f"{_VOICE_GUIDE}\n\n"
        "PRODUCT METADATA:\n"
        f"{metadata}\n\n"
        "Generate the following attributes based on what you can infer from the "
        "metadata.\n\n"
        "If you cannot generate accurate metadata, respond with:\n"
        '{\n  "status": "cannot_generate",\n  "reason": "ambiguous_context | other",\n'
        '  "details": "Brief explanation"\n}\n\n'
        f"{_OUTPUT_SCHEMA_BLOCK}\n"
    )


def _is_fashion(rec: dict) -> bool:
    return _product_type_raw(rec) in _FASHION_PRODUCT_TYPES


_SYSTEM_MSG = (
    "You are a helpful AI assistant that generates product metadata in "
    "editorial style."
)


def record_to_row(rec: dict, category: str, difficulty: str = "easy") -> Optional[dict]:
    if category == "fashion" and not _is_fashion(rec):
        return None
    gold = build_gold(rec, difficulty=difficulty)
    if gold is None:
        return None
    completion = json.dumps(gold, ensure_ascii=False, indent=2)
    student_prompt = build_student_prompt(rec, difficulty=difficulty)
    return {
        "product_id": str(rec.get("item_id", "")) or "abo",
        "student_prompt": student_prompt,
        "completion": completion,
        # messages schema for the SFT builder (and also accepted by the GRPO env);
        # keeps the same file drop-in for both training paths.
        "messages": [
            {"role": "system", "content": _SYSTEM_MSG},
            {"role": "user", "content": student_prompt},
            {"role": "assistant", "content": completion},
        ],
    }


# --------------------------------------------------------------------------- #
#  IO                                                                           #
# --------------------------------------------------------------------------- #


def iter_abo_records(paths: list[str]) -> Iterable[dict]:
    for path in paths:
        opener = gzip.open if path.endswith(".gz") else open
        with opener(path, "rt", encoding="utf-8") as fh:
            for line in fh:
                line = line.strip()
                if not line:
                    continue
                try:
                    yield json.loads(line)
                except (json.JSONDecodeError, ValueError):
                    continue


def _write_jsonl(rows: list[dict], path: str) -> None:
    os.makedirs(os.path.dirname(path) or ".", exist_ok=True)
    with open(path, "w", encoding="utf-8") as fh:
        for r in rows:
            fh.write(json.dumps(r, ensure_ascii=False) + "\n")


def _validate_row(row: dict) -> bool:
    try:
        obj = json.loads(row["completion"])
    except (KeyError, json.JSONDecodeError, ValueError):
        return False
    for f in ALL_FIELDS:
        cur: Any = obj
        for part in f.split("."):
            cur = cur.get(part) if isinstance(cur, dict) else None
        if cur in (None, "", []):
            return False
    return True


# --------------------------------------------------------------------------- #
#  Self-test (no ABO download / no network required)                           #
# --------------------------------------------------------------------------- #

_SYNTHETIC = [
    {
        "item_id": "B0SYNTH001",
        "brand": [{"language_tag": "en_US", "value": "Aurora"}],
        "item_name": [
            {
                "language_tag": "en_US",
                "value": "Aurora Womens Velvet Halterneck Cropped Going-Out Top",
            }
        ],
        "product_type": [{"value": "SHIRT"}],
        "color": [{"language_tag": "en_US", "value": "Black"}],
        "material": [{"language_tag": "en_US", "value": "Velvet"}],
        "fabric_type": [{"language_tag": "en_US", "value": "Woven"}],
        "style": [
            {"language_tag": "en_US", "value": "Party"},
            {"language_tag": "en_US", "value": "Evening"},
        ],
        "pattern": [{"language_tag": "en_US", "value": "Floral cutwork"}],
        "item_keywords": [
            {"language_tag": "en_US", "value": "high neck sleeveless open back crop"}
        ],
        "bullet_point": [
            {
                "language_tag": "en_US",
                "value": "Plush velvet halterneck top with a high neck and open back.",
            },
            {
                "language_tag": "en_US",
                "value": "Floral cutwork detailing for an elevated party look.",
            },
            {
                "language_tag": "en_US",
                "value": "Pairs easily with jeans or tailored trousers.",
            },
        ],
    },
    {
        "item_id": "B0SYNTH002",
        "brand": [{"language_tag": "en_US", "value": "Northpeak"}],
        "item_name": [
            {
                "language_tag": "en_US",
                "value": "Northpeak Mens Quilted Puffer Jacket Water-Resistant",
            }
        ],
        "product_type": [{"value": "OUTERWEAR_JACKET"}],
        "color": [{"language_tag": "en_US", "value": "Navy"}],
        "material": [{"language_tag": "en_US", "value": "Recycled polyester"}],
        "style": [{"language_tag": "en_US", "value": "Outdoor"}],
        "pattern": [{"language_tag": "en_US", "value": "Solid"}],
        "item_keywords": [
            {"language_tag": "en_US", "value": "warm insulated winter zip pockets"}
        ],
        "bullet_point": [
            {
                "language_tag": "en_US",
                "value": "Lightweight quilted puffer with water-resistant shell.",
            },
            {
                "language_tag": "en_US",
                "value": "Insulated for warmth on cold-weather commutes and trails.",
            },
        ],
    },
    # A deliberately sparse record that should be rejected.
    {"item_id": "B0SYNTH003", "product_type": [{"value": "SHOES"}]},
]


def _selftest() -> int:
    ok = 0
    made = 0
    for rec in _SYNTHETIC:
        easy_row = record_to_row(rec, category="all", difficulty="easy")
        hard_row = record_to_row(rec, category="all", difficulty="hard")
        if easy_row is None or hard_row is None:
            print(
                f"[skip] {rec.get('item_id')} (too sparse -- expected for B0SYNTH003)"
            )
            continue
        made += 1
        aligned = easy_row["product_id"] == hard_row["product_id"]
        attributes_hidden = all(
            f"{label}:" not in hard_row["student_prompt"]
            for label in ("productType", "color", "material", "keywords")
        )
        valid = (
            _validate_row(easy_row)
            and _validate_row(hard_row)
            and aligned
            and attributes_hidden
        )
        ok += int(valid)
        print("=" * 78)
        print(f"product_id: {easy_row['product_id']}   paired_arms_valid={valid}")
        print("- easy student_prompt (head) -------------------------------------")
        print("\n".join(easy_row["student_prompt"].splitlines()[:14]))
        print("- hard completion (gold) -----------------------------------------")
        print(hard_row["completion"])
    print("=" * 78)
    print(f"rows built: {made}   schema-valid: {ok}")
    expected_built = 2
    if made == expected_built and ok == expected_built:
        print("SELFTEST PASS")
        return 0
    print("SELFTEST FAIL")
    return 1


# --------------------------------------------------------------------------- #
#  CLI                                                                          #
# --------------------------------------------------------------------------- #


def main() -> int:
    ap = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    ap.add_argument("--abo-glob", help="glob for ABO listings_*.json(.gz) files")
    ap.add_argument("--out-dir", default="sample_data/abo", help="output directory")
    ap.add_argument(
        "--category",
        choices=["fashion", "all"],
        default="fashion",
        help="'fashion' keeps only apparel/footwear-like items",
    )
    ap.add_argument(
        "--difficulty",
        choices=["easy", "hard", "both"],
        default="easy",
        help="'hard' hides structured attributes from the prompt and "
        "derives cosine gold from them -> inference task with real "
        "SFT->RL headroom (vs 'easy' near-copy task); 'both' writes "
        "aligned easy/ and hard/ subdirectories",
    )
    ap.add_argument("--max", type=int, default=6000, help="max rows to emit")
    ap.add_argument("--seed", type=int, default=42)
    ap.add_argument("--test-fraction", type=float, default=0.1)
    ap.add_argument("--val-fraction", type=float, default=0.1)
    ap.add_argument(
        "--selftest",
        action="store_true",
        help="run the built-in synthetic mapping check and exit",
    )
    args = ap.parse_args()

    if args.selftest:
        return _selftest()
    if not args.abo_glob:
        ap.error("--abo-glob is required unless --selftest is given")

    paths = sorted(glob.glob(args.abo_glob))
    if not paths:
        ap.error(f"no files matched --abo-glob {args.abo_glob!r}")

    rows: list[dict] = []
    paired_rows: list[tuple[dict, dict]] = []
    scanned = 0
    for rec in iter_abo_records(paths):
        scanned += 1
        if args.difficulty == "both":
            easy_row = record_to_row(rec, category=args.category, difficulty="easy")
            hard_row = record_to_row(rec, category=args.category, difficulty="hard")
            if (
                easy_row
                and hard_row
                and _validate_row(easy_row)
                and _validate_row(hard_row)
                and easy_row["product_id"] == hard_row["product_id"]
            ):
                paired_rows.append((easy_row, hard_row))
        else:
            row = record_to_row(rec, category=args.category, difficulty=args.difficulty)
            if row and _validate_row(row):
                rows.append(row)
        if len(paired_rows if args.difficulty == "both" else rows) >= args.max:
            break

    if args.difficulty == "both":
        random.Random(args.seed).shuffle(paired_rows)
        easy_rows = [pair[0] for pair in paired_rows]
        hard_rows = [pair[1] for pair in paired_rows]
        if not paired_rows:
            print(
                f"scanned {scanned} records, produced 0 valid aligned pairs -- check schema/category"
            )
            return 1
        for arm, arm_rows in (("easy", easy_rows), ("hard", hard_rows)):
            _write_splits(arm_rows, os.path.join(args.out_dir, arm), args)
        print(
            f"scanned {scanned} ABO records -> {len(paired_rows)} aligned easy/hard pairs"
        )
        print(f"written to {args.out_dir}/easy and {args.out_dir}/hard")
        return 0

    if not rows:
        print(
            f"scanned {scanned} records, produced 0 valid rows -- check schema/category"
        )
        return 1

    random.Random(args.seed).shuffle(rows)
    _write_splits(rows, args.out_dir, args)
    print(f"scanned {scanned} ABO records -> {len(rows)} valid rows")
    print(
        f"written to {args.out_dir}/  (all.jsonl, train.jsonl, val.jsonl, test.jsonl)"
    )
    return 0


def _write_splits(rows: list[dict], out_dir: str, args: argparse.Namespace) -> None:
    n = len(rows)
    n_test = int(round(n * args.test_fraction))
    n_val = int(round(n * args.val_fraction))
    test, val, train = (
        rows[:n_test],
        rows[n_test : n_test + n_val],
        rows[n_test + n_val :],
    )

    _write_jsonl(rows, os.path.join(out_dir, "all.jsonl"))
    _write_jsonl(train, os.path.join(out_dir, "train.jsonl"))
    _write_jsonl(val, os.path.join(out_dir, "val.jsonl"))
    _write_jsonl(test, os.path.join(out_dir, "test.jsonl"))
    print(f"{out_dir}: train {len(train)} / val {len(val)} / test {len(test)}")


if __name__ == "__main__":
    raise SystemExit(main())
