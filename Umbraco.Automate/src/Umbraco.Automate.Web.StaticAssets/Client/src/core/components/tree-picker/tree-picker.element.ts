import { css, customElement, html, property, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import type {
    UmbPropertyEditorConfigCollection,
    UmbPropertyEditorUiElement,
} from "@umbraco-cms/backoffice/property-editor";
import type { BindingSource } from "../../utils/binding-context.utils.js";
import "../binding-picker/binding-picker-button.element.js";

type TreeEntityType = "document" | "media";

/**
 * Picks a single content or media node, holding its key as the value.
 *
 * The value is one string either way, because the same setting can be a node the author
 * chose now or an expression resolved per run — a run can create the folder its own items
 * go into, and you cannot click a node that doesn't exist yet. So the picker is shown while
 * the value is a key, the expression is shown as text once one is inserted, and the binding
 * button sits alongside in both states.
 */
@customElement("ua-tree-picker")
export class UaTreePickerElement extends UmbLitElement implements UmbPropertyEditorUiElement {
    @property()
    public value?: string;

    @property({ type: Boolean, reflect: true })
    readonly = false;

    @state()
    private _entityType: TreeEntityType = "document";

    @state()
    private _bindingSources: BindingSource[] = [];

    public set config(config: UmbPropertyEditorConfigCollection | undefined) {
        if (!config) return;

        this._entityType = config.getValueByAlias<TreeEntityType>("entityType") ?? "document";
        this._bindingSources = config.getValueByAlias<BindingSource[]>("bindingSources") ?? [];
    }

    /** An expression is anything holding a `${ ... }` binding, which no picker can represent. */
    get #isExpression(): boolean {
        return this.value?.includes("${") === true;
    }

    #setValue(value: string | undefined) {
        const next = value === "" ? undefined : value;
        if (next === this.value) return;

        this.value = next;
        this.dispatchEvent(new UmbChangeEvent());
    }

    #onPick(event: CustomEvent) {
        // Both node inputs are form controls over a comma-separated key list. Selection is
        // capped at one, so the whole value is the key.
        this.#setValue((event.target as HTMLElement & { value?: string }).value);
    }

    #onTypeExpression(event: InputEvent) {
        this.#setValue((event.target as HTMLInputElement).value);
    }

    #onInsertBinding(event: CustomEvent) {
        const { expression } = (event as CustomEvent<{ expression: string }>).detail;

        // Inserting a binding replaces a picked node rather than appending to its key, which
        // would make an unusable half-key half-expression value.
        this.#setValue(this.#isExpression ? (this.value ?? "") + expression : expression);
    }

    #renderInput() {
        if (this.#isExpression) {
            return html`
                <uui-input
                    .value=${this.value ?? ""}
                    ?readonly=${this.readonly}
                    label="Binding expression"
                    @input=${this.#onTypeExpression}>
                </uui-input>
            `;
        }

        return this._entityType === "media"
            ? html`
                <umb-input-media
                    .max=${1}
                    .value=${this.value ?? ""}
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

    override render() {
        return html`
            <div id="wrapper">
                <div id="input">${this.#renderInput()}</div>
                <ua-binding-picker-button
                    .sources=${this._bindingSources}
                    @ua:binding-select=${this.#onInsertBinding}>
                </ua-binding-picker-button>
            </div>
        `;
    }

    static override styles = [
        css`
            #wrapper {
                display: flex;
                align-items: flex-start;
                gap: var(--uui-size-space-2);
            }

            #input {
                flex: 1;
                min-width: 0;
            }

            uui-input {
                width: 100%;
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
