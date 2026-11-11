import { UmbModalToken } from '@umbraco-cms/backoffice/modal';
import type { DamAssetReference } from '../types.js';

export interface ResourceSpacePickerModalData {
  allowedResourceTypes?: string;
  currentResourceId?: string;
}

export interface ResourceSpacePickerModalValue {
  selection: DamAssetReference;
}

export const RESOURCESPACE_PICKER_MODAL = new UmbModalToken<
  ResourceSpacePickerModalData,
  ResourceSpacePickerModalValue
>('ResourceSpace.Modal.AssetPicker', {
  modal: {
    type: 'sidebar',
    size: 'large',
  },
});
