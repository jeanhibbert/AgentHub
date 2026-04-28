# AgentHub

Local trading sandbox built on .NET Aspire with:
- Commodities trading API and worker
- Interest-rate derivatives API and worker
- Correlation worker using local Ollama
- MCP HTTP endpoints for both trading systems
- Blazor dashboard for Scenario 1 bootstrap and macro narrative queries

## Primary UI

After startup, open the Blazor dashboard at http://localhost:17020 and go to the Macro Narrative page. The default query is:

`Is there a coherent macro narrative that explains current positions across both trading books?`

Use `Inject Scenario 1` and then `Run Narrative Query` to test the local LLM against the forced oil shock to swap repricing scenario.

## Recommended startup flow

Use the provided scripts from the repository root:

```powershell
.\scripts\start-sandbox.ps1 -NoBuild
```

That script:
- terminates stale `AgentHub.AppHost` processes
- removes old sandbox containers
- starts the Aspire host cleanly

To stop and clean the sandbox:

```powershell
.\scripts\stop-sandbox.ps1
```

To run the scripted end-to-end query path after startup:

```powershell
.\scripts\run-scenario-1-smoke-test.ps1
```

## Notes

- The dashboard is exposed on http://localhost:17020.
- Commodities API is exposed on http://localhost:17011.
- Rates API is exposed on http://localhost:17012.
- Ollama is exposed on http://localhost:11434.
- If ports are already in use, run `./scripts/stop-sandbox.ps1` first and start again.