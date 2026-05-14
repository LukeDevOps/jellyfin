# StreamHub Plugin — Requirements & Setup

StreamHub is a Jellyfin plugin that lets you search for movies and TV shows via Prowlarr, stream them instantly through Real-Debrid, and browse personalised recommendations and watch history from Trakt.

---

## Services Required

### Real-Debrid (required)
A premium link hoster that caches torrents and serves them as direct HTTP streams.

- Sign up at https://real-debrid.com
- Go to **Account → API Keys** and generate a key

### Prowlarr (required for search)
A self-hosted meta-indexer that aggregates torrent indexers.

Run locally with Docker:
```bash
docker run -d \
  --name=prowlarr \
  -p 9696:9696 \
  -e PUID=501 \
  -e PGID=20 \
  -v ~/.prowlarr:/config \
  --dns 8.8.8.8 \
  --dns 8.8.4.4 \
  --restart unless-stopped \
  lscr.io/linuxserver/prowlarr:latest
```

> The `--dns` flags are required to fix DNS resolution issues inside the Docker container that prevent indexers from connecting.

Open `http://localhost:9696`, add indexers under **Indexers**, then copy the API key from **Settings → General**.

To recreate the container (e.g. to add DNS flags to an existing install), your config is preserved in `~/.prowlarr`:
```bash
docker stop prowlarr && docker rm prowlarr
# then run the docker run command above
```

### Trakt (optional)
Used for watch history ("Continue Watching") and personalised recommendations.

- Sign up at https://trakt.tv
- Go to **Settings → Your API Apps** and create a new app
- Note the **Client ID** and **Client Secret**
- OAuth is handled via device code flow — no redirect URI needed

---

## Environment Variables

Set these before starting Jellyfin:

| Variable | Required | Description |
|---|---|---|
| `JELLYFIN_RD_API_KEY` | Yes | Real-Debrid API key |
| `JELLYFIN_PROWLARR_URL` | Yes (search) | Prowlarr base URL, e.g. `http://localhost:9696` |
| `JELLYFIN_PROWLARR_API_KEY` | Yes (search) | Prowlarr API key |
| `JELLYFIN_TRAKT_CLIENT_ID` | No (Trakt features) | Trakt app client ID |
| `JELLYFIN_TRAKT_CLIENT_SECRET` | No (Trakt features) | Trakt app client secret |

Example:
```bash
export JELLYFIN_RD_API_KEY=your_rd_key
export JELLYFIN_PROWLARR_URL=http://localhost:9696
export JELLYFIN_PROWLARR_API_KEY=your_prowlarr_key
export JELLYFIN_TRAKT_CLIENT_ID=your_trakt_client_id
export JELLYFIN_TRAKT_CLIENT_SECRET=your_trakt_client_secret
```

---

## Trakt Token Storage

After a successful Trakt OAuth flow, the access token is persisted to:
```
{Jellyfin data directory}/streamhub-trakt.json
```

This file is written automatically and does not need to be created manually.

---

## Frontend

The StreamHub UI lives in `jellyfin-web` at:
```
src/apps/stable/routes/realdebrid/
src/apps/stable/features/streamhub/
```

It is accessed from the Jellyfin sidebar under **Find Movies & TV**.

---

## How Search Works

1. User types a query and selects Movies or TV
2. Prowlarr searches all configured indexers and returns torrent results
3. Results with info hashes are checked against Real-Debrid's instant availability API
4. Only RD-cached results are returned — guaranteed to stream immediately
5. Clicking **Stream** adds the torrent to Real-Debrid and opens a direct stream URL in a new tab
