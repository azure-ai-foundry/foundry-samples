---
name: travel-guide
description: Creates colorful PDF travel guides for cities, including itinerary ideas, neighborhoods, food, practical tips, and photo-worthy stops. Use when the user asks for a travel guide, city guide, itinerary, trip plan, or PDF document for a destination.
---

# Travel guide skill

Use this skill when the user wants a city travel guide, itinerary, or downloadable PDF trip-planning document.

## Workflow

1. Identify the city or destination from the user's request.
2. Infer the trip length and interests when provided. If the user does not specify them, use a 3-day guide and a balanced mix of culture, food, neighborhoods, views, and practical tips.
3. Run the PDF generator script:
   - skill name: `travel-guide`
   - script name: `scripts/create_travel_guide.py`
   - args:
     - `city`: destination city, required
     - `days`: number of itinerary days, optional, defaults to `3`
     - `interests`: comma-separated interests such as `food,art,history,views`, optional
     - `tone`: guide style such as `family-friendly`, `luxury`, `budget`, or `first-time visitor`, optional
4. After the script returns, tell the user the `$HOME`-based PDF path and briefly summarize the guide.

## Available scripts

- `scripts/create_travel_guide.py` - Generates a colorful PDF travel guide and returns JSON with the saved file path.

## Example script arguments

```json
{
  "city": "Lisbon",
  "days": 3,
  "interests": "food,viewpoints,neighborhoods",
  "tone": "first-time visitor"
}
```
