import { css, customElement, html, property, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import type {
    UmbPropertyEditorConfigCollection,
    UmbPropertyEditorUiElement,
} from "@umbraco-cms/backoffice/property-editor";

type TreeEntityType = "document" | "media";
type MediaFolderFilter = "filesOnly" | "foldersOnly" | "filesAndFolders";

/**
 * Picks a single content or media node, holding its key as the value.
 *
 * Both CMS inputs are form controls over a comma-separated key list. Selection is capped at
 * one, so the whole value is the key and the setting stays a plain string.
 */
@customElement("ua-tree-picker")
export class UaTreePickerElement extends UmbLitElement implements UmbPropertyEditorUiElement {
    @property()
    public value?: string;

    @property({ type: Boolean, reflect: true })
    readonly = false;

    @state()
    private _entityType: TreeEntityType = "document";

    /**
     * Media only. `foldersOnly` is what a parent field wants, since a media item can only be
     * created inside a folder, never inside another image.
     */
    @state()
    private _folderFilter: MediaFolderFilter = "filesAndFolders";

    public set config(config: UmbPropertyEditorConfigCollection | undefined) {
        if (!config) return;

        this._entityType = config.getValueByAlias<TreeEntityType>("entityType") ?? "document";
        this._folderFilter = config.getValueByAlias<MediaFolderFilter>("folderFilter") ?? "filesAndFolders";
    }

    #onPick(event: CustomEvent) {
        const picked = (event.target as HTMLElement & { value?: string }).value;
        const next = picked === "" ? undefined : picked;
        if (next === this.value) return;

        this.value = next;
        this.dispatchEvent(new UmbChangeEvent());
    }

    override render() {
        return this._entityType === "media"
            ? html`
                <umb-input-media
                    .max=${1}
                    .value=${this.value ?? ""}
                    folder-filter=${this._folderFilter}
                    ?readonly=${this.readonly}
                    @change=${this.#onPick}>
                </umb-input-media>
            `
            : html`
                <umb-input-document
                    .max=${1}
                    .value=${this.value ?? ""}
                    ?readonly=${this.readonly}
                    @change=${this.#onPick}>
                </umb-input-document>
            `;
    }

    static override styles = [
        css`
            :host {
                display: block;
            }
        `,
    ];
}

export default UaTreePickerElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-tree-picker": UaTreePickerElement;
    }
}
