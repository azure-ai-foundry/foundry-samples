# Azure AI Foundry Project Files API — Reference

Verified live against a real project on 2026-07-22. All five operations confirmed working.

## Endpoint & auth

- **Base**: `{PROJECT_ENDPOINT}/files`
  where `PROJECT_ENDPOINT` = `https://<resource>.services.ai.azure.com/api/projects/<project>`
- **api-version**: `2025-05-15-preview` — **required on every request** (query string).
  Omitting it returns `400 { "code": "BadRequest", "message": "Missing required query parameter: api-version" }`.
- **Auth header**: `Authorization: Bearer <token>`
- **Token scope**: `https://ai.azure.com/.default` (also accepts `https://cognitiveservices.azure.com/.default`).
- OpenAI-compatible alias: `{PROJECT_ENDPOINT}/openai/v1/files` also works and does **not** require api-version.

## Operations

| Operation | HTTP | Path |
|---|---|---|
| List | `GET` | `/files?api-version=2025-05-15-preview` |
| Upload | `POST` | `/files?api-version=2025-05-15-preview` (multipart/form-data) |
| Get metadata | `GET` | `/files/{file_id}?api-version=2025-05-15-preview` |
| Download content | `GET` | `/files/{file_id}/content?api-version=2025-05-15-preview` |
| Delete | `DELETE` | `/files/{file_id}?api-version=2025-05-15-preview` |

### Upload form fields (multipart/form-data)

- `purpose` (required): one of `assistants`, `batch`, `fine-tune`, `vision`.
- `file` (required): the file part (`filename` + bytes).

### Response shapes

Upload / Get:
```json
{ "object": "file", "id": "assistant-XXXX", "purpose": "assistants",
  "filename": "data.txt", "bytes": 67, "created_at": 1784711786,
  "expires_at": null, "status": "processed", "status_details": null }
```
List: `{ "object": "list", "data": [ ...file objects... ], "has_more": bool, "first_id": ..., "last_id": ... }`
Download content: raw file bytes (not JSON).
Delete: `{ "object": "file", "deleted": true, "id": "assistant-XXXX" }`

## Gotchas learned from live testing

1. **File extension allow-list.** Upload rejects unsupported extensions with HTTP 400. Notably **`.jsonl` is NOT allowed** — batch/fine-tune data must be renamed (e.g. `.json` / `.txt`) or the call fails. Supported extensions:
   `c, cpp, css, csv, doc, docx, gif, go, html, java, jpeg, jpg, js, json, md, pdf, php, pkl, png, pptx, py, rb, tar, tex, ts, txt, webp, xlsx, xml, zip`
2. **api-version is mandatory** on `/files`, `/files/{id}`, and `/files/{id}/delete`. The `/content` download tolerated its absence in testing, but always include it.
3. **File IDs are prefixed** `assistant-...` when uploaded with `purpose=assistants`.
4. **RBAC**: the caller identity needs data-plane access to the project. In the managed harness, the Hand shares a 1P sandbox identity; if `list` returns `403`/`401`, the identity lacks a role assignment on the project account — surface that clearly rather than retrying.

## Raw curl equivalents

```bash
BASE="$PROJECT_ENDPOINT"; AV="2025-05-15-preview"
TOK=$(az account get-access-token --scope "https://ai.azure.com/.default" --query accessToken -o tsv)

curl -s "$BASE/files?api-version=$AV" -H "Authorization: Bearer $TOK"                       # list
curl -s -X POST "$BASE/files?api-version=$AV" -H "Authorization: Bearer $TOK" \
     -F purpose=assistants -F "file=@./notes.txt"                                            # upload
curl -s "$BASE/files/$FID?api-version=$AV" -H "Authorization: Bearer $TOK"                   # metadata
curl -s "$BASE/files/$FID/content?api-version=$AV" -H "Authorization: Bearer $TOK"           # download
curl -s -X DELETE "$BASE/files/$FID?api-version=$AV" -H "Authorization: Bearer $TOK"         # delete
```
