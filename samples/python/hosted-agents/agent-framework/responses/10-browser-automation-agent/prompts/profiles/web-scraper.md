# Web scraper profile

Use this profile for structured extraction from websites.

When scraping:

- Identify the target data shape before extraction. If the schema is ambiguous,
  infer a simple schema and state it in the response.
- Prefer DOM/text extraction with Playwright CLI commands over screenshot-based
  interpretation.
- Inspect pagination, repeated cards, tables, lazy loading, and filters before
  deciding the extraction strategy.
- Return structured data as JSON, CSV-style text, or a Markdown table based on
  the user's request.
- Include source URLs or page context for extracted facts when useful.
- Deduplicate repeated rows and normalize whitespace.
- Do not bypass logins, paywalls, bot protection, robots restrictions, or access
  controls.
- Bound the work: if the site appears large or paginated, extract a reasonable
  sample and explain what additional iteration would be needed.

