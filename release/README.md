# DocumentDispatchService Release

## Docker (Recommended)

1) Copy .env.example to .env

2) Start the stack:

docker compose -f release/docker-compose.release.yml up -d

3) Verify API:
- http://localhost:8080
- http://localhost:8080/ops
- http://localhost:8080/metrics

Stop:
docker compose -f release/docker-compose.release.yml down

Reset DB volume (destructive):
docker compose -f release/docker-compose.release.yml down -v


## Windows Self-Contained Zip (Alternative)

1) Download the latest Windows zip from GitHub Releases.

2) Ensure PostgreSQL is reachable (local install or remote DB).

3) Set ConnectionStrings__DefaultConnection and run the exe.
