# Privacy, security, and licensing

## Privacy promise

Key FX Board uses a global keyboard hook so it can play a sound. That is the entire reason it sees keys.

**We do not record, store, or transmit what you type.**

Normative rules:

1. No keystroke journal, even in memory beyond the current pressed-set and a tiny realtime queue.
2. No `KeyId` / `vkCode` in logs, crash dumps we control, or update telemetry.
3. No network calls in v1 except optional update check (Velopack) and only version metadata.
4. No reading of foreground window titles in v1.
5. Speak-key (later) is opt-in and still must not persist text.

This is a product feature, not a footer.

## Security

| Topic | Stance |
| --- | --- |
| Hook | Listen only, never suppress keys |
| Packs | Treat zips as untrusted: zip-slip checks, size caps, WAV-only in `.kfxpack`. Custom folder may use other Windows-decodable formats. |
| Updates | HTTPS when enabled |
| Elevation | Do not require admin |
| Secrets | None in the repo. No API keys for v1 |
| Single instance | Prevent two hooks |

Packs can contain malicious huge files; we reject them. Packs cannot contain code we execute. **Never** run scripts from a pack.

JSON is data, not plugins. No `Assembly.Load` from pack folders.

## Antivirus honesty

Global hooks trigger heuristics. We:

- Keep the implementation small and readable
- Document why the hook exists (this file + About)
- Sign later
- Do not ship obfuscation (looks worse to AV and to OSS)

## Licensing

### Application code

**MIT.** When the repo goes public, every source file / `LICENSE` at root.

Third-party notices: Avalonia, NAudio, Velopack, .NET, etc. in `THIRD_PARTY_NOTICES` at ship time.

### Samples and packs

Separate from MIT.

| Content | License |
| --- | --- |
| Factory placeholder clicks we generate | CC0-1.0 or MIT-like “use freely” |
| User-made packs and custom samples | Whatever they put in `manifest.license`, or their own files |
| Third-party piano libraries | **Do not ship** unless we have rights |

Piano realism is a **content** problem. MVP uses synthesized piano. The engine does not care.

### Open source and a later product

Compatible:

- Engine MIT
- Pack spec public
- Official installer on GitHub Releases
- Optional later: paid packs or a pro skin, sold as content / extra features
- No need to close the core to charge for packs

If we ever dual-license, that is a future legal decision, not an architecture change.

## Contributor expectations (when public)

- Do not commit copyrighted WAVs
- Do not add analytics without an obvious opt-in
- Hook and mapper changes require a privacy pass in review
