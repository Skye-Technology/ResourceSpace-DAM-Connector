import { html as o, nothing as c, css as v, property as d, state as p, customElement as h } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as f } from "@umbraco-cms/backoffice/lit-element";
import { UMB_AUTH_CONTEXT as b } from "@umbraco-cms/backoffice/auth";
import { UmbModalToken as _, UMB_MODAL_MANAGER_CONTEXT as g } from "@umbraco-cms/backoffice/modal";
import { UmbChangeEvent as m } from "@umbraco-cms/backoffice/event";
import { R as y, s as w } from "./url-CSrW_WFR.js";
const x = new _("ResourceSpace.Modal.AssetPicker", {
  modal: {
    type: "sidebar",
    size: "large"
  }
});
var A = Object.defineProperty, R = Object.getOwnPropertyDescriptor, u = (e, i, t, s) => {
  for (var a = s > 1 ? void 0 : s ? R(i, t) : i, r = e.length - 1, n; r >= 0; r--)
    (n = e[r]) && (a = (s ? n(i, t, a) : n(a)) || a);
  return s && a && A(i, t, a), a;
};
let l = class extends f {
  constructor() {
    super(...arguments), this.readonly = !1, this._allowedResourceTypes = "", this._liveDetail = null, this._loadingLive = !1;
  }
  set value(e) {
    var t;
    const i = (t = this._value) == null ? void 0 : t.resourceId;
    this._value = e, e != null && e.resourceId && e.resourceId !== i ? this._loadLiveDetail(e.resourceId) : e || (this._liveDetail = null);
  }
  get value() {
    return this._value;
  }
  set config(e) {
    this._allowedResourceTypes = (e == null ? void 0 : e.getValueByAlias("allowedResourceTypes")) ?? "";
  }
  connectedCallback() {
    super.connectedCallback(), this.consumeContext(b, (e) => {
      var i;
      e && (this._api = new y(() => e.getLatestToken()), (i = this._value) != null && i.resourceId && this._loadLiveDetail(this._value.resourceId));
    });
  }
  async _loadLiveDetail(e, i = !1) {
    if (this._api) {
      this._loadingLive = !0;
      try {
        this._liveDetail = i ? await this._api.refreshAsset(e) : await this._api.getAsset(e);
      } catch {
        this._liveDetail = null;
      } finally {
        this._loadingLive = !1;
      }
    }
  }
  async _openPicker() {
    var t, s, a;
    if (this.readonly) return;
    const e = await this.getContext(g);
    if (!e) return;
    const i = e.open(this, x, {
      data: {
        allowedResourceTypes: this._allowedResourceTypes,
        currentResourceId: (t = this._value) == null ? void 0 : t.resourceId
      }
    });
    try {
      const r = await i.onSubmit();
      if (r != null && r.selection) {
        const n = r.selection.resourceId === ((s = this._value) == null ? void 0 : s.resourceId) ? (a = this._value) == null ? void 0 : a.altText : null;
        this._updateValue({
          ...r.selection,
          altText: n ?? null
        });
      }
    } catch {
    }
  }
  _onAltTextChange(e) {
    if (!this._value || this.readonly) return;
    const i = e.target;
    this._updateValue({
      ...this._value,
      altText: i.value.trim() ? i.value : null
    });
  }
  _clearSelection() {
    this.readonly || this._updateValue(void 0);
  }
  _updateValue(e) {
    this.value = e, this.dispatchEvent(new m());
  }
  render() {
    var r;
    if (!((r = this._value) != null && r.resourceId))
      return o`
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
    const e = this._liveDetail, i = e !== null && !e.isAvailable, t = w((e == null ? void 0 : e.previewUrl) ?? (e == null ? void 0 : e.thumbnailUrl)), s = (e != null && e.isAvailable ? e.title : this._value.title) ?? `Resource #${this._value.resourceId}`, a = (e == null ? void 0 : e.defaultAltText) ?? s;
    return o`
      <div class="selected-card ${i ? "unavailable" : ""}">
        ${this._loadingLive ? o`<uui-loader-bar></uui-loader-bar>` : c}

        <div class="card-body">
          <div class="preview-box">
            ${t ? o`<img src=${t} alt=${s} referrerpolicy="no-referrer" />` : o`<uui-icon name="icon-document" class="doc-icon"></uui-icon>`}
          </div>

          <div class="info-box">
            <div class="header-row">
              <strong class="asset-title">${s}</strong>
              <div class="badges">
                <uui-tag look="secondary">Ref #${this._value.resourceId}</uui-tag>
                ${this._value.fileExtension ? o`<uui-tag look="outline">${this._value.fileExtension.toUpperCase()}</uui-tag>` : c}
                ${i ? o`<uui-tag color="warning">Unavailable in DAM</uui-tag>` : o`<uui-tag color="positive" look="outline">Live in DAM</uui-tag>`}
              </div>
            </div>

            ${i ? o`
                  <div class="warning-banner">
                    ${(e == null ? void 0 : e.unavailableReason) ?? "This asset was deleted, archived, or restricted in ResourceSpace."}
                  </div>
                ` : c}

            <label class="alt-field">
              <span>Editorial alt text override (optional — defaults to DAM caption)</span>
              <uui-input
                label="Editorial alt text override"
                placeholder=${a}
                .value=${this._value.altText ?? ""}
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
                @click=${() => this._loadLiveDetail(this._value.resourceId, !0)}></uui-button>
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
};
l.styles = v`
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
      background: var(--uui-color-warning-standalone, #fff4ce);
      color: #5c4500;
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
u([
  d({ type: Object })
], l.prototype, "value", 1);
u([
  d({ type: Boolean, reflect: !0 })
], l.prototype, "readonly", 2);
u([
  p()
], l.prototype, "_allowedResourceTypes", 2);
u([
  d({ attribute: !1 })
], l.prototype, "config", 1);
u([
  p()
], l.prototype, "_liveDetail", 2);
u([
  p()
], l.prototype, "_loadingLive", 2);
l = u([
  h("resourcespace-asset-picker")
], l);
const C = l;
export {
  l as ResourceSpaceAssetPickerElement,
  C as default
};
