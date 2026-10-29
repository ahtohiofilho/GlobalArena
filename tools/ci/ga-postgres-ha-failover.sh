#!/usr/bin/env bash
# Real two-node physical streaming replication + acknowledged CAS failover.
# Ephemeral CI lab only; not a production fencing manager.
set -Eeuo pipefail
suffix="$(date +%s)-$$"
network="ga-j3i-ha-$suffix"
primary="ga-j3i-primary-$suffix"
standby="ga-j3i-standby-$suffix"
password="ga_ci_ha_ephemeral_only"
replication_password="ga_ci_repl_ephemeral_only"
cleanup() {
  docker rm -f "$standby" "$primary" >/dev/null 2>&1 || true
  docker network rm "$network" >/dev/null 2>&1 || true
}
trap cleanup EXIT

docker network create "$network" >/dev/null
docker run -d --name "$primary" --hostname ga-primary --network "$network" \
  -p 55432:5432 -e POSTGRES_PASSWORD="$password" -e POSTGRES_DB=ga_ha \
  postgres:17-alpine -c wal_level=replica -c max_wal_senders=8 \
  -c max_replication_slots=8 >/dev/null
for attempt in $(seq 1 40); do
  if docker exec "$primary" pg_isready -h 127.0.0.1 -U postgres -d ga_ha >/dev/null 2>&1; then break; fi
  sleep 1
done
docker exec "$primary" pg_isready -h 127.0.0.1 -U postgres -d ga_ha
docker exec "$primary" psql -v ON_ERROR_STOP=1 -U postgres -d postgres \
  -c "CREATE ROLE replicator REPLICATION LOGIN PASSWORD '$replication_password'"
docker exec "$primary" sh -c 'echo "host replication replicator 0.0.0.0/0 scram-sha-256" >> "$PGDATA/pg_hba.conf"'
docker exec "$primary" psql -v ON_ERROR_STOP=1 -U postgres -d postgres \
  -c "SELECT pg_reload_conf()"

docker run -d --name "$standby" --hostname ga-standby --network "$network" \
  -p 55433:5432 --entrypoint sh postgres:17-alpine -c "sleep 3600" >/dev/null
docker exec -u postgres -e PGPASSWORD="$replication_password" "$standby" \
  pg_basebackup -h ga-primary -U replicator -D /tmp/ga-standby-pgdata \
  --wal-method=stream --write-recovery-conf --checkpoint=fast
docker exec -u postgres "$standby" \
  pg_ctl -D /tmp/ga-standby-pgdata -l /tmp/ga-standby.log \
  -o "-c listen_addresses='*' -c port=5432 -c hot_standby=on" -w start

for attempt in $(seq 1 40); do
  n="$(docker exec "$primary" psql -U postgres -d ga_ha -At -c \
    "SELECT count(*) FROM pg_stat_replication WHERE state='streaming'" || true)"
  if [[ "$n" == "1" ]]; then break; fi
  sleep 1
done
[[ "$n" == "1" ]] || { echo "Physical standby did not stream WAL"; exit 1; }
docker exec "$primary" psql -v ON_ERROR_STOP=1 -U postgres -d ga_ha \
  -c "ALTER SYSTEM SET synchronous_standby_names TO 'FIRST 1 (*)'"
docker exec "$primary" psql -v ON_ERROR_STOP=1 -U postgres -d ga_ha \
  -c "SELECT pg_reload_conf()"
for attempt in $(seq 1 40); do
  state="$(docker exec "$primary" psql -U postgres -d ga_ha -At -c \
    "SELECT sync_state FROM pg_stat_replication LIMIT 1" || true)"
  if [[ "$state" == "sync" ]]; then break; fi
  sleep 1
done
[[ "$state" == "sync" ]] || { echo "Standby not synchronous"; exit 1; }

docker exec -i "$primary" psql -U postgres -d ga_ha -v ON_ERROR_STOP=1 \
  < docs/engineering/sql/GA-G3J3g-trusted-anchor-postgres.sql
A="$(printf 'A%.0s' $(seq 1 64))"
B="$(printf 'B%.0s' $(seq 1 64))"
C="$(printf 'C%.0s' $(seq 1 64))"
Z="$(printf '0%.0s' $(seq 1 64))"
a0="{\"CheckpointSha256Hex\":\"$A\",\"JournalSha256Hex\":\"$Z\",\"Sequence\":0,\"Turn\":0}"
a1="{\"CheckpointSha256Hex\":\"$B\",\"JournalSha256Hex\":\"$C\",\"Sequence\":1,\"Turn\":1}"
before="{\"Confirmed\":$a0,\"Prepared\":null}"
pending="{\"Confirmed\":$a0,\"Prepared\":{\"Before\":$a0,\"After\":$a1}}"
after="{\"Confirmed\":$a1,\"Prepared\":null}"
docker exec -i "$primary" psql -v ON_ERROR_STOP=1 -v genesis="$before" \
  -U postgres -d ga_ha <<'SQL'
INSERT INTO public.ga_trusted_v9_anchor(game_id, snapshot)
VALUES ('ga-ha-game', :'genesis');
INSERT INTO public.ga_trusted_v9_game_principal(game_id, login_role)
VALUES ('ga-ha-game', session_user);
SQL
for transition in prepare finalize; do
  if [[ "$transition" == "prepare" ]]; then
    old="$before"; next="$pending"
  else
    old="$pending"; next="$after"
  fi
  result="$(docker exec -i "$primary" psql -v ON_ERROR_STOP=1 \
    -U postgres -d ga_ha -At -v old="$old" -v next="$next" <<'SQL'
BEGIN;
SET LOCAL synchronous_commit='remote_apply';
SELECT public.ga_v9_anchor_cas('ga-ha-game', :'old', :'next');
COMMIT;
SQL
)"
  printf '%s\n' "$result"
  grep -qx t <<< "$result" || { echo "CAS $transition failed"; exit 1; }
done

env GA_TEST_HA_CONNECTION="Host=localhost;Port=55433;Database=ga_ha;Username=postgres;Password=$password;SSL Mode=Disable;Pooling=false" \
  GA_TEST_HA_PHASE=standby \
  dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release \
    --no-restore --filter FullyQualifiedName~G3J3iPostgresHaIntegrationTests

# Hard-fence the old primary in this lab BEFORE promoting its standby.
docker stop -t 3 "$primary" >/dev/null
[[ "$(docker inspect --format '{{.State.Running}}' "$primary")" == "false" ]] ||
  { echo "Former primary not fenced"; exit 1; }
docker exec -u postgres "$standby" \
  pg_ctl -D /tmp/ga-standby-pgdata -w promote
for attempt in $(seq 1 40); do
  state="$(docker exec "$standby" psql -U postgres -d ga_ha -At \
    -c "SELECT pg_is_in_recovery()" || true)"
  if [[ "$state" == "f" ]]; then break; fi
  sleep 1
done
[[ "$state" == "f" ]] || { echo "Standby promotion failed"; exit 1; }

env GA_TEST_HA_CONNECTION="Host=localhost;Port=55433;Database=ga_ha;Username=postgres;Password=$password;SSL Mode=Disable;Pooling=false" \
  GA_TEST_HA_PHASE=promoted \
  dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release \
    --no-restore --filter FullyQualifiedName~G3J3iPostgresHaIntegrationTests
echo "Real PostgreSQL HA failover admission and acknowledged CAS: PASS"
