import { nothing as n, html as i, css as m, state as l, customElement as v } from "@umbraco-cms/backoffice/external/lit";
import { UMB_AUTH_CONTEXT as _ } from "@umbraco-cms/backoffice/auth";
import { UmbModalBaseElement as g } from "@umbraco-cms/backoffice/modal";
import { R as y, s as h } from "./url-CSrW_WFR.js";
var b = Object.defineProperty, f = Object.getOwnPropertyDescriptor, c = (e, t, s, o) => {
  for (var r = o > 1 ? void 0 : o ? f(t, s) : t, u = e.length - 1, d; u >= 0; u--)
    (d = e[u]) && (r = (o ? d(t, s, r) : d(r)) || r);
  return o && r && b(t, s, r), r;
};
const p = 18;
let a = class extends g {
  constructor() {
    super(...arguments), this._query = "", this._selectedType = "", this._resourceTypes = [], this._items = [], this._total = 0, this._skip = 0, this._loading = !1, this._error = null, this._activeSummary = null, this._activeDetail = null, this._loadingDetail = !1;
  }
  connectedCallback() {
    super.connectedCallback(), this.consumeContext(_, (e) => {
      e && (this._api = new y(() => e.getLatestToken()), this._initialize());
    });
  }
  async _initialize() {
    var e, t;
    if (this._api) {
      this._selectedType = ((e = this.data) == null ? void 0 : e.allowedResourceTypes) ?? "";
      try {
        const s = await this._api.getResourceTypes();
        if ((t = this.data) != null && t.allowedResourceTypes) {
          const o = new Set(
            this.data.allowedResourceTypes.split(",").map((r) => r.trim()).filter(Boolean)
          );
          this._resourceTypes = s.filter((r) => o.has(r.id));
        } else
          this._resourceTypes = s;
      } catch {
      }
      await this._executeSearch(0);
    }
  }
  async _executeSearch(e) {
    var t;
    if (this._api) {
      this._loading = !0, this._error = null, this._skip = e;
      try {
        const s = await this._api.search({
          query: this._query,
          resourceTypes: this._selectedType || ((t = this.data) == null ? void 0 : t.allowedResourceTypes),
          skip: e,
          take: p
        });
        if (this._items = s.items, this._total = s.total, !this._activeSummary && s.items.length > 0) {
          const o = s.items.find((r) => {
            var u;
            return r.resourceId === ((u = this.data) == null ? void 0 : u.currentResourceId);
          }) ?? s.items[0];
          this._inspectItem(o);
        }
      } catch (s) {
        this._error = s instanceof Error ? s.message : "Unable to query ResourceSpace.", this._items = [], this._total = 0;
      } finally {
        this._loading = !1;
      }
    }
  }
  _onQueryInput(e) {
    const t = e.target;
    this._query = t.value, window.clearTimeout(this._searchDebounce), this._searchDebounce = window.setTimeout(() => {
      this._executeSearch(0);
    }, 300);
  }
  _onTypeFilterChange(e) {
    this._selectedType = e, this._executeSearch(0);
  }
  async _inspectItem(e) {
    if (this._activeSummary = e, this._activeDetail = null, !!this._api) {
      this._loadingDetail = !0;
      try {
        this._activeDetail = await this._api.getAsset(e.resourceId);
      } catch {
      } finally {
        this._loadingDetail = !1;
      }
    }
  }
  _confirmSelection() {
    var s;
    const e = this._activeSummary;
    if (!e) return;
    const t = this._activeDetail;
    this.value = {
      selection: {
        resourceId: e.resourceId,
        provider: e.provider || "resourcespace",
        title: (t == null ? void 0 : t.title) ?? e.title,
        altText: null,
        fileExtension: (t == null ? void 0 : t.fileExtension) ?? e.fileExtension,
        resourceType: (t == null ? void 0 : t.resourceType) ?? e.resourceType,
        width: (t == null ? void 0 : t.originalWidth) ?? e.width ?? null,
        height: (t == null ? void 0 : t.originalHeight) ?? e.height ?? null,
        credit: (t == null ? void 0 : t.credit) ?? e.credit ?? null,
        selectedAtUtc: (/* @__PURE__ */ new Date()).toISOString()
      }
    }, (s = this.modalContext) == null || s.submit();
  }
  render() {
    var e;
    return i`
      <umb-body-layout headline="Pick from ResourceSpace DAM">
        <div class="picker-shell">
          <div class="toolbar">
            <uui-input
              class="search-input"
              placeholder="Search keywords, titles, subjects, or ResourceSpace queries (e.g. mosaic)..."
              .value=${this._query}
              @input=${this._onQueryInput}>
              <uui-icon name="icon-search" slot="prepend"></uui-icon>
            </uui-input>

            ${this._resourceTypes.length > 0 ? i`
                  <div class="type-pills" role="group" aria-label="Filter by resource type">
                    ${(e = this.data) != null && e.allowedResourceTypes ? n : i`
                          <uui-button
                            compact
                            look=${this._selectedType === "" ? "primary" : "secondary"}
                            label="All"
                            @click=${() => this._onTypeFilterChange("")}></uui-button>
                        `}
                    ${this._resourceTypes.map(
      (t) => i`
                        <uui-button
                          compact
                          look=${this._selectedType === t.id ? "primary" : "secondary"}
                          label=${t.name}
                          @click=${() => this._onTypeFilterChange(t.id)}></uui-button>
                      `
    )}
                  </div>
                ` : n}
          </div>

          ${this._loading ? i`<uui-loader-bar></uui-loader-bar>` : n}

          ${this._error ? i`
                <uui-box class="error-box">
                  <strong>Could not reach ResourceSpace:</strong> ${this._error}
                </uui-box>
              ` : i`
                <div class="workspace-split">
                  <div class="results-pane">
                    ${this._items.length === 0 && !this._loading ? i`<div class="empty-results">No matching DAM assets found.</div>` : i`
                          <div class="asset-grid">
                            ${this._items.map((t) => this._renderCard(t))}
                          </div>
                        `}

                    ${this._total > p ? i`
                          <div class="pager">
                            <uui-button
                              compact
                              look="secondary"
                              label="Previous"
                              ?disabled=${this._skip === 0 || this._loading}
                              @click=${() => this._executeSearch(Math.max(0, this._skip - p))}></uui-button>
                            <span class="pager-status">
                              Showing ${this._skip + 1}–${Math.min(this._skip + this._items.length, this._total)} of ${this._total}
                            </span>
                            <uui-button
                              compact
                              look="secondary"
                              label="Next"
                              ?disabled=${this._skip + p >= this._total || this._loading}
                              @click=${() => this._executeSearch(this._skip + p)}></uui-button>
                          </div>
                        ` : n}
                  </div>

                  <aside class="inspector-pane">
                    ${this._renderInspector()}
                  </aside>
                </div>
              `}
        </div>

        <div slot="actions">
          <uui-button
            label="Cancel"
            look="secondary"
            @click=${() => {
      var t;
      return (t = this.modalContext) == null ? void 0 : t.reject();
    }}></uui-button>
          <uui-button
            label="Select Asset"
            look="primary"
            color="positive"
            ?disabled=${!this._activeSummary || this._activeDetail !== null && !this._activeDetail.isAvailable}
            @click=${this._confirmSelection}></uui-button>
        </div>
      </umb-body-layout>
    `;
  }
  _renderCard(e) {
    var o;
    const t = ((o = this._activeSummary) == null ? void 0 : o.resourceId) === e.resourceId, s = h(e.thumbnailUrl);
    return i`
      <button
        type="button"
        class="asset-card ${t ? "selected" : ""}"
        @click=${() => this._inspectItem(e)}
        @dblclick=${() => {
      this._inspectItem(e).then(() => this._confirmSelection());
    }}>
        <div class="thumb-frame">
          ${s ? i`<img src=${s} alt=${e.title} loading="lazy" referrerpolicy="no-referrer" />` : i`<uui-icon name="icon-document" class="fallback-icon"></uui-icon>`}
          <span class="ext-badge">${(e.fileExtension || "file").toUpperCase()}</span>
        </div>
        <div class="card-meta">
          <span class="card-title" title=${e.title}>${e.title}</span>
          <span class="card-sub">#${e.resourceId} · ${e.resourceType}</span>
        </div>
      </button>
    `;
  }
  _renderInspector() {
    if (!this._activeSummary)
      return i`<p class="inspector-placeholder">Select an asset to inspect metadata and renditions.</p>`;
    const e = this._activeDetail, t = h(
      (e == null ? void 0 : e.previewUrl) ?? this._activeSummary.previewUrl ?? this._activeSummary.thumbnailUrl
    ), s = e ? Object.values(e.renditions) : [], o = e ? Object.entries(e.metadata) : [];
    return i`
      <div class="inspector-content">
        <div class="inspector-preview">
          ${t ? i`<img src=${t} alt=${(e == null ? void 0 : e.title) ?? this._activeSummary.title} referrerpolicy="no-referrer" />` : i`<uui-icon name="icon-document" class="fallback-icon"></uui-icon>`}
        </div>

        <h4 class="inspector-title">${(e == null ? void 0 : e.title) ?? this._activeSummary.title}</h4>
        <div class="tag-row">
          <uui-tag look="secondary">Ref #${this._activeSummary.resourceId}</uui-tag>
          <uui-tag look="secondary">${(e == null ? void 0 : e.resourceType) ?? this._activeSummary.resourceType}</uui-tag>
          ${e != null && e.originalWidth && (e != null && e.originalHeight) ? i`<uui-tag look="outline">${e.originalWidth} × ${e.originalHeight}px</uui-tag>` : n}
        </div>

        ${this._loadingDetail ? i`<uui-loader-bar></uui-loader-bar>` : n}

        ${e != null && e.description ? i`<p class="inspector-desc">${e.description}</p>` : n}

        ${e != null && e.credit ? i`<p class="inspector-credit"><strong>Credit:</strong> ${e.credit}</p>` : n}

        ${s.length > 0 ? i`
              <div class="inspector-section">
                <h5>Available DAM Renditions</h5>
                <div class="tag-row">
                  ${s.map(
      (r) => i`
                      <uui-tag look="outline" title=${r.name}>
                        ${r.code}${r.width ? ` (${r.width}w)` : ""}
                      </uui-tag>
                    `
    )}
                </div>
              </div>
            ` : n}

        ${o.length > 0 ? i`
              <div class="inspector-section">
                <h5>DAM Metadata</h5>
                <dl class="meta-list">
                  ${o.map(
      ([r, u]) => i`
                      <dt>${r}</dt>
                      <dd>${u}</dd>
                    `
    )}
                </dl>
              </div>
            ` : n}
      </div>
    `;
  }
};
a.styles = m`
    .picker-shell {
      display: flex;
      flex-direction: column;
      gap: var(--uui-size-space-4);
      height: 100%;
    }

    .toolbar {
      display: flex;
      flex-direction: column;
      gap: var(--uui-size-space-3);
    }

    .search-input {
      width: 100%;
    }

    .type-pills {
      display: flex;
      flex-wrap: wrap;
      gap: var(--uui-size-space-2);
    }

    .workspace-split {
      display: grid;
      grid-template-columns: 1fr 310px;
      gap: var(--uui-size-space-5);
      align-items: start;
    }

    .asset-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
      gap: var(--uui-size-space-3);
    }

    .asset-card {
      appearance: none;
      background: var(--uui-color-surface);
      border: 2px solid var(--uui-color-border);
      border-radius: var(--uui-border-radius);
      padding: 0;
      cursor: pointer;
      text-align: left;
      overflow: hidden;
      transition: border-color 120ms ease, box-shadow 120ms ease;
    }

    .asset-card:hover {
      border-color: var(--uui-color-selected-emphasis);
    }

    .asset-card.selected {
      border-color: var(--uui-color-selected);
      box-shadow: 0 0 0 2px var(--uui-color-selected);
    }

    .thumb-frame {
      position: relative;
      aspect-ratio: 4 / 3;
      background: var(--uui-color-surface-alt);
      display: flex;
      align-items: center;
      justify-content: center;
      overflow: hidden;
    }

    .thumb-frame img {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }

    .fallback-icon {
      font-size: 2rem;
      color: var(--uui-color-text-alt);
    }

    .ext-badge {
      position: absolute;
      top: 6px;
      right: 6px;
      background: rgba(0, 0, 0, 0.72);
      color: #fff;
      font-size: 10px;
      font-weight: 700;
      padding: 2px 6px;
      border-radius: 3px;
    }

    .card-meta {
      padding: var(--uui-size-space-2) var(--uui-size-space-3);
      display: flex;
      flex-direction: column;
      gap: 2px;
    }

    .card-title {
      font-size: 13px;
      font-weight: 600;
      color: var(--uui-color-text);
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .card-sub {
      font-size: 11px;
      color: var(--uui-color-text-alt);
    }

    .pager {
      margin-top: var(--uui-size-space-4);
      display: flex;
      align-items: center;
      justify-content: space-between;
    }

    .pager-status {
      font-size: 12px;
      color: var(--uui-color-text-alt);
    }

    .inspector-pane {
      background: var(--uui-color-surface);
      border: 1px solid var(--uui-color-border);
      border-radius: var(--uui-border-radius);
      padding: var(--uui-size-space-4);
      position: sticky;
      top: 0;
    }

    .inspector-preview {
      aspect-ratio: 16 / 10;
      background: var(--uui-color-surface-alt);
      border-radius: var(--uui-border-radius);
      display: flex;
      align-items: center;
      justify-content: center;
      overflow: hidden;
      margin-bottom: var(--uui-size-space-3);
    }

    .inspector-preview img {
      max-width: 100%;
      max-height: 100%;
      object-fit: contain;
    }

    .inspector-title {
      margin: 0 0 var(--uui-size-space-2) 0;
      font-size: 15px;
    }

    .tag-row {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
      margin-bottom: var(--uui-size-space-3);
    }

    .inspector-desc,
    .inspector-credit {
      font-size: 12px;
      margin: 0 0 var(--uui-size-space-2) 0;
      color: var(--uui-color-text);
    }

    .inspector-section {
      border-top: 1px solid var(--uui-color-border);
      padding-top: var(--uui-size-space-3);
      margin-top: var(--uui-size-space-3);
    }

    .inspector-section h5 {
      margin: 0 0 var(--uui-size-space-2) 0;
      font-size: 12px;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      color: var(--uui-color-text-alt);
    }

    .meta-list {
      display: grid;
      grid-template-columns: auto 1fr;
      gap: 4px 10px;
      font-size: 12px;
      margin: 0;
    }

    .meta-list dt {
      font-weight: 600;
      color: var(--uui-color-text-alt);
    }

    .meta-list dd {
      margin: 0;
      word-break: break-word;
    }

    .empty-results,
    .inspector-placeholder {
      padding: var(--uui-size-space-5);
      text-align: center;
      color: var(--uui-color-text-alt);
    }

    .error-box {
      color: var(--uui-color-danger);
    }
  `;
c([
  l()
], a.prototype, "_query", 2);
c([
  l()
], a.prototype, "_selectedType", 2);
c([
  l()
], a.prototype, "_resourceTypes", 2);
c([
  l()
], a.prototype, "_items", 2);
c([
  l()
], a.prototype, "_total", 2);
c([
  l()
], a.prototype, "_skip", 2);
c([
  l()
], a.prototype, "_loading", 2);
c([
  l()
], a.prototype, "_error", 2);
c([
  l()
], a.prototype, "_activeSummary", 2);
c([
  l()
], a.prototype, "_activeDetail", 2);
c([
  l()
], a.prototype, "_loadingDetail", 2);
a = c([
  v("resourcespace-picker-modal")
], a);
const S = a;
export {
  a as ResourceSpacePickerModalElement,
  S as default
};
