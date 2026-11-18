# The table's own domain, behind Cloudflare

The table answers at `play.downfallinstinct.com` through Cloudflare's proxy (ADR 0175). Once, in this order:

1. **Cloudflare, DNS** (both *DNS only*, grey, for now):
   - `CNAME play` → the platform's address (the deploy summary's *Platform address*, without `https://`).
   - `TXT asuid.play` → the deploy summary's *Domain verification* value.
2. **Cloudflare, SSL/TLS**: mode *Full (strict)*. Under *Origin Server*, *Create Certificate*: RSA (2048),
   host names `play.downfallinstinct.com` and `*.downfallinstinct.com`, 15 years, PEM. Never paste the private
   key anywhere but the files below.
3. **Upload the certificate**, in Cloud Shell, then delete the files:

   ```bash
   nano origin.pem   # the certificate
   nano origin.key   # the private key
   openssl pkcs12 -export -out origin.pfx -inkey origin.key -in origin.pem -passout pass:
   environment="$(az containerapp env list -g downfall-table --query "[0].name" -o tsv)"
   az containerapp env certificate upload -g downfall-table -n "$environment" \
     --certificate-file origin.pfx --certificate-name cloudflare-origin
   rm origin.pem origin.key origin.pfx
   ```

4. **Bind it**: set the repository variable `TABLE_DOMAIN` to `play.downfallinstinct.com` and run *Deploy the table*.
5. **Proxy it**: switch the `play` record to *Proxied* (orange).
6. **Sign-ins**: give the domain to the admin panel's redirect and the Google client's origins,
   [sign-ins.md](sign-ins.md).
7. **Close the platform's address**, once players have moved: compare the ranges in `infra/main.bicep` with
   <https://www.cloudflare.com/ips-v4>, set `TABLE_ONLY_CLOUDFLARE` to `true`, and run *Deploy the table*. The
   platform's address then answers Cloudflare alone.

In Cloudflare, keep Rocket Loader and Email Address Obfuscation off (they rewrite the pages and break their ETags,
ADR 0166) and bypass the cache on `/api/*` and `/.auth/*`. A browser keeps its cookie, saved seats and push
subscription per origin, so players sign in again on the new address and turn notifications on again.
