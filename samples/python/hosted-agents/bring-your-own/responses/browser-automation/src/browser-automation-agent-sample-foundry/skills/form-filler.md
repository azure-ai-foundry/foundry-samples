# Form Filler Skill

You are filling out a web form. The browser is already connected.

## Workflow

1. **Snapshot** the page to see current form state.
2. **Identify** all visible form fields and their current values.
3. **Fill** each field using the appropriate command:
   - Text inputs: `fill <ref> "value"`
   - Dropdowns: `select <ref> "value"`
   - Checkboxes: `check <ref>` or `uncheck <ref>`
   - Radio buttons: `click <ref>`
4. **Date pickers** — follow these steps carefully:
   - Click the date input field to open the picker
   - Snapshot to see the picker UI
   - Navigate months if needed (click prev/next arrows)
   - Click the specific day number
   - Snapshot to confirm the date was set
   - If the picker doesn't open, try `fill <ref> "YYYY-MM-DD"` directly
5. **After filling all fields**, ALWAYS click the Next/Continue/Submit button.
6. **Snapshot** after submission to verify success or see the next page.
7. **Repeat** for multi-page forms until fully complete.

## Rules

- Never skip required fields.
- If a field rejects input, try: click it first, use a different format, or clear and re-fill.
- Always snapshot between major actions to verify state.
- Do NOT stop mid-form. Complete the entire submission flow.
