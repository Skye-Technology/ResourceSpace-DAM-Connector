const n = "/umbraco/management/api/v1/resourcespace";
class i {
  constructor(e) {
    this._getToken = e;
  }
  async search(e) {
    const t = new URLSearchParams();
    return e.query && t.set("query", e.query), e.resourceTypes && t.set("resourceTypes", e.resourceTypes), t.set("skip", String(e.skip ?? 0)), t.set("take", String(e.take ?? 24)), this._request(`${n}/search?${t.toString()}`);
  }
  async getAsset(e) {
    return this._request(
      `${n}/assets/${encodeURIComponent(e)}`,
      { allow404: !0 }
    );
  }
  async refreshAsset(e) {
    return this._request(
      `${n}/assets/${encodeURIComponent(e)}/refresh`,
      { method: "POST", allow404: !0 }
    );
  }
  async getResourceTypes() {
    return this._request(`${n}/resource-types`);
  }
  async _request(e, t) {
    const a = await this._getToken(), s = await fetch(e, {
      method: (t == null ? void 0 : t.method) ?? "GET",
      headers: {
        Accept: "application/json",
        ...a ? { Authorization: `Bearer ${a}` } : {}
      }
    });
    if (t != null && t.allow404 && s.status === 404)
      return null;
    if (!s.ok) {
      let c = `HTTP ${s.status}`;
      try {
        const r = await s.json();
        c = (r == null ? void 0 : r.detail) || (r == null ? void 0 : r.title) || c;
      } catch {
      }
      throw new Error(c);
    }
    return await s.json();
  }
}
function o(u) {
  if (!u) return null;
  const e = u.trim();
  if (!e || /[\u0000-\u001F\u007F]/.test(e))
    return null;
  if (e.startsWith("/"))
    return e.startsWith("//") || e.startsWith("/\\") ? null : e;
  try {
    const t = new URL(e);
    if (t.protocol === "http:" || t.protocol === "https:")
      return e;
  } catch {
    return null;
  }
  return null;
}
export {
  i as R,
  o as s
};
