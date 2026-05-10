#!/usr/bin/env python3
# Copyright (c) Microsoft. All rights reserved.

from __future__ import annotations

import argparse
import json
import os
import re
import textwrap
from dataclasses import dataclass, field
from pathlib import Path

PAGE_WIDTH = 595
PAGE_HEIGHT = 842
MARGIN = 54


def safe_text(value: object) -> str:
    text = str(value)
    return text.encode("latin-1", "replace").decode("latin-1")


def pdf_escape(value: object) -> str:
    text = safe_text(value)
    return text.replace("\\", "\\\\").replace("(", "\\(").replace(")", "\\)")


def slugify(value: str) -> str:
    slug = re.sub(r"[^a-z0-9]+", "-", value.lower()).strip("-")
    return slug or "city"


def display_path(path: Path) -> str:
    home = Path.home().resolve()
    resolved_path = path.resolve()
    try:
        return f"$HOME/{resolved_path.relative_to(home).as_posix()}"
    except ValueError:
        return str(resolved_path)


def rgb(hex_color: str) -> tuple[float, float, float]:
    hex_color = hex_color.lstrip("#")
    return tuple(int(hex_color[index:index + 2], 16) / 255 for index in (0, 2, 4))


@dataclass
class PdfPage:
    commands: list[str] = field(default_factory=list)

    def rect(self, x: float, top: float, width: float, height: float, color: str) -> None:
        red, green, blue = rgb(color)
        y = PAGE_HEIGHT - top - height
        self.commands.append(f"{red:.3f} {green:.3f} {blue:.3f} rg {x:.1f} {y:.1f} {width:.1f} {height:.1f} re f")

    def text(self, x: float, top: float, value: object, size: int = 12, color: str = "#1f2937", bold: bool = False) -> None:
        red, green, blue = rgb(color)
        font = "F2" if bold else "F1"
        y = PAGE_HEIGHT - top
        self.commands.append(
            f"BT /{font} {size} Tf {red:.3f} {green:.3f} {blue:.3f} rg {x:.1f} {y:.1f} Td ({pdf_escape(value)}) Tj ET"
        )

    def wrapped_text(
        self,
        x: float,
        top: float,
        value: object,
        *,
        size: int = 12,
        color: str = "#1f2937",
        bold: bool = False,
        width_chars: int = 70,
        line_gap: int = 17,
    ) -> float:
        y = top
        for line in textwrap.wrap(safe_text(value), width=width_chars) or [""]:
            self.text(x, y, line, size=size, color=color, bold=bold)
            y += line_gap
        return y

    def section(self, title: str, top: float, accent: str = "#2563eb") -> float:
        self.rect(MARGIN, top - 17, 7, 24, accent)
        self.text(MARGIN + 17, top, title, size=18, color="#111827", bold=True)
        return top + 31


def build_pdf(pages: list[PdfPage], output_path: Path) -> None:
    objects: list[tuple[int, bytes]] = [
        (1, b"<< /Type /Catalog /Pages 2 0 R >>"),
        (3, b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"),
        (4, b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>"),
    ]

    kids = []
    for index, page in enumerate(pages):
        page_id = 5 + index * 2
        content_id = page_id + 1
        kids.append(f"{page_id} 0 R")
        stream = "\n".join(page.commands).encode("latin-1", "replace")
        objects.append(
            (
                page_id,
                (
                    f"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PAGE_WIDTH} {PAGE_HEIGHT}] "
                    f"/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {content_id} 0 R >>"
                ).encode("ascii"),
            )
        )
        objects.append(
            (
                content_id,
                b"<< /Length " + str(len(stream)).encode("ascii") + b" >>\nstream\n" + stream + b"\nendstream",
            )
        )

    objects.append((2, f"<< /Type /Pages /Kids [{' '.join(kids)}] /Count {len(pages)} >>".encode("ascii")))
    objects.sort(key=lambda item: item[0])

    output = bytearray(b"%PDF-1.4\n%\xe2\xe3\xcf\xd3\n")
    offsets = [0]
    for object_id, content in objects:
        offsets.append(len(output))
        output.extend(f"{object_id} 0 obj\n".encode("ascii"))
        output.extend(content)
        output.extend(b"\nendobj\n")

    xref_start = len(output)
    output.extend(f"xref\n0 {len(objects) + 1}\n".encode("ascii"))
    output.extend(b"0000000000 65535 f \n")
    for offset in offsets[1:]:
        output.extend(f"{offset:010d} 00000 n \n".encode("ascii"))
    output.extend(
        f"trailer\n<< /Size {len(objects) + 1} /Root 1 0 R >>\nstartxref\n{xref_start}\n%%EOF\n".encode("ascii")
    )

    output_path.write_bytes(output)


def normalize_interests(raw: str) -> list[str]:
    interests = [item.strip().lower() for item in raw.split(",") if item.strip()]
    return interests or ["food", "history", "art", "views"]


def top_experiences(city: str, interests: list[str]) -> list[str]:
    experiences = [
        f"Begin with a golden-hour walk through {city}'s most atmospheric streets.",
        "Choose one anchor museum, market, or landmark each day, then leave room for wandering.",
        "Plan a sunset viewpoint and a relaxed dinner nearby to avoid backtracking.",
    ]
    interest_map = {
        "food": "Book one local food experience: a market crawl, cooking class, or neighborhood tasting route.",
        "art": "Add a gallery district or design shop loop for a creative afternoon.",
        "history": "Pair the main historic sight with a smaller local museum for context without crowds.",
        "views": "Build in a rooftop, hilltop, riverfront, or observation stop for photos.",
        "neighborhoods": "Explore two contrasting neighborhoods instead of trying to cross the whole city.",
        "shopping": "Save space for local makers, bookshops, markets, and design boutiques.",
        "family": "Alternate big sights with parks, treats, and short transit hops.",
    }
    for interest in interests:
        if interest in interest_map:
            experiences.append(interest_map[interest])
    return experiences[:7]


def itinerary(city: str, days: int, interests: list[str]) -> list[tuple[str, list[str]]]:
    themes = [
        ("Arrival and icons", ["Historic center orientation walk", "Signature landmark or museum", "Sunset viewpoint"]),
        ("Neighborhood flavor", ["Local market breakfast", "Two-neighborhood walking loop", "Casual dinner on a lively side street"]),
        ("Culture and slow travel", ["Museum or gallery morning", "Cafe break and independent shops", "Evening performance or waterfront stroll"]),
        ("Hidden corners", ["Quiet park or garden", "Lesser-known district", "Chef-led, street-food, or family-run dinner"]),
        ("Day trip energy", ["Short regional excursion", "Scenic lunch stop", "Return for an easy evening"]),
        ("Active city day", ["Bike, boat, hike, or long promenade", "Picnic or food-hall lunch", "Golden-hour photo route"]),
        ("Favorites and farewell", ["Revisit the best neighborhood", "Buy local gifts", "Final meal with a view"]),
    ]
    if "food" in interests:
        themes[1][1][0] = "Market breakfast and local tasting crawl"
    if "art" in interests:
        themes[2][1][0] = "Museum, gallery, or design district morning"
    if "views" in interests:
        themes[0][1][2] = "Best sunset viewpoint or rooftop"
    return [(f"Day {index + 1}: {themes[index][0]}", themes[index][1]) for index in range(days)]


def add_header(page: PdfPage, city: str, subtitle: str) -> None:
    page.rect(0, 0, PAGE_WIDTH, 88, "#dbeafe")
    page.rect(0, 88, PAGE_WIDTH, 8, "#2563eb")
    page.text(MARGIN, 38, city, size=26, color="#111827", bold=True)
    page.text(MARGIN, 66, subtitle, size=12, color="#374151")


def add_bullets(page: PdfPage, items: list[str], top: float, *, color: str = "#1f2937") -> float:
    y = top
    for item in items:
        page.text(MARGIN + 8, y, "-", size=12, color="#2563eb", bold=True)
        y = page.wrapped_text(MARGIN + 25, y, item, size=11, color=color, width_chars=72, line_gap=15)
        y += 6
    return y


def build_travel_guide(city: str, days: int, interests: list[str], tone: str, output_path: Path) -> int:
    pages: list[PdfPage] = []

    cover = PdfPage()
    cover.rect(0, 0, PAGE_WIDTH, PAGE_HEIGHT, "#eff6ff")
    cover.rect(0, 0, PAGE_WIDTH, 170, "#2563eb")
    cover.rect(54, 122, 190, 12, "#f97316")
    cover.rect(268, 122, 115, 12, "#10b981")
    cover.rect(407, 122, 134, 12, "#facc15")
    cover.text(MARGIN, 82, "Colorful City Guide", size=22, color="#ffffff", bold=True)
    cover.text(MARGIN, 120, city, size=42, color="#ffffff", bold=True)
    cover.wrapped_text(
        MARGIN,
        210,
        f"A {days}-day {tone} travel guide with itinerary ideas, local flavor, practical tips, and photo-worthy stops.",
        size=16,
        color="#111827",
        bold=True,
        width_chars=52,
        line_gap=22,
    )
    cover.rect(MARGIN, 330, 487, 190, "#ffffff")
    cover.text(MARGIN + 26, 374, "Best for", size=17, color="#111827", bold=True)
    add_bullets(cover, [interest.title() for interest in interests[:6]], 405)
    cover.text(MARGIN, 742, "Generated by the Agent Framework travel-guide skill", size=11, color="#6b7280")
    pages.append(cover)

    overview = PdfPage()
    add_header(overview, city, "Highlights and planning compass")
    y = overview.section("Top experiences", 140, "#f97316")
    y = add_bullets(overview, top_experiences(city, interests), y)
    y = overview.section("Neighborhood strategy", y + 18, "#10b981")
    y = add_bullets(
        overview,
        [
            "Pick one compact base area with easy transit and strong evening food options.",
            "Group sights by neighborhood so each day has fewer transfers and more serendipity.",
            "Use mornings for major attractions, afternoons for cafes and local streets, evenings for views and food.",
        ],
        y,
    )
    y = overview.section("Food and drink notes", y + 18, "#7c3aed")
    add_bullets(
        overview,
        [
            "Reserve one special meal, then keep the rest flexible for markets, bakeries, and casual local spots.",
            "Ask for seasonal specialties and house recommendations rather than only ordering famous dishes.",
        ],
        y,
    )
    pages.append(overview)

    plan = PdfPage()
    add_header(plan, city, f"{days}-day itinerary")
    y = 140
    for title, items in itinerary(city, days, interests):
        if y > 690:
            pages.append(plan)
            plan = PdfPage()
            add_header(plan, city, f"{days}-day itinerary continued")
            y = 140
        y = plan.section(title, y, "#2563eb")
        y = add_bullets(plan, items, y)
        y += 12
    pages.append(plan)

    tips = PdfPage()
    add_header(tips, city, "Practical tips and finishing touches")
    y = tips.section("Easy logistics", 140, "#10b981")
    y = add_bullets(
        tips,
        [
            "Keep the first afternoon light: check in, walk the local area, and save the ambitious plan for day two.",
            "Download offline maps and pin your hotel, transit stops, dinner options, and backup rainy-day sights.",
            "Carry a reusable water bottle, a compact umbrella, and one comfortable layer for changing weather.",
        ],
        y,
    )
    y = tips.section("Photo checklist", y + 18, "#f97316")
    y = add_bullets(
        tips,
        [
            "Wide establishing shot from a viewpoint",
            "Street detail: tiles, signs, doors, markets, or transit",
            "One food photo in natural light",
            "Blue-hour skyline or waterfront scene",
        ],
        y,
    )
    y = tips.section("Before you go", y + 18, "#7c3aed")
    add_bullets(
        tips,
        [
            "Confirm opening days for museums and restaurants.",
            "Check local transit passes and airport transfer options.",
            "Leave one open block for discoveries, weather changes, or a slower morning.",
        ],
        y,
    )
    pages.append(tips)

    build_pdf(pages, output_path)
    return len(pages)


def main() -> None:
    parser = argparse.ArgumentParser(description="Create a colorful PDF city travel guide.")
    parser.add_argument("--city", required=True, help="Destination city for the guide.")
    parser.add_argument("--days", type=int, default=3, help="Number of itinerary days, from 1 to 7.")
    parser.add_argument("--interests", default="food,history,art,views", help="Comma-separated interests.")
    parser.add_argument("--tone", default="first-time visitor", help="Guide style or audience.")
    parser.add_argument(
        "--output-dir",
        default=os.getenv("TRAVEL_GUIDE_OUTPUT_DIR") or str(Path.home() / "generated-travel-guides"),
        help="Directory where the generated PDF should be saved.",
    )
    args = parser.parse_args()

    days = min(max(args.days, 1), 7)
    interests = normalize_interests(args.interests)
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    output_path = output_dir / f"{slugify(args.city)}-{days}-day-travel-guide.pdf"

    page_count = build_travel_guide(args.city, days, interests, args.tone, output_path)
    print(
        json.dumps(
            {
                "city": args.city,
                "days": days,
                "interests": interests,
                "pages": page_count,
                "path": display_path(output_path),
                "message": f"Created a colorful PDF travel guide for {args.city}.",
            },
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
