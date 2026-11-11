const c = "/umbraco/management/api/v1/resourcespace";
class o {
  constructor(e) {
    this._getToken = e;
  }
  async search(e) {
    const t = new URLSearchParams();
    return e.query && t.set("query", e.query), e.resourceTypes && t.set("resourceTypes", e.resourceTypes), t.set("skip", String(e.skip ?? 0)), t.set("take", String(e.take ?? 24)), this._request(`${c}/search?${t.toString()}`);
  }
  async getAsset(e) {
    return this._request(
      `${c}/assets/${encodeURIComponent(e)}`,
      { allow404: !0 }
    );
  }
  async refreshAsset(e) {
    return this._request(
      `${c}/assets/${encodeURIComponent(e)}/refresh`,
      { method: "POST", allow404: !0 }
    );
  }
  async getResourceTypes() {
    return this._request(`${c}/resource-types`);
  }
  async _request(e, t) {
    const n = await this._getToken(), r = await fetch(e, {
      method: (t == null ? void 0 : t.method) ?? "GET",
      headers: {
        Accept: "application/json",
        ...n ? { Authorization: `Bearer ${n}` } : {}
      }
    });
    if (t != null && t.allow404 && r.status === 404)
      return null;
    if (!r.ok) {
      let a = `HTTP ${r.status}`;
      try {
        const s = await r.json();
        a = (s == null ? void 0 : s.detail) || (s == null ? void 0 : s.title) || a;
      } catch {
      }
      throw new Error(a);
    }
    return await r.json();
  }
}
export {
  o as R
};
//# sourceMappingURL=resourcespace-api.client-eLZvwiWC.js.map
