sap.ui.define([
    "s4herp/ui/controller/BaseController",
    "sap/ui/model/json/JSONModel",
    "s4herp/ui/model/formatter",
    "sap/m/MessageBox",
    "sap/m/MessageToast",
    "sap/m/Dialog",
    "sap/m/Button",
    "sap/m/TextArea"
], (BaseController, JSONModel, formatter, MessageBox, MessageToast,
    Dialog, Button, TextArea) => {
    "use strict";

    /**
     * Imported bank statements. The entry point to the only activity in this
     * system that asks whether the ledger is telling the truth.
     */
    return BaseController.extend("s4herp.ui.controller.BankStatements", {
        formatter,

        onInit() {
            this.getView().setModel(new JSONModel({ statements: [] }));
            this.getRouter().getRoute("bankStatements")
                .attachPatternMatched(this._load, this);
        },

        async _load() {
            const view = this.getView();
            view.setBusy(true);
            try {
                const statements = await this.api().request(
                    "/api/v1/finance/bank-statements", { context: this.contextData() });
                view.getModel().setData({ statements: statements || [] });
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
            this.getRouter().navTo("bankStatement", {
                statementId: event.getSource().getBindingContext().getObject().statementId
            });
        },

        onImport() {
            // Pasted rather than uploaded. A bank feed is an integration, not a
            // person with a file, and building a file picker would dress up the
            // absence of one — the honest interim is the same XML the API takes.
            const input = new TextArea({
                id: this.createId("statementXml"),
                width: "100%",
                rows: 12,
                placeholder: this.text("bankStatementsPastePlaceholder")
            });

            const dialog = new Dialog({
                title: this.text("bankStatementsImport"),
                contentWidth: "44rem",
                content: [input],
                beginButton: new Button({
                    id: this.createId("statementImportConfirm"),
                    text: this.text("bankStatementsImport"),
                    type: "Emphasized",
                    press: () => {
                        dialog.close();
                        this._import(input.getValue());
                    }
                }),
                endButton: new Button({
                    text: this.text("cancel"),
                    press: () => dialog.close()
                }),
                afterClose: () => dialog.destroy()
            });

            this.getView().addDependent(dialog);
            dialog.open();
        },

        async _import(xml) {
            const view = this.getView();
            view.setBusy(true);
            try {
                const response = await fetch("/api/v1/finance/bank-statements", {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/xml",
                        "X-S4HERP-User": this.contextData().userName || ""
                    },
                    body: xml
                });

                const result = await response.json();
                if (!response.ok) {
                    throw new Error(result.detail || this.text("requestFailed"));
                }

                MessageToast.show(result.alreadyImported
                    ? this.text("bankStatementsAlreadyImported")
                    : this.text("bankStatementsImported", [result.lineCount, result.unmatched]));

                this.getRouter().navTo("bankStatement", { statementId: result.statementId });
            } catch (error) {
                MessageBox.error(error.message, { title: this.text("requestFailed") });
            } finally {
                view.setBusy(false);
            }
        }
    });
});
