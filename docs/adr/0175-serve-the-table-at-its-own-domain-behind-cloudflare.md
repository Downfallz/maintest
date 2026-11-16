# 0175. Serve the table at its own domain, behind Cloudflare

Date: 2026-11-13

Status: Accepted. Amends [0080](0080-host-the-table-on-azure-container-apps.md)

## Context

The table answers at the platform's address, `downfall-table.<...>.canadacentral.azurecontainerapps.io`
(ADR 0080). Every invitation link, seat link, replay link and phone notice carries that address, and so does
everything a browser keeps per origin: the session cookie, the saved seats, the presentation preference and the
push subscriptions. The project now owns `downfallinstinct.com`, its DNS on Cloudflare.

The address the game answers at has to outlive the host it runs on. The apex is the brand's front door, and a
public page that explains the game is wanted there, apart from the game.

## Decision

We will serve the table at `play.downfallinstinct.com`, through Cloudflare's proxy, and keep the apex for a
public site of its own, decided apart.

- Cloudflare proxies `play`, a CNAME to the platform's address, in Full (strict) TLS. Container Apps binds the host
  name (`customDomain`) with a Cloudflare Origin CA certificate uploaded once, by hand, to the environment. Its
  private key is in no repository, variable or workflow secret, which keeps ADR 0082's promise. The domain is
  proven to Azure by a TXT record, `asuid.play`.
- Once the domain serves, the API answers Cloudflare's IPv4 ranges alone (`onlyCloudflare`, the
  `TABLE_ONLY_CLOUDFLARE` variable), so the platform's address no longer bypasses Cloudflare's TLS, WAF and rate
  limits. The ranges are written in `infra/main.bicep`.
- The page and its API stay on one origin. No `api.` host: the same-origin fence and the `SameSite=Lax` session
  cookie depend on it.
- The operator's sign-in lists both redirect URIs while the domain is brought in.

## Consequences

- Good: links, cookies and push subscriptions are bound to a name the project owns, not to the host; moving off
  Azure changes one DNS record.
- Good: Cloudflare absorbs abusive traffic before it wakes a replica that scales to zero.
- Bad: moving origin loses what browsers keep per origin. Players sign in again, re-open seats from their
  account, and turn on notifications again on each device.
- Bad: the Cloudflare ranges are a copy; a range Cloudflare adds is refused until the list is updated.
- Bad: the Origin CA certificate is trusted by Cloudflare only and expires after the validity chosen (15 years).
- Neutral: the Google OAuth client and the operator's sign-in list the new origin by hand (infra/README.md).

## Alternatives considered

- The game on the apex: the simplest address, but a public site there later would move the game again, and an
  apex CNAME needs flattening for Azure's validation.
- Cloudflare in DNS only, with Azure's managed certificate: fewer parts, but no WAF, and the platform's address
  stays open.
- Cloudflare Tunnel: needs `cloudflared` running all the time, which ends the scale to zero.
- Azure Front Door: about 35 dollars a month for what Cloudflare's free plan gives here.
