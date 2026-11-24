# host-reviewer lessons, #1204/#1210 PR (return page retries a short refusal, 9e24225)

- A `Retry-After` on the host's own gate is only as true as the gate's release: CodeExchangeSignIn's AtOnce says 1 s, but a place frees when an exchange answers, up to Patience (10 s) when the provider is slow; the page's one retry then reads 503 again. Check the wait against what frees the gate (#1204 PR).
- Retrying a code-flow post is safe only while the refusal comes before the provider is asked (Admit, AtOnce) and `recent` is read, not spent (ReadRecent); recheck if either gate moves after the exchange (#1204 PR).
- A page retry keyed on status and `Retry-After` alone also fires on a 503 the host never wrote (Cloudflare, the platform's proxy), maybe after the code was spent: key it on the host's namespaced code too (#1204 PR, 7bd86cd).
