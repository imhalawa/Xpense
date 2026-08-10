# Xpense — one entrypoint for the api stack and the web app.
#
#   make start          api stack detached, then the web dev server in the foreground
#   make start api      postgres, migrations, api, notifications; reuses a running stack
#   make start web      vite dev server
#   make start dev      db in a container, API + web on the host, both hot-reloading
#   make stop [api]     docker compose down
#   make logs [api]     follow container logs
#   make build [api|web]
#   make test           dotnet test (Docker must be running — Testcontainers)
#
# ponytail: the second word is read off MAKECMDGOALS and swallowed by the no-op
# `api`/`web` targets at the bottom. Plain `make start-web` also works.

API     := api
WEB     := web
COMPOSE := docker compose -f $(API)/docker-compose.yml

TARGET := $(word 2,$(MAKECMDGOALS))
PICK    = $(if $(TARGET),$(TARGET),all)

.DEFAULT_GOAL := help
API_PORT := 4000
WEB_PORT := 5173

.PHONY: help start stop logs build test api web dev free-ports \
        start-all start-api start-web start-dev stop-all stop-api stop-web \
        logs-all logs-api logs-web build-all build-api build-web

help:
	@echo "make start [api|web]   start everything, or just one side"
	@echo "make start dev         db in docker, API + web on the host, hot reload"
	@echo "make stop  [api]       stop the containers"
	@echo "make logs  [api]       follow container logs"
	@echo "make build [api|web]   build images / production bundle"
	@echo "make test              run the .NET test suite"

# ponytail: `dotnet watch` and vite both spawn the real server as a grandchild, and a
# ctrl-C does not always reap it. A leftover on 4000 aborts the API with "address already
# in use"; a leftover on 5173 silently pushes vite to 5174, which breaks the API's CORS
# origin and PublicUrl. Free both before starting. Docker's listeners are skipped —
# the api container is stopped through compose, not killed.
free-ports:
	@for port in $(API_PORT) $(WEB_PORT); do \
	  for pid in $$(lsof -tnP -iTCP:$$port -sTCP:LISTEN 2>/dev/null); do \
	    case "$$(ps -o comm= -p $$pid 2>/dev/null)" in *[dD]ocker*) continue;; esac; \
	    echo "freeing port $$port (pid $$pid left over from an earlier run)"; \
	    kill $$pid 2>/dev/null || true; \
	  done; \
	done

start: start-$(PICK)
stop:  stop-$(PICK)
logs:  logs-$(PICK)
build: build-$(PICK)

start-all: start-api start-web

# ponytail: reuse whatever is already up so the postgres container (and its data)
# stays the same instance. only a cold stack gets --build.
start-api:
	@[ -f $(API)/.env ] || cp $(API)/.env.example $(API)/.env
	@if [ -n "$$($(COMPOSE) ps -q --status running)" ]; then \
	  echo 'api stack already running — reusing it, no rebuild. `make stop api` first to rebuild.'; \
	  $(COMPOSE) up -d; \
	else \
	  $(COMPOSE) up -d --build; \
	fi

start-web:
	@[ -d $(WEB)/node_modules ] || (cd $(WEB) && npm install)
	cd $(WEB) && npm run dev

# both on the host so `dotnet watch` and vite HMR both apply; ctrl-C stops the pair
start-dev:
	@[ -f $(API)/.env ] || cp $(API)/.env.example $(API)/.env
	@[ -d $(WEB)/node_modules ] || (cd $(WEB) && npm install)
	$(COMPOSE) up -d --build migrations
	$(COMPOSE) stop api
	@$(MAKE) --no-print-directory free-ports
	@PW=$$(grep -E '^POSTGRES_PASSWORD=' $(API)/.env | cut -d= -f2-); \
	export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=xpense;Username=xpense;Password=$$PW"; \
	trap 'kill 0' INT TERM; \
	(cd $(API)/src/Xpense && dotnet watch run --project Xpense.API) & \
	(cd $(WEB) && npm run dev) & \
	wait

stop-all: stop-api
stop-api:
	$(COMPOSE) down
stop-web:
	@echo "web runs in the foreground — ctrl-C it"

logs-all: logs-api
logs-api:
	$(COMPOSE) logs -f
logs-web:
	@echo "web logs to the terminal it runs in"

build-all: build-api build-web
build-api:
	$(COMPOSE) build
build-web:
	cd $(WEB) && npm run build

test:
	dotnet test $(API)/src/Xpense/Xpense.sln

# swallow the second word so `make start web` doesn't try to build a `web` target
api web dev:
	@:
