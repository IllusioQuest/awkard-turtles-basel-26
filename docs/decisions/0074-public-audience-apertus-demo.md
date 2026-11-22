# 0074 — Temporary public audience demo on one laptop

Status: proposed. T-88. The user chose four workers on one laptop, a public visitor page, Apertus, a one-click question, Lovable, and a visible four-panel job/heat display after the main live demo.

## Decision

Run four Docker workload identities on one physical laptop, owned by one `WorkloadCoordinator`. Use its existing heat-first placement and batch heat-control path; do not alter scored reference-house figures or the offline January/July demo. The four heat sites are explicitly illustrative reference-server equivalents; their demand figures are scenario inputs, and physical laptop heat is not measured. An Apertus 8B Q4 container answers each accepted question, using the locally measured model family and existing llama.cpp image. A read-only host mount stages the verified model once into a dedicated Docker volume; inference containers see only that volume, avoiding a slow multi-gigabyte image layer and repeated Windows filesystem reads. The existing storage contract requires a pinned node, so a `WorkloadPlacement` preview selects a node using the same heat ranking; the coordinator then independently admits the pinned request. Show real observed lifecycle state and generated text separately from synthetic heat.

Expose a bounded public question API through a temporary Cloudflare Quick Tunnel. The Lovable site hosts the public question form and calls that API; it runs no Lovable AI or backend. The operator view runs on laptop localhost. The audience is told that prompts and answers are public and to submit no personal or confidential information. Limit prompt length, active queue and repeat submissions. A static Lovable URL is suitable for a QR; the temporary tunnel URL must be updated in its API setting before each session.

## Why and limits

This provides a live, real local AI result and visual placement without changing the scored model. It is a public demonstration of local compute control, not proof of end-to-end data sovereignty or legal compliance: visitor traffic crosses Lovable and Cloudflare, and the demo accepts only public text. Four workers are logical partitions of one machine, not four independent physical heat nodes. The short-question runner has not been energy-measured; the 0.8 kWh/h display is the case's one-kilowatt reference-server assumption and is never attributed to this laptop. Quick Tunnel has no stable hostname or uptime guarantee. The existing offline demo remains the reliable pitch proof.

## Review

Fresh-context verdict: **go with changes**. The reviewer identified three risks: fractional heat signals cannot be represented faithfully by the Docker batch pause path; four simultaneous 8B containers need host-specific timing and memory validation; illustrative heat demands need a traceable configuration. Applied zero/full heat-state restriction to the operator input and runtime control, moved scenario values and their sources to `config/audience-apertus-demo.json`, and completed a four-job rehearsal on this laptop. All four answered, in 314–442 seconds. The display therefore emphasizes immediate placement and heat-state decisions; visitors receive answers later. We retain the four-worker goal because the user explicitly requested it, and make no physical-node or measured-heat claim. The existing offline pitch demo remains the primary scored proof.
