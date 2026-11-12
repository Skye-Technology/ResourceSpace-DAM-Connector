import {
  css,
  customElement,
  html,
  nothing,
  property,
  state,
} from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { UMB_MODAL_MANAGER_CONTEXT } from '@umbraco-cms/backoffice/modal';
import { UmbChangeEvent } from '@umbraco-cms/backoffice/event';
import type {
  UmbPropertyEditorConfigCollection,
  UmbPropertyEditorUiElement,
} from '@umbraco-cms/backoffice/property-editor';
import { ResourceSpaceApiClient } from '../api/resourcespace-api.client.js';
import { RESOURCESPACE_PICKER_MODAL } from '../modal/resourcespace-picker-modal.token.js';
import type { DamAssetDetail, DamAssetReference } from '../types.js';
import { sanitizeAssetUrl } from '../utils/url.js';

@customElement('resourcespace-asset-picker')
export class ResourceSpaceAssetPickerElement
  extends UmbLitElement
  implements UmbPropertyEditorUiElement
{
  private _api?: ResourceSpaceApiClient;
  private _value?: DamAssetReference;

  @property({ type: Object })
  public set value(val: DamAssetReference | undefined) {
    const prevId = this._value?.resourceId;
    this._value = val;
    if (val?.resourceId && val.resourceId !== prevId) {
      void this._loadLiveDetail(val.resourceId);
    } else if (!val) {
      this._liveDetail = null;
    }
  }
  public get value(): DamAssetReference | undefined {
    return this._value;
  }

  @property({ type: Boolean, reflect: true })
  public readonly = false;

  @state()
  private _allowedResourceTypes = '';

  @property({ attribute: false })
  public set config(config: UmbPropertyEditorConfigCollection | undefined) {
    this._allowedResourceTypes =
      config?.getValueByAlias<string>('allowedResourceTypes') ?? '';
  }

  @state()
  private _liveDetail: DamAssetDetail | null = null;

  @state()
  private _loadingLive = false;

  override connectedCallback(): void {
    super.connectedCallback();

    this.consumeContext(UMB_AUTH_CONTEXT, (authContext) => {
      if (!authContext) return;
      this._api = new ResourceSpaceApiClient(() => authContext.getLatestToken());
      if (this._value?.resourceId) {
        void this._loadLiveDetail(this._value.resourceId);
      }
    });
  }

  private async _loadLiveDetail(resourceId: string, forceRefresh = false): Promise<void> {
    if (!this._api) return;
    this._loadingLive = true;
    try {
      this._liveDetail = forceRefresh
        ? await this._api.refreshAsset(resourceId)
        : await this._api.getAsset(resourceId);
    } catch {
      this._liveDetail = null;
    } finally {
      this._loadingLive = false;
    }
  }

  private async _openPicker(): Promise<void> {
    if (this.readonly) return;

    const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
    if (!modalManager) return;

    const modal = modalManager.open(this, RESOURCESPACE_PICKER_MODAL, {
      data: {
        allowedResourceTypes: this._allowedResourceTypes,
        currentResourceId: this._value?.resourceId,
      },
    });

    try {
      const result = await modal.onSubmit();
      if (result?.selection) {
        // Preserve any editorial alt text if re-picking the same resource
        const preservedAlt =
          result.selection.resourceId === this._value?.resourceId
            ? this._value?.altText
            : null;

        this._updateValue({
          ...result.selection,
          altText: preservedAlt ?? null,
        });
      }
    } catch {
      // Modal dismissed without selection
    }
  }

  private _onAltTextChange(e: Event): void {
    if (!this._value || this.readonly) return;
    const input = e.target as HTMLInputElement;
    this._updateValue({
      ...this._value,
      altText: input.value.trim() ? input.value : null,
    });
  }

  private _clearSelection(): void {
    if (this.readonly) return;
    this._updateValue(undefined);
  }

  private _updateValue(next: DamAssetReference | undefined): void {
    this.value = next;
    this.dispatchEvent(new UmbChangeEvent());
  }

  override render() {
    if (!this._value?.resourceId) {
      return html`
        <div class="empty-picker">
          <uui-button
            look="placeholder"
            label="Pick from ResourceSpace DAM"
            ?disabled=${this.readonly}
            @click=${this._openPicker}>
            <uui-icon name="icon-picture"></uui-icon>
            <span>Browse ResourceSpace DAM</span>
          </uui-button>
        </div>
      `;
    }

    const detail = this._liveDetail;
    const isUnavailable = detail !== null && !detail.isAvailable;
    const thumb = sanitizeAssetUrl(detail?.previewUrl ?? detail?.thumbnailUrl);
    const title = (detail?.isAvailable ? detail.title : this._value.title) ?? `Resource #${this._value.resourceId}`;
    const defaultAlt = detail?.defaultAltText ?? title;

    return html`
      <div class="selected-card ${isUnavailable ? 'unavailable' : ''}">
        ${this._loadingLive ? html`<uui-loader-bar></uui-loader-bar>` : nothing}

        <div class="card-body">
          <div class="preview-box">
            ${thumb
              ? html`<img src=${thumb} alt=${title} referrerpolicy="no-referrer" />`
              : html`<uui-icon name="icon-document" class="doc-icon"></uui-icon>`}
          </div>

          <div class="info-box">
            <div class="header-row">
              <strong class="asset-title">${title}</strong>
              <div class="badges">
                <uui-tag look="secondary">Ref #${this._value.resourceId}</uui-tag>
                ${this._value.fileExtension
                  ? html`<uui-tag look="outline">${this._value.fileExtension.toUpperCase()}</uui-tag>`
                  : nothing}
                ${isUnavailable
                  ? html`<uui-tag color="warning">Unavailable in DAM</uui-tag>`
                  : html`<uui-tag color="positive" look="outline">Live in DAM</uui-tag>`}
              </div>
            </div>

            ${isUnavailable
              ? html`
                  <div class="warning-banner">
                    ${detail?.unavailableReason ?? 'This asset was deleted, archived, or restricted in ResourceSpace.'}
                  </div>
                `
              : nothing}

            <label class="alt-field">
              <span>Editorial alt text override (optional — defaults to DAM caption)</span>
              <uui-input
                label="Editorial alt text override"
                placeholder=${defaultAlt}
                .value=${this._value.altText ?? ''}
                ?readonly=${this.readonly}
                @change=${this._onAltTextChange}></uui-input>
            </label>

            <div class="actions-row">
              <uui-button
                compact
                look="secondary"
                label="Replace"
                ?disabled=${this.readonly}
                @click=${this._openPicker}></uui-button>
              <uui-button
                compact
                look="secondary"
                label="Refresh DAM status"
                @click=${() => this._loadLiveDetail(this._value!.resourceId, true)}></uui-button>
              <uui-button
                compact
                look="secondary"
                color="danger"
                label="Remove"
                ?disabled=${this.readonly}
                @click=${this._clearSelection}></uui-button>
            </div>
          </div>
        </div>
      </div>
    `;
  }

  static override styles = css`
    .empty-picker uui-button {
      width: 100%;
      min-height: 84px;
    }

    .selected-card {
      border: 1px solid var(--uui-color-border);
      border-radius: var(--uui-border-radius);
      background: var(--uui-color-surface);
      padding: var(--uui-size-space-4);
    }

    .selected-card.unavailable {
      border-color: var(--uui-color-warning);
    }

    .card-body {
      display: grid;
      grid-template-columns: 160px 1fr;
      gap: var(--uui-size-space-4);
      align-items: start;
    }

    .preview-box {
      aspect-ratio: 4 / 3;
      background: var(--uui-color-surface-alt);
      border-radius: var(--uui-border-radius);
      display: flex;
      align-items: center;
      justify-content: center;
      overflow: hidden;
    }

    .preview-box img {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }

    .doc-icon {
      font-size: 2.2rem;
      color: var(--uui-color-text-alt);
    }

    .info-box {
      display: flex;
      flex-direction: column;
      gap: var(--uui-size-space-3);
    }

    .header-row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: var(--uui-size-space-2);
    }

    .asset-title {
      font-size: 14px;
    }

    .badges {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
    }

    .warning-banner {
      font-size: 12px;
      padding: var(--uui-size-space-2) var(--uui-size-space-3);
      background: #fff7ed;
      border: 1px solid #fdba74;
      color: #9a3412;
      border-radius: var(--uui-border-radius);
    }

    .alt-field {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }

    .alt-field span {
      font-size: 12px;
      color: var(--uui-color-text-alt);
    }

    .alt-field uui-input {
      width: 100%;
    }

    .actions-row {
      display: flex;
      flex-wrap: wrap;
      gap: var(--uui-size-space-2);
    }
  `;
}

export default ResourceSpaceAssetPickerElement;

declare global {
  interface HTMLElementTagNameMap {
    'resourcespace-asset-picker': ResourceSpaceAssetPickerElement;
  }
}
