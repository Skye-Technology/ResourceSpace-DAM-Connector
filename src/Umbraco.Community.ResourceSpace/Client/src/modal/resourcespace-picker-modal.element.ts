import {
  css,
  customElement,
  html,
  nothing,
  state,
} from '@umbraco-cms/backoffice/external/lit';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { UmbModalBaseElement } from '@umbraco-cms/backoffice/modal';
import { ResourceSpaceApiClient } from '../api/resourcespace-api.client.js';
import type {
  DamAssetDetail,
  DamAssetSummary,
  DamResourceTypeOption,
} from '../types.js';
import type {
  ResourceSpacePickerModalData,
  ResourceSpacePickerModalValue,
} from './resourcespace-picker-modal.token.js';

const PAGE_SIZE = 18;

@customElement('resourcespace-picker-modal')
export class ResourceSpacePickerModalElement extends UmbModalBaseElement<
  ResourceSpacePickerModalData,
  ResourceSpacePickerModalValue
> {
  private _api?: ResourceSpaceApiClient;
  private _searchDebounce?: number;

  @state()
  private _query = '';

  @state()
  private _selectedType = '';

  @state()
  private _resourceTypes: DamResourceTypeOption[] = [];

  @state()
  private _items: DamAssetSummary[] = [];

  @state()
  private _total = 0;

  @state()
  private _skip = 0;

  @state()
  private _loading = false;

  @state()
  private _error: string | null = null;

  @state()
  private _activeSummary: DamAssetSummary | null = null;

  @state()
  private _activeDetail: DamAssetDetail | null = null;

  @state()
  private _loadingDetail = false;

  override connectedCallback(): void {
    super.connectedCallback();

    this.consumeContext(UMB_AUTH_CONTEXT, (authContext) => {
      if (!authContext) return;
      this._api = new ResourceSpaceApiClient(() => authContext.getLatestToken());
      void this._initialize();
    });
  }

  private async _initialize(): Promise<void> {
    if (!this._api) return;
    this._selectedType = this.data?.allowedResourceTypes ?? '';

    try {
      const allTypes = await this._api.getResourceTypes();
      if (this.data?.allowedResourceTypes) {
        const allowed = new Set(
          this.data.allowedResourceTypes.split(',').map((s) => s.trim()).filter(Boolean)
        );
        this._resourceTypes = allTypes.filter((t) => allowed.has(t.id));
      } else {
        this._resourceTypes = allTypes;
      }
    } catch {
      // Non-fatal: search still works even if resource type lookup fails
    }

    await this._executeSearch(0);
  }

  private async _executeSearch(skip: number): Promise<void> {
    if (!this._api) return;
    this._loading = true;
    this._error = null;
    this._skip = skip;

    try {
      const result = await this._api.search({
        query: this._query,
        resourceTypes: this._selectedType || this.data?.allowedResourceTypes,
        skip,
        take: PAGE_SIZE,
      });
      this._items = result.items;
      this._total = result.total;

      if (!this._activeSummary && result.items.length > 0) {
        const preselected =
          result.items.find((x) => x.resourceId === this.data?.currentResourceId) ??
          result.items[0];
        void this._inspectItem(preselected);
      }
    } catch (err) {
      this._error = err instanceof Error ? err.message : 'Unable to query ResourceSpace.';
      this._items = [];
      this._total = 0;
    } finally {
      this._loading = false;
    }
  }

  private _onQueryInput(e: Event): void {
    const target = e.target as HTMLInputElement;
    this._query = target.value;
    window.clearTimeout(this._searchDebounce);
    this._searchDebounce = window.setTimeout(() => {
      void this._executeSearch(0);
    }, 300);
  }

  private _onTypeFilterChange(typeId: string): void {
    this._selectedType = typeId;
    void this._executeSearch(0);
  }

  private async _inspectItem(item: DamAssetSummary): Promise<void> {
    this._activeSummary = item;
    this._activeDetail = null;
    if (!this._api) return;

    this._loadingDetail = true;
    try {
      this._activeDetail = await this._api.getAsset(item.resourceId);
    } catch {
      // Fall back to summary metadata if detail fetch fails
    } finally {
      this._loadingDetail = false;
    }
  }

  private _confirmSelection(): void {
    const summary = this._activeSummary;
    if (!summary) return;
    const detail = this._activeDetail;

    this.value = {
      selection: {
        resourceId: summary.resourceId,
        provider: summary.provider || 'resourcespace',
        title: detail?.title ?? summary.title,
        altText: null,
        fileExtension: detail?.fileExtension ?? summary.fileExtension,
        resourceType: detail?.resourceType ?? summary.resourceType,
        width: detail?.originalWidth ?? summary.width ?? null,
        height: detail?.originalHeight ?? summary.height ?? null,
        credit: detail?.credit ?? summary.credit ?? null,
        selectedAtUtc: new Date().toISOString(),
      },
    };
    this.modalContext?.submit();
  }

  override render() {
    return html`
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

            ${this._resourceTypes.length > 0
              ? html`
                  <div class="type-pills" role="group" aria-label="Filter by resource type">
                    ${!this.data?.allowedResourceTypes
                      ? html`
                          <uui-button
                            compact
                            look=${this._selectedType === '' ? 'primary' : 'secondary'}
                            label="All"
                            @click=${() => this._onTypeFilterChange('')}></uui-button>
                        `
                      : nothing}
                    ${this._resourceTypes.map(
                      (t) => html`
                        <uui-button
                          compact
                          look=${this._selectedType === t.id ? 'primary' : 'secondary'}
                          label=${t.name}
                          @click=${() => this._onTypeFilterChange(t.id)}></uui-button>
                      `
                    )}
                  </div>
                `
              : nothing}
          </div>

          ${this._loading ? html`<uui-loader-bar></uui-loader-bar>` : nothing}

          ${this._error
            ? html`
                <uui-box class="error-box">
                  <strong>Could not reach ResourceSpace:</strong> ${this._error}
                </uui-box>
              `
            : html`
                <div class="workspace-split">
                  <div class="results-pane">
                    ${this._items.length === 0 && !this._loading
                      ? html`<div class="empty-results">No matching DAM assets found.</div>`
                      : html`
                          <div class="asset-grid">
                            ${this._items.map((item) => this._renderCard(item))}
                          </div>
                        `}

                    ${this._total > PAGE_SIZE
                      ? html`
                          <div class="pager">
                            <uui-button
                              compact
                              look="secondary"
                              label="Previous"
                              ?disabled=${this._skip === 0 || this._loading}
                              @click=${() => this._executeSearch(Math.max(0, this._skip - PAGE_SIZE))}></uui-button>
                            <span class="pager-status">
                              Showing ${this._skip + 1}–${Math.min(this._skip + this._items.length, this._total)} of ${this._total}
                            </span>
                            <uui-button
                              compact
                              look="secondary"
                              label="Next"
                              ?disabled=${this._skip + PAGE_SIZE >= this._total || this._loading}
                              @click=${() => this._executeSearch(this._skip + PAGE_SIZE)}></uui-button>
                          </div>
                        `
                      : nothing}
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
            @click=${() => this.modalContext?.reject()}></uui-button>
          <uui-button
            label="Select Asset"
            look="primary"
            color="positive"
            ?disabled=${!this._activeSummary || (this._activeDetail !== null && !this._activeDetail.isAvailable)}
            @click=${this._confirmSelection}></uui-button>
        </div>
      </umb-body-layout>
    `;
  }

  private _renderCard(item: DamAssetSummary) {
    const isSelected = this._activeSummary?.resourceId === item.resourceId;
    return html`
      <button
        type="button"
        class="asset-card ${isSelected ? 'selected' : ''}"
        @click=${() => this._inspectItem(item)}
        @dblclick=${() => {
          void this._inspectItem(item).then(() => this._confirmSelection());
        }}>
        <div class="thumb-frame">
          ${item.thumbnailUrl
            ? html`<img src=${item.thumbnailUrl} alt=${item.title} loading="lazy" />`
            : html`<uui-icon name="icon-document" class="fallback-icon"></uui-icon>`}
          <span class="ext-badge">${(item.fileExtension || 'file').toUpperCase()}</span>
        </div>
        <div class="card-meta">
          <span class="card-title" title=${item.title}>${item.title}</span>
          <span class="card-sub">#${item.resourceId} · ${item.resourceType}</span>
        </div>
      </button>
    `;
  }

  private _renderInspector() {
    if (!this._activeSummary) {
      return html`<p class="inspector-placeholder">Select an asset to inspect metadata and renditions.</p>`;
    }

    const detail = this._activeDetail;
    const previewUrl = detail?.previewUrl ?? this._activeSummary.previewUrl ?? this._activeSummary.thumbnailUrl;
    const renditions = detail ? Object.values(detail.renditions) : [];
    const metadataEntries = detail ? Object.entries(detail.metadata) : [];

    return html`
      <div class="inspector-content">
        <div class="inspector-preview">
          ${previewUrl
            ? html`<img src=${previewUrl} alt=${detail?.title ?? this._activeSummary.title} />`
            : html`<uui-icon name="icon-document" class="fallback-icon"></uui-icon>`}
        </div>

        <h4 class="inspector-title">${detail?.title ?? this._activeSummary.title}</h4>
        <div class="tag-row">
          <uui-tag look="secondary">Ref #${this._activeSummary.resourceId}</uui-tag>
          <uui-tag look="secondary">${detail?.resourceType ?? this._activeSummary.resourceType}</uui-tag>
          ${detail?.originalWidth && detail?.originalHeight
            ? html`<uui-tag look="outline">${detail.originalWidth} × ${detail.originalHeight}px</uui-tag>`
            : nothing}
        </div>

        ${this._loadingDetail ? html`<uui-loader-bar></uui-loader-bar>` : nothing}

        ${detail?.description
          ? html`<p class="inspector-desc">${detail.description}</p>`
          : nothing}

        ${detail?.credit
          ? html`<p class="inspector-credit"><strong>Credit:</strong> ${detail.credit}</p>`
          : nothing}

        ${renditions.length > 0
          ? html`
              <div class="inspector-section">
                <h5>Available DAM Renditions</h5>
                <div class="tag-row">
                  ${renditions.map(
                    (r) => html`
                      <uui-tag look="outline" title=${r.name}>
                        ${r.code}${r.width ? ` (${r.width}w)` : ''}
                      </uui-tag>
                    `
                  )}
                </div>
              </div>
            `
          : nothing}

        ${metadataEntries.length > 0
          ? html`
              <div class="inspector-section">
                <h5>DAM Metadata</h5>
                <dl class="meta-list">
                  ${metadataEntries.map(
                    ([key, val]) => html`
                      <dt>${key}</dt>
                      <dd>${val}</dd>
                    `
                  )}
                </dl>
              </div>
            `
          : nothing}
      </div>
    `;
  }

  static override styles = css`
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
}

export default ResourceSpacePickerModalElement;

declare global {
  interface HTMLElementTagNameMap {
    'resourcespace-picker-modal': ResourceSpacePickerModalElement;
  }
}
