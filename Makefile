DOTNET ?= dotnet
SOLUTION ?= Michi.slnx
TEST_PROJECT ?= tests/Michi.Tests/Michi.Tests.csproj
PACK_PROJECT ?= src/Michi/Michi.csproj
ARTIFACTS_DIR ?= ./artifacts
INSPECT_OUTPUT ?= /tmp/inspect.xml

.PHONY: help tools restore build test pack pack-all cleanup inspect verify

help: ## Show available targets
	@awk 'BEGIN {FS = ": ## "} /^[a-zA-Z0-9_.-]+: ## / {printf "%-24s %s\n", $$1, $$2}' $(MAKEFILE_LIST)

tools: ## Restore local dotnet tools
	$(DOTNET) tool restore

restore: ## Restore NuGet dependencies
	$(DOTNET) restore $(SOLUTION)

build: ## Build the solution in Release mode
	$(DOTNET) build $(SOLUTION) -c Release --nologo

test: ## Run the test project in Release mode
	$(MAKE) build
	$(DOTNET) test $(TEST_PROJECT) -c Release --no-build --nologo

pack: ## Pack the Michi project into ./artifacts
	$(MAKE) build
	$(DOTNET) pack $(PACK_PROJECT) -c Release --no-build --nologo --output $(ARTIFACTS_DIR)

pack-all: ## Pack all packable projects into ./artifacts
	$(MAKE) build
	$(DOTNET) pack $(SOLUTION) -c Release --no-build --nologo --output $(ARTIFACTS_DIR)

cleanup: ## Run JetBrains cleanupcode
	$(MAKE) tools
	$(DOTNET) tool run jb -- cleanupcode $(SOLUTION) --profile="Built-in: Full Cleanup"

inspect: ## Run inspectcode and require zero issues
	$(MAKE) tools
	$(MAKE) build
	$(DOTNET) tool run jb -- inspectcode $(SOLUTION) --output=$(INSPECT_OUTPUT) --format=Xml --severity=WARNING --no-build
	@count=$$(grep -c '<Issue ' $(INSPECT_OUTPUT) || true); \
	if [ "$$count" -ne 0 ]; then \
		printf 'inspectcode reported %s issue(s); see %s\n' "$$count" "$(INSPECT_OUTPUT)"; \
		exit 1; \
	fi

verify: ## Run the full local verification flow
	$(MAKE) tools
	$(MAKE) test
	$(MAKE) cleanup
	$(MAKE) test
	$(MAKE) pack
	$(MAKE) inspect
