# SB03 Proof Manifest

## Scope

Docker rebuild, browser validation, and screenshots.

## Commands

- `docker compose build economy-node accountapp-alice accountapp-bob accountapp-lab demo-seeder`
- `docker compose up -d economy-node accountapp-alice accountapp-bob accountapp-lab`
- `docker compose build accountapp-alice accountapp-bob accountapp-lab`
- `docker compose up -d accountapp-alice accountapp-bob accountapp-lab`
- `docker compose ps`

## Final Service Status

- `accountapp-alice`: up on `0.0.0.0:5081->80/tcp`
- `accountapp-bob`: up on `0.0.0.0:5082->80/tcp`
- `accountapp-lab`: up on `0.0.0.0:5083->80/tcp`
- `economy-node`: up on `0.0.0.0:8080->8080/tcp`
- `postgres`: healthy on `0.0.0.0:54329->5432/tcp`

## Screenshot Artifacts

- `output/playwright/docker-dataitems-gallery-visible.png`
- `output/playwright/docker-dataitems-details-visible.png`
- `output/playwright/docker-message-image-attachment-visible.png`
