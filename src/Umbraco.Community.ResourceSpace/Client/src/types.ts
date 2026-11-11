export interface DamAssetReference {
  resourceId: string;
  provider: string;
  title?: string | null;
  altText?: string | null;
  fileExtension?: string | null;
  resourceType?: string | null;
  width?: number | null;
  height?: number | null;
  credit?: string | null;
  selectedAtUtc?: string | null;
}

export interface DamRendition {
  code: string;
  name: string;
  url: string;
  width?: number | null;
  height?: number | null;
  fileSizeBytes?: number | null;
  fileExtension?: string | null;
}

export interface DamAssetSummary {
  resourceId: string;
  provider: string;
  title: string;
  fileExtension: string;
  resourceType: string;
  resourceTypeId?: string | null;
  thumbnailUrl?: string | null;
  previewUrl?: string | null;
  width?: number | null;
  height?: number | null;
  credit?: string | null;
  createdAt?: string | null;
}

export interface DamAssetDetail {
  resourceId: string;
  provider: string;
  title: string;
  description?: string | null;
  defaultAltText?: string | null;
  credit?: string | null;
  fileExtension: string;
  resourceType: string;
  isAvailable: boolean;
  unavailableReason?: string | null;
  originalWidth?: number | null;
  originalHeight?: number | null;
  thumbnailUrl?: string | null;
  previewUrl?: string | null;
  originalUrl?: string | null;
  renditions: Record<string, DamRendition>;
  metadata: Record<string, string>;
}

export interface DamSearchResult {
  total: number;
  items: DamAssetSummary[];
}

export interface DamResourceTypeOption {
  id: string;
  name: string;
}
