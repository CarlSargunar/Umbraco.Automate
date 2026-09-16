export const TREE_PICKER_UI_ALIAS = "Umb.Automate.TreePicker";

const treePicker: UmbExtensionManifest = {
    type: "propertyEditorUi",
    alias: TREE_PICKER_UI_ALIAS,
    name: "Automate Tree Picker",
    element: () => import("./tree-picker.element.js"),
    meta: {
        label: "Tree Picker",
        icon: "icon-document",
        group: "Automate",
    },
};

export const treePickerManifests: UmbExtensionManifest[] = [
    treePicker,
];
