const e = [
  {
    type: "propertyEditorUi",
    alias: "ResourceSpace.PropertyEditorUi.AssetPicker",
    name: "ResourceSpace Asset Picker UI",
    element: () => import("./resourcespace-picker.element-SEGHyfFR.js"),
    meta: {
      label: "ResourceSpace Asset Picker",
      propertyEditorSchemaAlias: "ResourceSpace.AssetPicker",
      icon: "icon-picture",
      group: "media",
      settings: {
        properties: [
          {
            alias: "defaultRenditionSize",
            label: "Default rendition size code",
            description: "ResourceSpace size code resolved by default (e.g. scr, pre, hpr, original).",
            propertyEditorUiAlias: "Umb.PropertyEditorUi.TextBox"
          },
          {
            alias: "allowedResourceTypes",
            label: "Allowed ResourceSpace type IDs",
            description: 'Optional comma-separated ResourceSpace resource type IDs (e.g. "1" for Photo only, "1,2" for Photo & Document).',
            propertyEditorUiAlias: "Umb.PropertyEditorUi.TextBox"
          },
          {
            alias: "fallbackImageUrl",
            label: "Fallback image URL",
            description: "Optional placeholder URL used when a referenced asset has been deleted or restricted in ResourceSpace.",
            propertyEditorUiAlias: "Umb.PropertyEditorUi.TextBox"
          }
        ],
        defaultData: [
          {
            alias: "defaultRenditionSize",
            value: "scr"
          }
        ]
      }
    }
  },
  {
    type: "modal",
    alias: "ResourceSpace.Modal.AssetPicker",
    name: "ResourceSpace Asset Picker Modal",
    element: () => import("./resourcespace-picker-modal.element-MzoWesrx.js")
  }
];
export {
  e as manifests
};
