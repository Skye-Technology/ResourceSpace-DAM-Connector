/**
 * Validates that a DAM preview/thumbnail URL uses an explicit http(s) scheme
 * or a safe site-relative path (excluding protocol-relative `//`).
 */
export function sanitizeAssetUrl(url: string | null | undefined): string | null {
  if (!url) return null;
  const trimmed = url.trim();
  if (!trimmed || /[\u0000-\u001F\u007F]/.test(trimmed)) {
    return null;
  }

  if (trimmed.startsWith('/')) {
    return trimmed.startsWith('//') || trimmed.startsWith('/\\') ? null : trimmed;
  }

  try {
    const parsed = new URL(trimmed);
    if (parsed.protocol === 'http:' || parsed.protocol === 'https:') {
      return trimmed;
    }
  } catch {
    return null;
  }

  return null;
}
