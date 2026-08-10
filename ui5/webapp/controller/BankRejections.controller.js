sap.ui.define([
    "s4herp/ui/controller/BaseController",
    "sap/ui/model/json/JSONModel",
    "s4herp/ui/model/formatter",
    "sap/m/MessageBox",
    "sap/m/MessageToast"
], (BaseController, JSONModel, formatter, MessageBox, MessageToast) => {
    "use strict";

    /**
     * Everything the bank has refused, across every run.
     *
     * The rejection query and the reversal action both existed after increment
     * 12; what was missing was a page. A treasurer had to already know which run
     * to open, which means the discrepancy between the ledger and the bank was
     * discoverable only by someone who already suspected it.
     */
    return BaseController.extend("s4herp.ui.controller.BankRejections", {
        formatter,

        onInit() {
            this.getView().setModel(new JSONModel({
                rejections: [], outstanding: 0, includeResolved: false
            }));
            this.getRouter().getRoute("bankRejections")
                .attachPatternMatched(this._load, this);
        },

        async _load() {
            const view = this.getView();
            const model = view.getModel();
            view.setBusy(true);
            try {
                const includeResolved = Boolean(model.getProperty("/includeResolved"));
                const rejections = await this.api().request(
                    `/api/v1/finance/payment-status-reports/rejections?includeResolved=${includeResolved}`,
                    { context: this.contextData() });

                model.setProperty("/rejections", rejections || []);
                // Counted from the rows rather than trusted from the filter: with
                // resolved ones shown, the list length is not the workload.
                model.setProperty("/outstanding",
                    (rejections || []).filter((r) => !r.isResolved).length);
            } catch (error) {
                MessageBox.error(error.message, { title: this.text("requestFailed") });
            } finally {
                view.setBusy(false);
            }
        },

        onRefresh() {
            this._load();
        },

        onToggleResolved(event) {
            this.getView().getModel().setProperty("/includeResolved", event.getParameter("selected"));
            this._load();
        },

        onOpenRun(event) {
            this.getRouter().navTo("paymentRun", {
                runId: event.getSource().getBindingContext().getObject().runId
            });
        },

        onResolve(event) {
            const item = event.getSource().getBindingContext().getObject();

            // Same mandatory reason as on the run screen, and the same handler
            // behind it. Reversing a posted payment from a list is still
            // reversing a posted payment.
            this._promptComment("paymentRunResolveRejection", "paymentRunResolveReason", true,
                async (comment) => {
                    const view = this.getView();
                    view.setBusy(true);
                    try {
                        const result = await this.api().request(
                            "/api/v1/finance/payment-status-reports/rejections/"
                            + encodeURIComponent(item.endToEndId) + "/resolve",
                            { method: "POST", body: { comment }, context: this.contextData() });

                        MessageToast.show(this.text("paymentRunRejectionResolved",
                            [result.reversalDocumentNumber, result.itemsReopened]));
                        await this._load();
                    } catch (error) {
                        MessageBox.error(error.message, { title: this.text("requestFailed") });
                    } finally {
                        view.setBusy(false);
                    }
                });
        }
    });
});
