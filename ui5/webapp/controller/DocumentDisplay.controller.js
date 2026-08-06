sap.ui.define([
    "s4herp/ui/controller/BaseController",
    "sap/ui/model/json/JSONModel",
    "s4herp/ui/model/formatter",
    "sap/m/MessageBox",
    "sap/m/MessageToast"
], (BaseController, JSONModel, formatter, MessageBox, MessageToast) => {
    "use strict";

    return BaseController.extend("s4herp.ui.controller.DocumentDisplay", {
        formatter,

        onInit() {
            this.getView().setModel(new JSONModel({ lines: [] }));
            this.getRouter().getRoute("journalDisplay")
                .attachPatternMatched(this._onRouteMatched, this);
        },

        _onRouteMatched(event) {
            this._key = event.getParameter("arguments");
            this._load();
        },

        async _load() {
            const view = this.getView();
            view.setBusy(true);
            try {
                const result = await this.api().request(
                    "/api/v1/finance/journal-entries/"
                    + encodeURIComponent(this._key.companyCode) + "/"
                    + encodeURIComponent(this._key.fiscalYear) + "/"
                    + encodeURIComponent(this._key.documentNumber),
                    { context: this.contextData() });
                view.getModel().setData(result);
            } catch (error) {
                MessageBox.error(error.message, { title: this.text("requestFailed") });
            } finally {
                view.setBusy(false);
            }
        },

        onReverse() {
            // A reversal reason is mandatory server-side, so ask rather than
            // sending a placeholder the auditor would later have to interpret.
            MessageBox.confirm(this.text("reverseConfirm"), {
                title: this.text("reverse"),
                onClose: (action) => {
                    if (action === MessageBox.Action.OK) {
                        this._reverse();
                    }
                }
            });
        },

        async _reverse() {
            const view = this.getView();
            view.setBusy(true);
            try {
                const result = await this.api().request(
                    "/api/v1/finance/journal-entries/"
                    + encodeURIComponent(this._key.companyCode) + "/"
                    + encodeURIComponent(this._key.fiscalYear) + "/"
                    + encodeURIComponent(this._key.documentNumber) + "/reverse",
                    {
                        method: "POST",
                        body: { reversalReasonCode: "01" },
                        context: this.contextData()
                    });

                MessageToast.show(this.text("reversedOk", [result.reversalDocumentNumberFormatted]));
                if (result.postingDateMoved) {
                    MessageBox.information(this.text("reversalDateMoved", [result.reversalPostingDate]));
                }
                this._load();
            } catch (error) {
                MessageBox.error(error.message, { title: this.text("requestFailed") });
            } finally {
                view.setBusy(false);
            }
        }
    });
});
