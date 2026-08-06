sap.ui.define([
    "s4herp/ui/controller/BaseController",
    "sap/ui/model/json/JSONModel",
    "s4herp/ui/model/formatter",
    "sap/m/MessageBox"
], (BaseController, JSONModel, formatter, MessageBox) => {
    "use strict";

    /**
     * The approver's inbox. Deliberately thin: it lists what the server says is
     * waiting on this user and navigates to the document, where the decision is
     * taken with the document in front of you. Approving from a list, without
     * seeing the lines, is how rubber-stamping happens.
     */
    return BaseController.extend("s4herp.ui.controller.Approvals", {
        formatter,

        onInit() {
            this.getView().setModel(new JSONModel({ items: [] }));
            this.getRouter().getRoute("approvals")
                .attachPatternMatched(this._load, this);
        },

        async _load() {
            const view = this.getView();
            view.setBusy(true);
            try {
                const items = await this.api().request(
                    "/api/v1/finance/approvals", { context: this.contextData() });
                view.getModel().setData({ items: items || [] });
            } catch (error) {
                MessageBox.error(error.message, { title: this.text("requestFailed") });
            } finally {
                view.setBusy(false);
            }
        },

        onRefresh() {
            this._load();
        },

        onOpen(event) {
            const item = event.getSource().getBindingContext().getObject();

            // The inbox carries the formatted document id — KSS-1000-2026-SA-
            // 0100000042 — because that is what Workflow stores as the object key.
            // The route needs the three parts of the business key back.
            const parts = /^KSS-(\d+)-(\d+)-[A-Z0-9]+-(\d+)$/.exec(item.documentId);
            if (!parts) {
                MessageBox.error(this.text("approvalsUnparseableId", [item.documentId]));
                return;
            }

            this.getRouter().navTo("journalDisplay", {
                companyCode: parts[1],
                fiscalYear: parts[2],
                documentNumber: Number(parts[3])
            });
        }
    });
});
