sap.ui.define([
    "s4herp/ui/controller/BaseController",
    "sap/ui/model/json/JSONModel",
    "s4herp/ui/model/formatter",
    "sap/m/MessageBox",
    "sap/m/MessageToast",
    "sap/m/Dialog",
    "sap/m/Button",
    "sap/m/Input",
    "sap/m/Label",
    "sap/ui/layout/form/SimpleForm"
], (BaseController, JSONModel, formatter, MessageBox, MessageToast,
    Dialog, Button, Input, Label, SimpleForm) => {
    "use strict";

    /**
     * One statement, and whether the ledger agrees with it.
     *
     * The two balances and their difference sit in the header, together: reading
     * a difference off two screens is how a reconciliation gets skipped. The
     * lines below are the working — what the bank did, what it corresponds to,
     * and what nobody has explained yet.
     */
    return BaseController.extend("s4herp.ui.controller.BankStatement", {
        formatter,

        onInit() {
            this.getView().setModel(new JSONModel({ lines: [] }));
            this.getView().setModel(new JSONModel({}), "recon");
            this.getRouter().getRoute("bankStatement")
                .attachPatternMatched(this._onRouteMatched, this);
        },

        _onRouteMatched(event) {
            this._statementId = event.getParameter("arguments").statementId;
            this._load();
        },

        _path(suffix) {
            return "/api/v1/finance/bank-statements/"
                + encodeURIComponent(this._statementId) + (suffix || "");
        },

        async _load() {
            const view = this.getView();
            view.setBusy(true);
            try {
                // Both in one go: the page is meaningless with either half
                // missing, and two independent loads racing to paint it is the
                // shape that produced three test races in increment 10.
                const [statement, reconciliation] = await Promise.all([
                    this.api().request(this._path(), { context: this.contextData() }),
                    this.api().request(this._path("/reconciliation"), { context: this.contextData() })
                ]);

                view.getModel().setData(statement);
                view.getModel("recon").setData(reconciliation);
            } catch (error) {
                MessageBox.error(error.message, { title: this.text("requestFailed") });
            } finally {
                view.setBusy(false);
            }
        },

        onRefresh() {
            this._load();
        },

        onMatch(event) {
            const line = event.getSource().getBindingContext().getObject();

            const year = new Input({
                id: this.createId("matchYear"),
                value: String(new Date(this.getView().getModel().getProperty("/statementDate")).getFullYear())
            });
            const document = new Input({ id: this.createId("matchDocument") });

            const dialog = new Dialog({
                title: this.text("bankStatementMatchTitle", [line.lineNumber]),
                contentWidth: "26rem",
                content: [new SimpleForm({
                    editable: true,
                    layout: "ResponsiveGridLayout",
                    content: [
                        // Shown so the person types a document against a movement
                        // they can see, rather than against a line number.
                        new Label({ text: this.text("amount") }),
                        new Input({
                            value: `${line.isCredit ? "+" : "-"}${formatter.amount(line.amount)}`,
                            editable: false
                        }),
                        new Label({ text: this.text("fiscalYear") }), year,
                        new Label({ text: this.text("paymentRunDocument") }), document
                    ]
                })],
                beginButton: new Button({
                    id: this.createId("matchConfirm"),
                    text: this.text("bankStatementMatch"),
                    type: "Emphasized",
                    press: () => {
                        dialog.close();
                        this._act(`/lines/${line.lineNumber}/match`, {
                            fiscalYear: Number(year.getValue()),
                            documentNumber: Number(document.getValue())
                        }, "bankStatementMatched");
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

        onIgnore(event) {
            const line = event.getSource().getBindingContext().getObject();

            // Mandatory reason, enforced here as well as by the server: an
            // unexplained dismissal is indistinguishable from an oversight, and
            // this is the button that makes a movement stop being asked about.
            this._promptComment("bankStatementIgnore", "bankStatementIgnoreReason", true,
                (reason) => this._act(`/lines/${line.lineNumber}/ignore`,
                    { reason }, "bankStatementIgnored"));
        },

        async _act(suffix, body, successKey) {
            const view = this.getView();
            view.setBusy(true);
            try {
                await this.api().request(this._path(suffix), {
                    method: "POST",
                    body,
                    context: this.contextData()
                });
                MessageToast.show(this.text(successKey));
                await this._load();
            } catch (error) {
                MessageBox.error(error.message, { title: this.text("requestFailed") });
            } finally {
                view.setBusy(false);
            }
        }
    });
});
