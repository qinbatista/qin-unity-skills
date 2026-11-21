# Unity C# Service Integration

Use for Unity services, platform SDKs and provider initialization.

## Stable boundary

- A neutral public contract can shield callers from optional or interchangeable SDKs. For a direct single-owner operation, use the [common method boundary rules](unity-csharp-rules.md#c-style).
- Keep initialization explicit with observable states such as uninitialized, initializing, ready, and failed. Coalesce concurrent initialization, reject or queue calls according to the declared contract, and do not initialize the same SDK from multiple owners.
- Forward each callback or operation once. Avoid duplicate event bridges, duplicate save calls, nested manager-to-manager forwarding, and hidden initialization in getters or unrelated methods.

## Transfer into a receiving game

- Bind the selected SDK and receiver revisions. Resolve the required assembly and package dependencies, including transitive references, platform defines, native plugins and any linker preservation needed for reflected code in the Player. Keep optional modules outside the transfer unless needed by those dependencies.
- Configure the receiving project's service identity and environment in its owned configuration. Keep credentials with their existing provider or secret owner and preserve the game's separate build and signing configuration.
- Check the selected Player's initialization and normal gameplay consumer. Bind evidence to the installed artifact, provider environment and operation identity. Observe the terminal result and the authoritative state for a service operation that changes persistent data or currency.

## Optional provider and proof boundary

Use [state and side-effect ownership](https://github.com/qinbatista/qin-codex-skills/blob/master/code-skill/references/state-and-side-effects.md) for configuration handoff, operation identity, duplicate callbacks, and uncertain retries; keep those semantics in the actual service owner.

- The optional SDK assembly may be absent. Keep compile-time provider selection and runtime availability explicit; do not scatter conditional SDK branches through gameplay callers.
- Preserve cancellation, exception, offline, and retry semantics at the provider boundary. Do not add silent fallback providers or report a successful cloud/device operation from local source inspection alone.
- Use a thin test UI only to exercise the public facade. It must not become a second service owner or bypass the neutral interface.
- Claim editor, device, cloud, sandbox, production, or general-availability support only when that exact surface has fresh evidence under [Verify's target-bound evidence](https://github.com/qinbatista/qin-codex-skills/blob/master/verify-skill/SKILL.md#bind-evidence-to-the-actual-target).

## Purchase and durable side effects

Establish the provider and exact reward, persistence, confirmation and wallet owners before wiring purchase UI. For an explicitly local diagnostic adapter that owns native pending confirmation, commit and read back the local reward and transaction ledger before confirming that pending order. Preserve deduplication across restart and a failed confirmation. A server-authoritative provider retains its own confirmation and wallet contract. Its final result updates the receiver through that boundary.

Verify transaction identity, repeated callbacks, save or provider failure, pending recovery, reentry and process restart where applicable. Record provider confirmation, persistence and authoritative balance with their separate evidence. Use a clearly labeled simulation only for its authorized local test scope. Exercise actual native store flow in the selected environment when making a store claim.
