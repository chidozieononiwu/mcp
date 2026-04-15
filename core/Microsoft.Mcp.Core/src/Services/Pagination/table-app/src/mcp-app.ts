/**
 * @file MCP App for browsing paginated results in an interactive table.
 * Uses @modelcontextprotocol/ext-apps SDK for MCP Apps protocol compliance.
 */
import {
  App,
  applyDocumentTheme,
  applyHostStyleVariables,
  applyHostFonts,
  type McpUiHostContext,
} from "@modelcontextprotocol/ext-apps";
import type { CallToolResult } from "@modelcontextprotocol/ext-apps";
import "./mcp-app.css";

// ── DOM refs ──
const appRoot = document.getElementById("app-root")!;
const statusEl = document.getElementById("status")!;
const errorEl = document.getElementById("error")!;
const tableEl = document.getElementById("table-container")!;
const prevBtn = document.getElementById("btn-prev") as HTMLButtonElement;
const nextBtn = document.getElementById("btn-next") as HTMLButtonElement;
const pageInfoEl = document.getElementById("page-info")!;

// ── App state ──
interface PageEntry {
  items: Record<string, unknown>[];
  nextPage: string | null;
}

const pageCache: PageEntry[] = [];
let currentPage = -1;
let nextPageUri: string | null = null;

// ── Helpers ──
function escapeHtml(s: string): string {
  return s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

function showError(msg: string) {
  errorEl.textContent = msg;
  errorEl.style.display = "block";
  statusEl.textContent = "";
}

function updateControls() {
  prevBtn.disabled = currentPage <= 0;
  nextBtn.disabled = currentPage >= pageCache.length - 1 && !nextPageUri;
  pageInfoEl.textContent =
    pageCache.length > 0
      ? `Page ${currentPage + 1} of ${pageCache.length}${nextPageUri ? "+" : ""}`
      : "";
}

function renderTable(items: Record<string, unknown>[]) {
  if (!items || items.length === 0) {
    tableEl.innerHTML = "<p>No items.</p>";
    return;
  }
  const keys = Object.keys(items[0]);
  let html = "<table><thead><tr>";
  for (const k of keys) html += `<th>${escapeHtml(k)}</th>`;
  html += "</tr></thead><tbody>";
  for (const item of items) {
    html += "<tr>";
    for (const k of keys) {
      const v = item[k];
      const display = v === null || v === undefined ? "" : String(v);
      html += `<td title="${escapeHtml(display)}">${escapeHtml(display)}</td>`;
    }
    html += "</tr>";
  }
  html += "</tbody></table>";
  tableEl.innerHTML = html;
  statusEl.textContent = `${items.length} item(s)`;
}

function notifySize() {
  const height = appRoot.offsetHeight;
  app.sendSizeChanged({ height });
}

function showPage(index: number) {
  if (index < 0 || index >= pageCache.length) return;
  currentPage = index;
  renderTable(pageCache[index].items);
  nextPageUri = pageCache[index].nextPage;
  updateControls();
  notifySize();
}

async function fetchAndShowPage(uri: string) {
  statusEl.textContent = "Loading...";
  errorEl.style.display = "none";
  try {
    const result = await app.readServerResource({ uri });
    const contents = result.contents ?? [];
    if (contents.length === 0) {
      showError("Empty resource response.");
      notifySize();
      return;
    }
    const textContent = contents[0];
    const text = "text" in textContent ? (textContent as { text: string }).text : "";
    const data = JSON.parse(text);
    const items = data.items ?? [];
    const next: string | null = data.nextPage ?? null;
    pageCache.push({ items, nextPage: next });
    showPage(pageCache.length - 1);
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : String(e);
    showError("Failed to fetch page: " + msg);
    notifySize();
  }
}

// ── Extract pagedResourceUri from tool result ──
function extractPagedUri(result: CallToolResult): string | null {
  const content = result.content ?? [];
  for (const block of content) {
    if (block.type === "text" && "text" in block) {
      try {
        const parsed = JSON.parse(block.text);
        if (parsed.results?.pagedResourceUri) return parsed.results.pagedResourceUri;
        if (parsed.pagedResourceUri) return parsed.pagedResourceUri;
      } catch {
        // not JSON, skip
      }
    }
  }
  return null;
}

// ── Button handlers ──
prevBtn.addEventListener("click", () => {
  if (currentPage > 0) showPage(currentPage - 1);
});

nextBtn.addEventListener("click", () => {
  if (currentPage < pageCache.length - 1) {
    showPage(currentPage + 1);
  } else if (nextPageUri) {
    fetchAndShowPage(nextPageUri);
  }
});

// ── Host theming ──
function handleHostContextChanged(ctx: McpUiHostContext) {
  if (ctx.theme) {
    applyDocumentTheme(ctx.theme);
  }
  if (ctx.styles?.variables) {
    applyHostStyleVariables(ctx.styles.variables);
  }
  if (ctx.styles?.css?.fonts) {
    applyHostFonts(ctx.styles.css.fonts);
  }
}

// ── Create MCP App ──
const app = new App({ name: "table-app", version: "1.0.0" }, {}, { autoResize: false });

app.ontoolresult = (result: CallToolResult) => {
  // Debug: show raw tool result
  console.log("[table-app] ontoolresult raw:", JSON.stringify(result, null, 2));
  statusEl.textContent = `Received tool result (${result.content?.length ?? 0} content blocks)`;

  const uri = extractPagedUri(result);
  console.log("[table-app] extractPagedUri =>", uri);
  statusEl.textContent = `pagedResourceUri: ${uri ?? "(none)"}`;

  if (uri) {
    // Show the app and reset state for new tool result
    document.body.classList.add("active");
    appRoot.style.display = "";
    pageCache.length = 0;
    currentPage = -1;
    nextPageUri = null;
    fetchAndShowPage(uri);
  }
  // If no pagination URI, stay hidden — this result isn't paginated
};

app.onhostcontextchanged = handleHostContextChanged;

app.onerror = (err) => {
  console.error("MCP App error:", err);
};

// ── Connect to host ──
app.connect().then(() => {
  const ctx = app.getHostContext();
  if (ctx) {
    handleHostContextChanged(ctx);
  }
  // Start with zero height until paginated content arrives
  app.sendSizeChanged({ width: 0, height: 0 });
});
