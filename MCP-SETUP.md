# Unity MCP setup

This workspace is configured for MCP for Unity and Codex.

## One-time setup

1. Open `My project` in Unity `6000.6.1f1`.
2. Let Package Manager resolve the Git dependency in `My project/Packages/manifest.json`.
3. Open **Window → MCP for Unity**.
4. Let the setup wizard install or locate Python 3.10+ and `uv` if prompted.
5. Start the server with **HTTP** transport. The default endpoint is `http://localhost:8080/mcp`.
6. Open this workspace in Codex and mark it as trusted if prompted. Project-local MCP configuration is only loaded for trusted projects.
7. Restart Codex or reload its MCP connections, then run `/mcp` to confirm that the `unity` server is listed.

With Unity open and the MCP window showing Connected, ask Codex:

> Read the Unity console and summarize current errors and warnings.

The bridge is local-only by default and does not need an API key. Keep Unity running while using Unity tools. If it is not reachable, restart it from **Window → MCP for Unity** and verify that port 8080 is available.
