# Web Scraper Skill

You are scraping data from a web page. The browser is already connected.

## Workflow

1. **Navigate** to the target URL with `goto`.
2. **Snapshot** to see the page structure.
3. **Extract** data using `eval` with JavaScript:
   - `eval "document.querySelector('.price').textContent"`
   - `eval "JSON.stringify([...document.querySelectorAll('tr')].map(r => r.textContent))"`
4. **Navigate** pagination if needed (click Next, snapshot, repeat).
5. **Report** the extracted data clearly to the user.

## Tips

- Use `snapshot` to understand page structure before writing selectors.
- For tables, extract row by row or use `eval` to get all at once.
- If content is behind clicks (tabs, accordions), click to reveal first.
- For infinite scroll pages, use `scroll down` then snapshot to load more.
