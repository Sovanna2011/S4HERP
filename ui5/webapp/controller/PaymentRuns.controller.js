sap.ui.define([
    "s4herp/ui/controller/BaseController",
    "sap/ui/model/json/JSONModel",
    "s4herp/ui/model/formatter",
    "sap/m/MessageBox",
    "sap/m/Dialog",
    "sap/m/Button",
    "sap/m/Input",
    "sap/m/DatePicker",
    "sap/m/Label",
    "sap/ui/layout/form/SimpleForm"
], (BaseController, JSONModel, formatter, MessageBox,
    Dialog, Button, Input, DatePicker, Label, SimpleForm) => {
    "use strict";

    /**
     * The payment runs a person may see, and the way to start a new one.
     *
     * Before this the only route to a run was its id, which meant the approvals
     * inbox or a note on somebody's desk — fine for the run you were told about,
     * useless for "what did we pay last Tuesday".
     */
    return BaseController.extend("s4herp.ui.controller.PaymentRuns", {
        formatter,

        onInit() {
            this.getView().setModel(new JSONModel({ runs: [] }));
            this.getRouter().getRoute("paymentRuns")
                .attachPatternMatched(this._load, this);
        },

        async _load() {
            const view = this.getView();
            view.setBusy(true);
            try {
                const runs = await this.api().request(
                    "/api/v1/finance/payment-runs", { context: this.contextData() });
                view.getModel().setData({ runs: runs || [] });
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
            this.getRouter().navTo("paymentRun", {
                runId: event.getSource().getBindingContext().getObject().runId
            });
        },

        onCreate() {
            const context = this.contextData();
            const today = new Date();
            // Thirty days out by default, which is the seeded N030 term. A due-by
            // of today would propose almost nothing and look broken.
            const dueBy = new Date(today.getTime() + 30 * 24 * 60 * 60 * 1000);

            const fields = {
                companyCode: new Input({ value: context.companyCode || "1000" }),
                runDate: new DatePicker({
                    valueFormat: "yyyy-MM-dd", displayFormat: "yyyy-MM-dd",
                    value: this._isoDate(today)
                }),
                dueBy: new DatePicker({
                    valueFormat: "yyyy-MM-dd", displayFormat: "yyyy-MM-dd",
                    value: this._isoDate(dueBy)
                }),
                paymentMethod: new Input({ value: "T" }),
                houseBank: new Input({ value: "ACLED" }),
                houseBankAccount: new Input({ value: "MAIN" }),
                businessPartner: new Input({ placeholder: this.text("paymentRunsAllPartners") })
            };

            const form = new SimpleForm({
                editable: true,
                layout: "ResponsiveGridLayout",
                content: [
                    new Label({ text: this.text("companyCode") }), fields.companyCode,
                    new Label({ text: this.text("runDate") }), fields.runDate,
                    new Label({ text: this.text("dueBy") }), fields.dueBy,
                    new Label({ text: this.text("paymentMethod") }), fields.paymentMethod,
                    new Label({ text: this.text("houseBank") }), fields.houseBank,
                    new Label({ text: this.text("houseBankAccount") }), fields.houseBankAccount,
                    new Label({ text: this.text("businessPartner") }), fields.businessPartner
                ]
            });

            const dialog = new Dialog({
                id: this.createId("createRunDialog"),
                title: this.text("paymentRunsCreate"),
                contentWidth: "32rem",
                content: [form],
                beginButton: new Button({
                    id: this.createId("createRunConfirm"),
                    text: this.text("paymentRunsPropose"),
                    type: "Emphasized",
                    press: () => {
                        dialog.close();
                        this._propose({
                            companyCode: fields.companyCode.getValue(),
                            runDate: fields.runDate.getValue(),
                            dueBy: fields.dueBy.getValue(),
                            paymentMethod: fields.paymentMethod.getValue(),
                            houseBank: fields.houseBank.getValue(),
                            houseBankAccount: fields.houseBankAccount.getValue(),
                            businessPartner: fields.businessPartner.getValue() || null
                        });
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

        _isoDate(date) {
            return date.toISOString().slice(0, 10);
        },

        async _propose(body) {
            const view = this.getView();
            view.setBusy(true);
            try {
                const run = await this.api().request("/api/v1/finance/payment-runs", {
                    method: "POST",
                    body,
                    context: this.contextData()
                });
                // Straight to the proposal rather than back to the list: the
                // point of proposing is to look at what it selected, and what it
                // did not.
                this.getRouter().navTo("paymentRun", { runId: run.runId });
            } catch (error) {
                MessageBox.error(error.message, { title: this.text("requestFailed") });
            } finally {
                view.setBusy(false);
            }
        }
    });
});
