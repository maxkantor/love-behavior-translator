# Amplify SPA Routing

If refreshing `/platform` (or other client routes like `/admin`) returns 404, add this rewrite rule in **AWS Amplify Console** → your app → **App settings** → **Redirects and rewrites**:

**Option 1 – exclude static assets (recommended):**
- Source: `^/(?!.*\.(css|gif|ico|jpg|js|png|txt|svg|woff|woff2|map)$).*`
- Target: `/index.html`
- Type: Rewrite
- Status: 200

**Option 2 – catch-all (simpler):**
- Source: `/<*>`
- Target: `/index.html`
- Type: Rewrite
- Status: 200

This ensures all non-static paths (including `/platform`) serve `index.html`, allowing React Router to handle routing.
