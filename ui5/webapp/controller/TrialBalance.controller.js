sap.ui.define([
    "s4herp/ui/controller/BaseController",
    "sap/ui/model/json/JSONModel",
    "s4herp/ui/model/formatter",
    "sap/m/MessageBox"
], (BaseController, JSONModel, formatter, MessageBox) => {
    "use strict";

    return BaseController.extend("s4herp.ui.controller.TrialBalance", {
        formatter,

        onInit() {
            this.getView().setModel(new JSONModel({ rows: [], difference: 0 }));
            this.getRouter().getRoute("trialBalance")
                .attachPatternMatched(this.onRefresh, this);
        },

        async onRefresh() {
            const view = this.getView();
            const context = this.contextData();
            view.setBusy(true);

            try {
                const result = await this.api().request(
                    "/api/v1/finance/reports/trial-balance"
                    + "?companyCode=" + encodeURIComponent(context.companyCode)
                    + "&fiscalYear=" + encodeURIComponent(context.fiscalYear),
                    { context });
                view.getModel().setData(result);
            } catch (error) {
                MessageBox.error(error.message, {
                    title: this.text("requestFailed"),
                    details: error.correlationId
                        ? this.text("correlationId") + ": " + error.correlationId
                        : undefined
                });
            } finally {
                view.setBusy(false);
            }
        },

        /**
         * Client-side export of what is already on screen. A server-side export
         * is a separately authorised action (S_EXPORT) with its own row cap; this
         * cannot exceed what the user was already allowed to read.
         */
        onExport() {
            const data = this.getView().getModel().getData();
            const header = ["GLAccount", "Description", "Debit", "Credit", "Balance"];
            const rows = (data.rows || []).map((r) => [
                r.glAccount,
                '"' + String(r.description || "").replace(/"/g, '""') + '"',
                r.debit, r.credit, r.balance
            ].join(","));

            const csv = [header.join(","), ...rows].join("\n");
            const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
            const link = document.createElement("a");
            link.href = URL.createObjectURL(blob);
            link.download = "trial-balance-" + data.companyCode + "-" + data.fiscalYear + ".csv";
            link.click();
            URL.revokeObjectURL(link.href);
        }
    });
});
