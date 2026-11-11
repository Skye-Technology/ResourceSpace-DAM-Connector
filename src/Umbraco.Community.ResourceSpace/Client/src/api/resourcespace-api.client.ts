import type {
  DamAssetDetail,
  DamResourceTypeOption,
  DamSearchResult,
} from '../types.js';

export type TokenProvider = () => Promise<string | undefined>;

const BASE_PATH = '/umbraco/management/api/v1/resourcespace';

export class ResourceSpaceApiClient {
  constructor(private readonly _getToken: TokenProvider) {}

  async search(params: {
    query?: string;
    resourceTypes?: string;
    skip?: number;
    take?: number;
  }): Promise<DamSearchResult> {
    const qs = new URLSearchParams();
    if (params.query) qs.set('query', params.query);
    if (params.resourceTypes) qs.set('resourceTypes', params.resourceTypes);
    qs.set('skip', String(params.skip ?? 0));
    qs.set('take', String(params.take ?? 24));

    return this._request<DamSearchResult>(`${BASE_PATH}/search?${qs.toString()}`);
  }

  async getAsset(resourceId: string): Promise<DamAssetDetail | null> {
    return this._request<DamAssetDetail | null>(
      `${BASE_PATH}/assets/${encodeURIComponent(resourceId)}`,
      { allow404: true }
    );
  }

  async refreshAsset(resourceId: string): Promise<DamAssetDetail | null> {
    return this._request<DamAssetDetail | null>(
      `${BASE_PATH}/assets/${encodeURIComponent(resourceId)}/refresh`,
      { method: 'POST', allow404: true }
    );
  }

  async getResourceTypes(): Promise<DamResourceTypeOption[]> {
    return this._request<DamResourceTypeOption[]>(`${BASE_PATH}/resource-types`);
  }

  private async _request<T>(
    url: string,
    options?: { method?: string; allow404?: boolean }
  ): Promise<T> {
    const token = await this._getToken();
    const response = await fetch(url, {
      method: options?.method ?? 'GET',
      headers: {
        Accept: 'application/json',
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
    });

    if (options?.allow404 && response.status === 404) {
      return null as T;
    }

    if (!response.ok) {
      let detail = `HTTP ${response.status}`;
      try {
        const problem = await response.json();
        detail = problem?.detail || problem?.title || detail;
      } catch {
        // ignore JSON parse failure on error body
      }
      throw new Error(detail);
    }

    return (await response.json()) as T;
  }
}
