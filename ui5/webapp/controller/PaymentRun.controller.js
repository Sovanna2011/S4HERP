sap.ui.define([
    "s4herp/ui/controller/BaseController",
    "sap/ui/model/json/JSONModel",
    "s4herp/ui/model/formatter",
    "sap/m/MessageBox",
    "sap/m/MessageToast"
], (BaseController, JSONModel, formatter, MessageBox, MessageToast) => {
    "use strict";

    /**
     * One payment run, from proposal to bank file.
     *
     * The run is the transaction that moves the most money in the system, and
     * until now it was the only approval path with no screen — releasing one
     * meant an API call. Everything the run's state permits is here, and nothing
     * is hidden on the grounds that the caller might lack the authority: the
     * server refuses, visibly, which is the lesson worth teaching.
     */
    return BaseController.extend("s4herp.ui.controller.PaymentRun", {
        formatter,

        onInit() {
            this.getView().setModel(new JSONModel({ payments: [], excluded: [], approvalSteps: [] }));
            // A separate model because the file is a separate resource with a
            // separate permission: metadata may 404 for a run that has none, and
            // that is a normal answer rather than an error.
            this.getView().setModel(new JSONModel({}), "file");
            this.getRouter().getRoute("paymentRun")
                .attachPatternMatched(this._onRouteMatched, this);
        },

        _onRouteMatched(event) {
            this._runId = event.getParameter("arguments").runId;
            this._load();
        },

        _path(suffix) {
            return "/api/v1/finance/payment-runs/"
                + encodeURIComponent(this._runId) + (suffix || "");
        },

        async _load() {
            const view = this.getView();
            view.setBusy(true);
            try {
                const run = await this.api().request(
                    this._path(), { context: this.contextData() });
                view.getModel().setData(run);

                // Only executed runs can have one, so asking earlier would be a
                // guaranteed 404 on every proposal a user opens.
                view.getModel("file").setData(
                    run.status === "Executed" ? await this._loadFile() : {});
            } catch (error) {
                MessageBox.error(error.message, { title: this.text("requestFailed") });
            } finally {
                view.setBusy(false);
            }
        },

        async _loadFile() {
            try {
                return await this.api().request(
                    this._path("/payment-file"), { context: this.contextData() });
            } catch {
                // PAYMENT_FILE_NOT_GENERATED is the expected answer for a run
                // nobody has generated a file for yet. Reporting it as an error
                // would put a red box on a perfectly ordinary page.
                return {};
            }
        },

        onRefresh() {
            this._load();
        },

        onSubmit() {
            this._act("/submit", {}, "paymentRunSubmitted");
        },

        onApprove() {
            this._promptComment("approve", "approveComment", false, (comment) =>
                this._act("/approve", { comment }, "paymentRunApproved"));
        },

        onReject() {
            this._promptComment("reject", "rejectComment", true, (comment) =>
                this._act("/reject", { comment }, "paymentRunRejected"));
        },

        onExecute() {
            // The one irreversible step on this screen: executing posts payment
            // documents and clears open items, and there is no un-execute. The
            // total is in the prompt because that is the number worth reading
            // twice.
            const model = this.getView().getModel();
            MessageBox.warning(
                this.text("paymentRunExecuteConfirm", [
                    formatter.amount(model.getProperty("/totalToPay")),
                    model.getProperty("/currency")
                ]),
                {
                    title: this.text("paymentRunExecute"),
                    actions: [MessageBox.Action.OK, MessageBox.Action.CANCEL],
                    emphasizedAction: MessageBox.Action.CANCEL,
                    onClose: (action) => {
                        if (action === MessageBox.Action.OK) {
                            this._act("/execute", {}, "paymentRunExecuted");
                        }
                    }
                });
        },

        onDelete() {
            MessageBox.warning(this.text("paymentRunDeleteConfirm"), {
                title: this.text("discard"),
                actions: [MessageBox.Action.DELETE, MessageBox.Action.CANCEL],
                emphasizedAction: MessageBox.Action.CANCEL,
                onClose: (action) => {
                    if (action === MessageBox.Action.DELETE) {
                        this._act("", null, "paymentRunDeleted", "DELETE");
                    }
                }
            });
        },

        onGenerateFile() {
            this._act("/payment-file", {}, "paymentRunFileGenerated");
        },

        async onDownloadFile() {
            // Fetched rather than linked so the user header goes with it, and so
            // a 403 lands in a message box instead of a blank browser tab. The
            // download is audited server-side; this is the client half.
            try {
                const xml = await this.api().request(
                    this._path("/payment-file/content"),
                    { context: this.contextData(), raw: true });

                const url = URL.createObjectURL(new Blob([xml], { type: "application/xml" }));
                const link = document.createElement("a");
                link.href = url;
                link.download = `${this._runId}.xml`;
                link.click();
                URL.revokeObjectURL(url);

                MessageToast.show(this.text("paymentRunFileDownloaded"));
                await this._load();
            } catch (error) {
                MessageBox.error(error.message, { title: this.text("requestFailed") });
            }
        },

        async _act(suffix, body, successKey, method) {
            const view = this.getView();
            view.setBusy(true);
            try {
                await this.api().request(this._path(suffix), {
                    method: method || "POST",
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
